using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Golovomka
{
    public partial class EchoMvp : MonoBehaviour
    {
        public enum Interaction
        {
            None,
            AnswerPhone,
            EnterLaboratory,
            PickUpRemote,
            UseRoomPassage,
            PressTimedButton,
            ReadNoticeBoard
        }

        public EchoHistory history;
        public EchoTape recording;
        public float elapsed;
        public int zone, facing = 3;
        public Vector2 position;
        public bool plateA;
        public bool plateB;
        public bool recordingActive;
        public string mode = "menu";
        [NonSerialized] public string saveFileOverride;
        [SerializeField] SpriteRenderer hero;
        [SerializeField, Min(0)] float moveSpeed = 3.6f;
        Vector3 heroBaseScale;
        CharacterWalk officerWalk;
        SpriteRenderer ghost;
        static readonly Vector2 FeetSize = new Vector2(.44f, .28f);
        static readonly ContactFilter2D SolidContacts = new ContactFilter2D { useTriggers = false };
        readonly List<Collider2D> overlapHits = new List<Collider2D>();
        readonly List<RaycastHit2D> movementHits = new List<RaycastHit2D>();
        Font font;
        Canvas canvas;
        RectTransform panel;
        const int DialogFontSize = 21;
        const float TitledDialogBodyY = -20f;
        Text prompt, returnHint, dialogTitle, dialogBody, caution;
        Text stationExitLabel;
        static readonly string[] PauseOptions = { "Продолжить", "Главное меню", "Выход" };
        static readonly int[] PauseOptionTop = { 355, 481, 606 };
        static readonly Vector2 PauseArtworkSize = new Vector2(1672, 941);
        const float PauseRowLeft = 86, PauseRowWidth = 728, PauseRowHeight = 122;
        readonly RawImage[] pauseRows = new RawImage[PauseOptions.Length];
        RectTransform pauseMenu;
        int pauseSelection;
        bool phoneRead;
        const float StationFadeDuration = .25f;
        UnityEngine.UI.Image stationFade;
        float stationFadeTime;
        float nextSample;
        PoliceStationLayout station;
        Room01Layout room01;
        readonly Dictionary<int, LaboratoryRoom> rooms = new Dictionary<int, LaboratoryRoom>();
        readonly List<(int room, SequenceRoomLayout layout)> sequences = new List<(int, SequenceRoomLayout)>();
        SequenceRoomLayout currentSequence;
        TimedButtonsRoom currentButtons;
        TargetRangeRoom currentRange;
        FinaleRoom currentFinale;
        float finaleRetryDelay;
        bool officerDying;
        float officerDeathTime;
        RawImage illustratedCard;
        Texture2D approachDefeatArt, accomplicesDefeatArt, ivanDeathArt, victoryArt, remoteHelpArt;
        bool replayFinale;
        TargetRangeRoom pistolArt;
        bool pistolDrawn;
        bool CanDrawPistol => pistolArt && zone >= 4;
        Vector2 ShotDirection => OfficerDirection(facing);
        bool officerMoving;
        LaboratoryRoom introductionRoom, currentRoom;
        GameObject[] stationRoots, laboratoryRoots;
        Color amber = new Color(1, .74f, .36f);

        IEnumerator Start()
        {
            if (!roomCamera || !hero)
            {
                Debug.LogError("Назначьте камеру и SpriteRenderer Ивана в компоненте EchoMvp.", this);
                yield break;
            }
            if (!hero.TryGetComponent(out officerWalk) || !officerWalk.IsConfigured)
            {
                Debug.LogError("Назначьте префаб Ивана с настроенной анимацией CharacterWalk.", this);
                yield break;
            }
            // Герой и копии берут кадры префаба, а направление задаёт игра и запись.
            officerWalk.enabled = false;
            heroBaseScale = hero.transform.localScale;

            yield return LoadRoomLayout<PoliceStationLayout>(PoliceStationLayout.ScenePath,
                (layout, roots) => { station = layout; stationRoots = roots; });
            yield return LoadRoomLayout<Room01Layout>(LaboratoryRoom.ScenePath,
                (layout, roots) => { room01 = layout; laboratoryRoots = roots; });
            if (!station || !station.entrance || !station.phone || !station.exit || !station.exitLabel ||
                !station.furniture || !station.phoneSprite)
            {
                Debug.LogError("В сцене PoliceStation заполните ссылки на объекты в PoliceStationLayout.", this);
                yield break;
            }
            if (!room01 || !room01.TryGetComponent(out introductionRoom) || !room01.remote || !room01.furniture ||
                !room01.door || !room01.plateOverlay || !room01.remoteSprite || !room01.door.Passage)
            {
                Debug.LogError("В сцене Laboratory заполните ссылки на объекты в Room01Layout.", this);
                yield break;
            }
            foreach (var root in laboratoryRoots)
            foreach (var room in root.GetComponentsInChildren<LaboratoryRoom>(true))
            {
                if (room.TryGetComponent<TimedButtonsRoom>(out var buttons))
                {
                    if (!buttons.IsConfigured)
                    {
                        Debug.LogError("Проверьте кнопки, табло и дверь комнаты.", room);
                        yield break;
                    }
                    buttons.SignalShown += PlayButtonSignal;
                    buttons.PressAccepted += PlayCorrectButton;
                }
                if (room.TryGetComponent<TargetRangeRoom>(out var range) && !range.IsConfigured)
                {
                    Debug.LogError("Назначьте мишени, индикаторы, стену, дверь и спрайты пистолета тира.", room);
                    yield break;
                }
                if (range)
                {
                    pistolArt = range;
                    range.ShotFired += PlayShot;
                    range.TargetsRising += PlayTargetRise;
                }
                if (room.TryGetComponent<FinaleRoom>(out var finale) && !finale.IsConfigured)
                {
                    Debug.LogError("Настройте участников, укрытия и портал финала.", room);
                    yield break;
                }
                if (finale)
                {
                    finale.ShotFired += PlayShot;
                    finale.OfficerShotFired += (shot, copy) => pistolArt?.AnimateShot(shot, copy);
                }
                if (room.TryGetComponent<SequenceRoomLayout>(out var puzzle))
                {
                    if (!puzzle.IsConfigured)
                    {
                        Debug.LogError("Проверьте ссылки плит и дверей комнаты.", puzzle);
                        yield break;
                    }
                    sequences.Add((room.roomNumber, puzzle));
                }
                if (!room.entrance || room.roomNumber < 1 || room.roomNumber > EchoHistory.MaxRoomNumber ||
                    rooms.ContainsKey(room.roomNumber))
                {
                    Debug.LogError("Задайте точку входа и уникальный номер комнаты от 1 до 5.", room);
                    yield break;
                }
                rooms.Add(room.roomNumber, room);
                if (!room.cameraArea && (room.cameraSize.x <= 0 || room.cameraSize.y <= 0))
                {
                    Debug.LogError("Назначьте область камеры или укажите положительные размеры кадра.", room);
                    yield break;
                }
            }

            Application.targetFrameRate = 60;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            ghost = new GameObject("Echo", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            ghost.transform.SetParent(transform, false);
            ghost.sharedMaterial = hero.sharedMaterial;
            ghost.color = new Color(.3f, .85f, 1, .55f);
            ghost.enabled = false;
            InitializeAudio();
            UI();
            history = EchoHistory.Load() ?? new EchoHistory();
            BeginCycle();
            ShowMenu();
        }

        static IEnumerator LoadRoomLayout<T>(string path, Action<T, GameObject[]> assign) where T : MonoBehaviour
        {
            var scene = SceneManager.GetSceneByPath(path);
            int buildIndex = SceneUtility.GetBuildIndexByScenePath(path);
            if (!scene.isLoaded && buildIndex >= 0)
            {
                yield return SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Additive);
                scene = SceneManager.GetSceneByPath(path);
            }
            if (!scene.isLoaded) yield break;
            var roots = scene.GetRootGameObjects();
            foreach (var root in roots)
            {
                var layout = root.GetComponentInChildren<T>(includeInactive: true);
                if (!layout) continue;
                // Сохраняем авторскую видимость остальных корней сцены.
                var roomRoots = Array.FindAll(roots, item => item == root || item.activeSelf);
                SetRoomVisible(roomRoots, false);
                assign(layout, roomRoots);
                yield break;
            }
        }

        static void SetRoomVisible(GameObject[] roots, bool visible)
        {
            foreach (var root in roots) root.SetActive(visible);
        }

        public void BeginCycle(LaboratoryRoom resumeRoom = null)
        {
            menuMusicAudio.Stop();
            replayFinale = false;
            facing = resumeRoom ? 0 : 3; phoneRead = false; mode = "play";
            titleScreen.SetActive(false);
            panel.gameObject.SetActive(false);
            ActivateRoom(resumeRoom, returning: resumeRoom != null);
        }
        void ResetRoomRecording()
        {
            elapsed = 0; nextSample = .05f; recording = new EchoTape { room = zone }; recordingActive = true;
            Record();
        }
        public void RestartCycle()
        {
            if (!currentRoom || !history.remoteObtained) return;
            ReturnToRoom();
        }

        void Save()
        {
            try { history.Save(saveFileOverride); caution.text = ""; }
            catch (Exception ex) { caution.text = "Не удалось сохранить прогресс. Продолжение после закрытия может быть недоступно."; Debug.LogWarning(ex.Message); }
        }
        void Update()
        {
            if (!canvas || mode == "quitting") return;
            if (stationFade.gameObject.activeSelf)
            {
                ResetOfficerMovement();
                AdvanceStationFade(Time.unscaledDeltaTime);
                return;
            }
            if (room2PreviewDoor)
            {
                ResetOfficerMovement();
                AdvanceRoom2DoorPreview(Mathf.Min(Time.unscaledDeltaTime, .05f));
                return;
            }
            var k = Keyboard.current;
            if (mode != "play" || k == null) ResetOfficerMovement();
            bool enter = k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame);
            bool advance = enter || (k != null && k.eKey.wasPressedThisFrame);
            if (mode == "pistol-help")
            {
                if (advance)
                {
                    rangeGuideShown = true;
                    HidePistolHelp();
                }
                return;
            }
            if (k == null) return;
            if (mode == "menu" || mode == "confirm")
            {
                UpdateTitleMenu(k, enter);
                return;
            }
            if (mode == "pause")
            {
                int direction = 0;
                if (k.downArrowKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame) direction++;
                if (k.upArrowKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame) direction--;
                if (direction != 0)
                    FocusPauseOption((pauseSelection + direction + PauseOptions.Length) % PauseOptions.Length);
                if (enter) UsePauseOption(pauseSelection);
                else if (k.escapeKey.wasPressedThisFrame) Close();
                return;
            }
            if (mode == "dialog")
            {
                if (enter || k.eKey.wasPressedThisFrame || k.escapeKey.wasPressedThisFrame)
                    Close();
                return;
            }
            if (mode == "done")
            {
                if (enter) ShowMenu();
                else if (currentFinale && k.rKey.wasPressedThisFrame)
                {
                    replayFinale = true;
                    ReturnToRoom();
                }
                return;
            }
            if (mode == "finale-failed")
            {
                currentFinale.AdvanceFailureVisual(Time.unscaledDeltaTime);
                if (officerDying)
                {
                    officerDeathTime += Time.unscaledDeltaTime;
                    hero.sprite = officerWalk.GetDeathFrame(facing, officerDeathTime);
                    if (officerDeathTime >= CharacterWalk.DeathAnimationDuration)
                    {
                        officerDying = false;
                        ShowFinaleDefeat();
                    }
                }
                if (k.rKey.wasPressedThisFrame)
                    ReturnToRoom();
                else if (!illustratedCard.isActiveAndEnabled)
                {
                    finaleRetryDelay -= Time.unscaledDeltaTime;
                    if (finaleRetryDelay <= 0) ReturnToRoom();
                }
                return;
            }
            if (k.escapeKey.wasPressedThisFrame)
            {
                mode = "pause";
                shotAudio.Stop();
                FocusPauseOption(0, playSound: false);
                pauseMenu.SetAsLastSibling();
                pauseMenu.gameObject.SetActive(true);
                return;
            }
            if (k.rKey.wasPressedThisFrame) { RestartCycle(); return; }
            float dt = Mathf.Min(Time.deltaTime, .05f); if (recordingActive) elapsed += dt;
            AdvanceDepartedEchoes(dt);
            UpdateGhosts();
            if (room2PreviewDoor) return;
            Vector2 move = new Vector2((k.dKey.isPressed || k.rightArrowKey.isPressed ? 1 : 0) - (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1 : 0),
                (k.wKey.isPressed || k.upArrowKey.isPressed ? 1 : 0) - (k.sKey.isPressed || k.downArrowKey.isPressed ? 1 : 0));
            bool toggledPistol = CanDrawPistol && k.qKey.wasPressedThisFrame;
            if (toggledPistol) pistolDrawn = !pistolDrawn;
            Vector2 previousPosition = position;
            int previousFacing = facing;
            MoveOfficer(move, k, dt);
            bool fire = pistolDrawn && (k.spaceKey.wasPressedThisFrame || (Mouse.current?.leftButton.wasPressedThisFrame ?? false));
            officerMoving = position != previousPosition;
            hero.sprite = officerWalk.GetFrame(facing, officerMoving, Time.time);
            Place(hero, position);
            if (fire && currentRange && recordingActive)
            {
                Vector2 origin = position + Vector2.up * TargetRangeRoom.MuzzleHeight;
                currentRange.Shoot(origin, ShotDirection, elapsed, recording, OfficerMuzzle() - origin);
            }
            UpdateGhosts();
            var crossing = FindCrossing(previousPosition);
            if (crossing != null) { UsePassage(crossing); return; }
            if (recordingActive && (toggledPistol || facing != previousFacing || elapsed >= nextSample))
            { Record(); nextSample = elapsed + .05f; }
            if (currentFinale && UpdateFinale(dt, fire, k.eKey.wasPressedThisFrame)) return;
            var target = Nearby();
            string hint = InteractionHint(target);
            prompt.text = string.IsNullOrEmpty(hint) ? string.Empty : "[ E ]  " + hint;
            if (!room2PreviewDoor && k.eKey.wasPressedThisFrame && target != Interaction.None) Interact(target);
        }
        public void Record()
        {
            if (recordingActive && zone > 0)
                recording.frames.Add(new EchoFrame(elapsed, position, zone, facing, ShotDirection, pistolDrawn));
        }
        void Place(SpriteRenderer sprite, Vector2 p)
        {
            sprite.transform.position = new Vector3(Mathf.Round(p.x * 16) / 16, Mathf.Round(p.y * 16) / 16, 0);
            sprite.sortingOrder = 100 - Mathf.RoundToInt(p.y * 10);
        }
        public void UpdateGhosts() => UpdateGhosts(false);

        void UpdateGhosts(bool immediateDoor)
        {
            var actor = new EchoFrame(0, position, zone, facing);
            bool hasPlateA = currentRoom && (currentRoom == introductionRoom || currentSequence);
            plateA = hasPlateA && EchoHistory.OnPlate(actor, CurrentPlateA);
            plateB = currentSequence &&
                EchoHistory.OnPlate(actor, currentSequence.second.plate.transform.position);
            bool previewDoor = zone == 2 && currentSequence && plateB && autoFitRoom &&
                !history.room2DoorPreviewSeen && !room2PreviewDoor && !immediateDoor;
            bool showOpening = previewDoor && !currentSequence.second.door.Passage.isTrigger;
            var tape = ActiveTape;
            ghost.enabled = false;
            Vector2 copyDirection = Vector2.up;
            bool copyMoving = false;
            bool copyPistolDrawn = false;
            if (recordingActive && (!currentFinale || !currentFinale.CopyHit) &&
                tape != null && tape.Sample(elapsed, out var frame) && frame.zone == zone)
            {
                ghost.enabled = true;
                copyPistolDrawn = frame.pistolDrawn;
                copyMoving = DrawEcho(ghost, tape, frame, elapsed);
                copyDirection = OfficerDirection(frame.facing);
                if (hasPlateA) plateA |= EchoHistory.OnPlate(frame, CurrentPlateA);
                if (currentSequence) plateB |= EchoHistory.OnPlate(frame, currentSequence.second.plate.transform.position);
            }
            if (!currentRoom) return;
            UpdateIntroductionVisuals(immediateDoor);
            var feet = new Bounds(FeetCenter(position), FeetSize);
            if (currentButtons)
            {
                if (recordingActive && tape != null) currentButtons.Replay(tape, elapsed);
                currentButtons.Show(elapsed);
                currentButtons.exitDoor.SetPassage(currentButtons.Solved, feet, immediateDoor);
            }
            if (currentRange)
            {
                if (recordingActive) currentRange.Replay(tape, elapsed);
                currentRange.Show(elapsed);
                currentRange.ShowPistols(hero, ShotDirection, officerMoving, pistolDrawn,
                    ghost, copyDirection, copyMoving, copyPistolDrawn, elapsed);
                currentRange.exitDoor.SetPassage(currentRange.Solved, feet, immediateDoor);
            }
            else if (CanDrawPistol)
                pistolArt.ShowPistols(hero, ShotDirection, officerMoving, pistolDrawn,
                    ghost, copyDirection, copyMoving, copyPistolDrawn, elapsed);
            foreach (var (room, sequence) in sequences)
            {
                if (!sequence.isActiveAndEnabled) continue;
                bool active = sequence == currentSequence;
                sequence.Apply(active ? plateA : DepartedEchoOnPlate(room, sequence.first.plate.transform.position),
                    active ? plateB : DepartedEchoOnPlate(room, sequence.second.plate.transform.position), feet, immediateDoor);
            }
            if (previewDoor)
            {
                var door = currentSequence.second.door;
                // Откладываем движение створок до момента, когда дверь окажется в кадре.
                if (showOpening) door.SetOpen(false);
                BeginRoom2DoorPreview(door);
            }
        }

        static Vector2 FeetCenter(Vector2 p) => p + new Vector2(0, .04f);

        bool IsMovementObstacle(Collider2D obstacle) =>
            obstacle.gameObject.scene == (zone == 0 ? station.gameObject.scene : room01.gameObject.scene);

        bool CanStandAt(Vector2 destination)
        {
            Physics2D.OverlapBox(FeetCenter(destination), FeetSize, 0, SolidContacts, overlapHits);
            foreach (var obstacle in overlapHits)
                if (IsMovementObstacle(obstacle)) return false;
            return true;
        }

        bool CanMove(Vector2 destination)
        {
            if (!CanStandAt(destination)) return false;
            Vector2 step = destination - position;
            if (step == Vector2.zero) return true;
            Physics2D.BoxCast(FeetCenter(position), FeetSize, 0, step.normalized, SolidContacts, movementHits, step.magnitude);
            foreach (var hit in movementHits)
                if (hit.distance > 0 && IsMovementObstacle(hit.collider)) return false;
            return true;
        }

        Interaction Nearby()
        {
            if (zone == 0) return StationNearby();
            var local = currentRoom == introductionRoom ? IntroNearby() : Interaction.None;
            if (local != Interaction.None) return local;
            if (currentButtons && currentButtons.NearbyButton(position) > 0) return Interaction.PressTimedButton;
            return FindPassage() != null ? Interaction.UseRoomPassage : Interaction.None;
        }

        string InteractionHint(Interaction target) => target switch
        {
            Interaction.AnswerPhone => "Телефон дежурного",
            Interaction.PickUpRemote => "Взять пульт",
            Interaction.PressTimedButton when recordingActive && !currentButtons.Solved =>
                currentButtons.AwaitingStart && currentButtons.NearbyButton(position) == 1
                    ? "Запустить последовательность" : "Нажать кнопку",
            Interaction.ReadNoticeBoard => "Прочитать заметки",
            Interaction.UseRoomPassage => FindPassage()?.label,
            _ => string.Empty
        };

        public void Interact(Interaction target)
        {
            if (stationFade.gameObject.activeSelf) return;
            switch (target)
            {
                case Interaction.ReadNoticeBoard: ReadNoticeBoard(); break;
                case Interaction.PickUpRemote: UseIntroRemote(); break;
                case Interaction.PressTimedButton:
                    if (currentButtons && recordingActive)
                    {
                        currentButtons.Press(currentButtons.NearbyButton(position), elapsed, recording);
                        UpdateGhosts();
                    }
                    break;
                case Interaction.UseRoomPassage:
                    var passage = FindPassage();
                    if (passage != null) UsePassage(passage);
                    break;
                case Interaction.AnswerPhone: AnswerPhone(); break;
                case Interaction.EnterLaboratory:
                    if (!phoneRead) { Dialog("ДЕЖУРНЫЙ", "Сначала ответьте на телефон. Поступил вызов."); break; }
                    stationFadeTime = 0;
                    stationFade.color = Color.clear;
                    stationFade.transform.SetAsLastSibling();
                    stationFade.gameObject.SetActive(true);
                    break;
            }
        }

        void AdvanceStationFade(float deltaTime)
        {
            stationFadeTime += deltaTime;
            float progress = Mathf.Clamp01(stationFadeTime / StationFadeDuration);
            bool leavingStation = zone == 0;
            float opacity = Mathf.SmoothStep(0, 1, progress);
            stationFade.color = new Color(0, 0, 0, leavingStation ? opacity : 1 - opacity);
            if (progress < 1) return;

            if (leavingStation)
            {
                if (history.returnRoom == 0)
                {
                    history.returnRoom = introductionRoom.roomNumber;
                    Save();
                }
                ActivateRoom(introductionRoom);
                stationFadeTime = 0;
            }
            else
            {
                stationFade.gameObject.SetActive(false);
            }
        }
        void ActivateRoom(LaboratoryRoom targetRoom, Transform arrival = null, bool keepPosition = false, bool returning = false)
        {
            prompt.text = string.Empty;
            officerDying = false;
            officerDeathTime = 0;
            HidePistolHelp();
            illustratedCard.gameObject.SetActive(false);
            shotAudio.Stop();
            buttonSignalAudio.Stop();
            correctButtonAudio.Stop();
            targetRiseAudio.Stop();
            room2PreviewDoor = null;
            bool enteringLaboratory = targetRoom && !currentRoom;
            bool continuingForward = currentRoom && targetRoom && keepPosition && !returning && targetRoom.roomNumber > zone;
            if (continuingForward) KeepDepartedEcho();
            else
                for (int room = 1; room < departedEchoes.Length; room++) ClearDepartedEcho(room);
            if (currentSequence) currentSequence.UpdateCabinetPlant(null);
            if (currentFinale) currentFinale.End();
            if (currentRange)
            {
                currentRange.Hide();
                currentRange.exitDoor.SetPassage(history.returnRoom > zone, new Bounds(FeetCenter(position), FeetSize), true);
            }
            else if (pistolArt) pistolArt.HidePistols();
            currentRoom = targetRoom;
            currentSequence = targetRoom ? targetRoom.GetComponent<SequenceRoomLayout>() : null;
            currentButtons = targetRoom ? targetRoom.GetComponent<TimedButtonsRoom>() : null;
            currentRange = targetRoom ? targetRoom.GetComponent<TargetRangeRoom>() : null;
            currentFinale = targetRoom ? targetRoom.GetComponent<FinaleRoom>() : null;
            if (currentFinale) currentFinale.officerBody = officerWalk;
            zone = targetRoom ? targetRoom.roomNumber : 0;
            if (!keepPosition || !CanDrawPistol)
                pistolDrawn = returning && CanDrawPistol;
            bool inStation = !targetRoom;
            if (inStation) gameMusicAudio.Stop();
            else if (enteringLaboratory && gameMusicAudio.clip) gameMusicAudio.Play();
            SetRoomVisible(inStation ? laboratoryRoots : stationRoots, false);
            SetRoomVisible(inStation ? stationRoots : laboratoryRoots, true);
            UpdateFurnitureSorting(inStation ? station.furniture : room01.furniture,
                inStation ? station.phoneSprite : room01.remoteSprite);
            if (!keepPosition)
            {
                position = arrival ? arrival.position : (inStation ? station.entrance.position : targetRoom.entrance.position);
                ResetOfficerMovement();
            }
            if (inStation || targetRoom.disableRecording)
            {
                elapsed = 0;
                nextSample = 0;
                recording = new EchoTape();
                recordingActive = false;
            }
            else ResetRoomRecording();
            if (currentButtons) currentButtons.ResetAttempt(history.returnRoom > zone);
            if (currentRange) currentRange.ResetAttempt(history.returnRoom > zone);
            if (currentFinale)
            {
                if (pistolArt) pistolArt.exitDoor.SetPassage(true, new Bounds(FeetCenter(position), FeetSize), true);
                currentFinale.Begin(position);
            }

            if (enteringLaboratory) CacheLaboratoryCameraBounds();
            FitRoomCamera(snap: enteringLaboratory);
            UpdateStationExitLabel();
            RefreshActors(immediateDoor: !continuingForward);
            if (currentRange && !returning && !rangeGuideShown) ShowPistolHelp();
            if (currentFinale && history.completed && !replayFinale)
            {
                currentFinale.RestoreCompletion();
                FinishFinale();
            }
        }

        bool UpdateFinale(float dt, bool fire, bool interact)
        {
            var tape = ActiveTape;
            currentFinale.Replay(tape, elapsed);
            Vector2? copy = tape != null && !currentFinale.CopyHit && tape.Sample(elapsed, out var frame)
                ? new Vector2(frame.x, frame.y) : null;
            currentFinale.Tick(dt, elapsed, position, copy);
            if (fire && recordingActive)
            {
                Vector2 origin = position + Vector2.up * TargetRangeRoom.MuzzleHeight;
                currentFinale.Shoot(position, ShotDirection, elapsed, recording, OfficerMuzzle() - origin);
            }
            UpdateGhosts();
            if (currentFinale.Failure != null)
            {
                officerDying = currentFinale.Failure == "Иван погиб." && officerWalk.HasDeathAnimation;
                officerDeathTime = 0;
                if (officerDying)
                {
                    pistolArt.HidePistols();
                    hero.sprite = officerWalk.GetDeathFrame(facing, 0);
                }
                finaleRetryDelay = 2f;
                mode = "finale-failed";
                if (officerDying) panel.gameObject.SetActive(false);
                else ShowFinaleDefeat();
                return true;
            }
            int hostage = currentFinale.NearbyCaptive(position);
            if (hostage < 0) return false;
            prompt.text = hostage == 0 ? "[ E ] Освободить Фёдора" : "[ E ] Освободить Алексея";
            if (interact)
            {
                currentFinale.Release(hostage);
                if (currentFinale.Complete) FinishFinale();
            }
            return true;
        }

        void FinishFinale()
        {
            prompt.text = string.Empty;
            if (!history.completed)
            {
                history.completed = true;
                Save();
            }
            recordingActive = false;
            recording = new EchoTape();
            UpdateGhosts();
            mode = "done";
            if (victoryArt) ShowIllustratedCard(victoryArt);
            else Panel("ЗАЛОЖНИКИ СПАСЕНЫ", "Фёдор и Алексей живы.\n\nENTER — главное меню    R — повторить финал");
        }

        void UpdateFurnitureSorting(Transform group, SpriteRenderer interactiveSprite)
        {
            foreach (var box in group.GetComponentsInChildren<BoxCollider2D>())
            {
                var bounds = box.bounds;
                var sprite = box.GetComponentInChildren<SpriteRenderer>();
                if (sprite) sprite.sortingOrder = 100 - Mathf.RoundToInt(bounds.min.y * 10);
            }
            var desk = interactiveSprite.GetComponentInParent<BoxCollider2D>();
            if (desk && desk.TryGetComponent<SpriteRenderer>(out var deskSprite))
                interactiveSprite.sortingOrder = deskSprite.sortingOrder + 1;
        }

        void RefreshActors(bool immediateDoor)
        {
            officerMoving = false;
            hero.sprite = officerWalk.GetFrame(facing, false, 0);
            hero.transform.localScale = heroBaseScale * (zone > 0 ? .65f : 1f);
            Place(hero, position);
            ghost.transform.localScale = hero.transform.localScale;
            UpdateGhosts(immediateDoor);
        }
        Text Text(string name, Transform parent, Vector2 size, Vector2 p, int fontSize, Color color, TextAnchor align = TextAnchor.UpperLeft)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f; rect.sizeDelta = size; rect.anchoredPosition = p;
            var text = go.AddComponent<Text>(); text.font = font; text.fontSize = fontSize; text.color = color; text.alignment = align; text.supportRichText = false; text.raycastTarget = false; return text;
        }
        void UI()
        {
            var go = new GameObject("EchoUI", typeof(Canvas), typeof(CanvasScaler)); canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            SceneManager.MoveGameObjectToScene(go, gameObject.scene);
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
            stationExitLabel = Text("WorldLabel", go.transform, new Vector2(340, 34), Vector2.zero, 20, Color.white, TextAnchor.MiddleCenter);
            stationExitLabel.text = "[E] Отправиться в лабораторию";
            stationExitLabel.enabled = false;
            prompt = Text("Prompt", go.transform, new Vector2(1220, 42), new Vector2(0, -292), 20, Color.white, TextAnchor.MiddleCenter);
            returnHint = Text("ReturnHint", go.transform, new Vector2(1220, 28), Vector2.zero, 20, Color.white, TextAnchor.MiddleCenter);
            returnHint.text = "[R] Вернуться к точке пульта";
            returnHint.enabled = false;
            caution = Text("SaveStatus", go.transform, new Vector2(1180, 30), new Vector2(0, -250), 15, amber, TextAnchor.MiddleCenter);
            var panelGo = new GameObject("Dialog", typeof(RectTransform), typeof(UnityEngine.UI.Image)); panelGo.transform.SetParent(go.transform, false); panel = panelGo.GetComponent<RectTransform>(); panel.sizeDelta = new Vector2(920, 490); panelGo.GetComponent<UnityEngine.UI.Image>().color = new Color(.035f, .065f, .085f, .99f);
            dialogTitle = Text("Title", panel, new Vector2(820, 55), new Vector2(0, 180), 27, amber);
            dialogBody = Text("Body", panel, new Vector2(820, 340), new Vector2(0, TitledDialogBodyY), DialogFontSize, new Color(.86f, .9f, .89f));
            CreatePauseMenu();
            ArrangeOverlayHUD();
            CreatePistolHelp();
            CreateIllustratedCard();
            CreateTitleMenu();
            var fadeObject = new GameObject("StationFade", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            stationFade = fadeObject.GetComponent<UnityEngine.UI.Image>();
            var fadeRect = stationFade.rectTransform;
            fadeRect.SetParent(canvas.transform, false);
            fadeRect.anchorMin = Vector2.zero;
            fadeRect.anchorMax = Vector2.one;
            fadeRect.offsetMin = fadeRect.offsetMax = Vector2.zero;
            stationFade.raycastTarget = false;
            fadeObject.SetActive(false);
        }

        void CreateIllustratedCard()
        {
            approachDefeatArt = Resources.Load<Texture2D>("Menu/defeat-01-approach");
            accomplicesDefeatArt = Resources.Load<Texture2D>("Menu/defeat-02-accomplices");
            ivanDeathArt = Resources.Load<Texture2D>("Menu/defeat-03-ivan");
            victoryArt = Resources.Load<Texture2D>("Menu/victory");
            remoteHelpArt = Resources.Load<Texture2D>("Menu/RemoteHelp");
            phoneCallArt = Resources.Load<Texture2D>("Menu/PhoneCall");
            var card = MenuRegion("IllustratedCard", canvas.transform, new Vector2(.05f, .05f), new Vector2(.95f, .95f));
            illustratedCard = card.gameObject.AddComponent<RawImage>();
            illustratedCard.raycastTarget = false;
            FitMenuGraphic(card, 4f / 3f);
            card.gameObject.SetActive(false);
        }

        void ShowFinaleDefeat()
        {
            mode = "finale-failed";
            var art = currentFinale.FailureIllustration switch
            {
                FinaleRoom.DefeatIllustration.Approach => approachDefeatArt,
                FinaleRoom.DefeatIllustration.Accomplices => accomplicesDefeatArt,
                FinaleRoom.DefeatIllustration.IvanDeath => ivanDeathArt,
                _ => null
            };
            if (!art)
            {
                finaleRetryDelay = 2f;
                Panel("ПОПЫТКА ПРОВАЛЕНА", currentFinale.Failure + "\n\nВозврат ко входу…    R — сразу");
                return;
            }

            ShowIllustratedCard(art);
        }

        void ShowIllustratedCard(Texture2D art, float scale = 1f)
        {
            panel.gameObject.SetActive(false);
            illustratedCard.texture = art;
            illustratedCard.rectTransform.localScale = new Vector3(scale, scale, 1f);
            illustratedCard.GetComponent<AspectRatioFitter>().aspectRatio = (float)art.width / art.height;
            illustratedCard.transform.parent.SetAsLastSibling();
            illustratedCard.gameObject.SetActive(true);
        }
        void Panel(string title, string body)
        {
            if (mode == "dialog")
            {
                shotAudio.Stop();
            }
            panel.gameObject.SetActive(true);
            panel.SetAsLastSibling();
            dialogTitle.text = title;
            dialogBody.text = body;
            bool hasTitle = !string.IsNullOrEmpty(title);
            dialogBody.rectTransform.anchoredPosition = new Vector2(0, hasTitle ? TitledDialogBodyY : 0f);
            dialogBody.fontSize = hasTitle ? DialogFontSize : DialogFontSize + 1;
        }
        void Dialog(string title, string body)
        {
            prompt.text = string.Empty;
            mode = "dialog";
            Panel(title, body + "\n\nE / ENTER — продолжить");
        }
        void Close()
        {
            mode = "play";
            panel.gameObject.SetActive(false);
            pauseMenu.gameObject.SetActive(false);
            illustratedCard.gameObject.SetActive(false);
        }

        void CreatePauseMenu()
        {
            pauseMenu = MenuRegion("PauseMenu", canvas.transform, Vector2.zero, Vector2.one);
            var backdrop = pauseMenu.gameObject.AddComponent<UnityEngine.UI.Image>();
            backdrop.color = new Color32(10, 9, 10, 211);

            var composition = MenuRegion("PauseComposition", pauseMenu, Vector2.zero, Vector2.one);
            FitMenuGraphic(composition, PauseArtworkSize.x / PauseArtworkSize.y);
            var atlas = Resources.Load<Texture2D>("Menu/PauseMenu");
            if (atlas)
            {
                const float headingHeight = 330;
                var headingArt = MenuRegion("HeadingArt", composition,
                    new Vector2(0, 1 - headingHeight / PauseArtworkSize.y), Vector2.one).gameObject.AddComponent<RawImage>();
                headingArt.texture = atlas;
                headingArt.uvRect = PauseArtworkUv(atlas, 0, 0, PauseArtworkSize.x, headingHeight);
                headingArt.raycastTarget = false;
            }
            else
            {
                MenuLabel("Heading", composition, new Vector2(.05f, .77f), new Vector2(.44f, .87f),
                    font, 52, TitlePaper, "ПАУЗА").alignment = TextAnchor.MiddleLeft;
                MenuLabel("Status", composition, new Vector2(.05f, .64f), new Vector2(.49f, .75f),
                    font, 26, TitlePaper, "Запись и копии приостановлены.").alignment = TextAnchor.MiddleLeft;
            }

            for (int i = 0; i < PauseOptions.Length; i++)
            {
                float top = 1 - PauseOptionTop[i] / PauseArtworkSize.y;
                var row = MenuRegion("Option" + (i + 1), composition,
                    new Vector2(PauseRowLeft / PauseArtworkSize.x, top - PauseRowHeight / PauseArtworkSize.y),
                    new Vector2((PauseRowLeft + PauseRowWidth) / PauseArtworkSize.x, top));
                var graphic = row.gameObject.AddComponent<RawImage>();
                graphic.texture = atlas;
                if (!atlas)
                    MenuLabel("Label", row, Vector2.zero, Vector2.one, font, 32, TitlePaper, PauseOptions[i]);
                pauseRows[i] = graphic;

                var button = row.gameObject.AddComponent<UnityEngine.UI.Button>();
                button.targetGraphic = graphic;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                button.transition = Selectable.Transition.None;
                int index = i;
                button.onClick.AddListener(() => UsePauseOption(index));
                AddMenuHover(button, () =>
                {
                    if (mode == "pause") FocusPauseOption(index);
                });
            }
            pauseMenu.gameObject.SetActive(false);
        }

        static Rect PauseArtworkUv(Texture2D atlas, float x, float y, float width, float height)
        {
            return new Rect(x / atlas.width, 1 - (y + height) / atlas.height, width / atlas.width, height / atlas.height);
        }

        void FocusPauseOption(int index, bool playSound = true)
        {
            if (playSound && pauseSelection != index) PlayMenuSelect();
            pauseSelection = index;
            for (int i = 0; i < pauseRows.Length; i++)
            {
                bool selected = i == index;
                var row = pauseRows[i];
                if (row.texture is Texture2D atlas)
                {
                    // Под макетом в атласе лежат альтернативные состояния кнопок.
                    bool alternate = selected != (i == 0);
                    float x = alternate ? 0 : PauseRowLeft;
                    float y = alternate ? PauseArtworkSize.y + i * PauseRowHeight : PauseOptionTop[i];
                    row.uvRect = PauseArtworkUv(atlas, x, y, PauseRowWidth, PauseRowHeight);
                }
                else
                {
                    row.color = selected ? new Color32(83, 66, 43, 255) : new Color32(38, 48, 47, 255);
                }
            }
        }

        void UsePauseOption(int index)
        {
            if (mode != "pause") return;
            PlayMenuSelect();
            if (index == 0) Close();
            else if (index == 1) ShowMenu();
            else QuitGame();
        }
    }
}

