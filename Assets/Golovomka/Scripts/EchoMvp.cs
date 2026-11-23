using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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
            InspectRemote,
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
        Text heading, objective, status, prompt, dialogTitle, dialogBody, caution;
        Text stationExitLabel;
        static readonly string[] PauseOptions = { "Продолжить", "Вернуться к входу комнаты", "Главное меню", "Выход" };
        const int PauseReturnOption = 1;
        readonly Text[] pauseLabels = new Text[PauseOptions.Length];
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
        readonly List<SequenceRoomLayout> sequences = new List<SequenceRoomLayout>();
        SequenceRoomLayout currentSequence;
        TimedButtonsRoom currentButtons;
        TargetRangeRoom currentRange;
        FinaleRoom currentFinale;
        float finaleRetryDelay;
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
                if (room.TryGetComponent<TimedButtonsRoom>(out var buttons) && !buttons.IsConfigured)
                {
                    Debug.LogError("Проверьте кнопки, табло и дверь комнаты.", room);
                    yield break;
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
                    sequences.Add(puzzle);
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

        public void BeginCycle()
        {
            replayFinale = false;
            facing = 3; phoneRead = false; mode = "play";
            titleScreen.SetActive(false);
            ActivateRoom(null);
            panel.gameObject.SetActive(false);
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
            if (!canvas) return;
            if (stationFade.gameObject.activeSelf)
            {
                AdvanceStationFade(Time.unscaledDeltaTime);
                return;
            }
            if (room2PreviewDoor)
            {
                AdvanceRoom2DoorPreview(Mathf.Min(Time.unscaledDeltaTime, .05f));
                return;
            }
            var k = Keyboard.current;
            bool enter = k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame);
            bool advance = enter || (k != null && k.eKey.wasPressedThisFrame);
            if (mode == "speech")
            {
                UpdateSpeech(Time.unscaledDeltaTime, advance);
                return;
            }
            if (mode == "pistol-help")
            {
                if (advance)
                {
                    rangeGuideShown = true;
                    HideSpeechUI();
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
                {
                    int index = pauseSelection;
                    do { index = (index + direction + PauseOptions.Length) % PauseOptions.Length; }
                    while (!pauseLabels[index].transform.parent.gameObject.activeSelf);
                    FocusPauseOption(index);
                }
                if (enter) UsePauseOption(pauseSelection);
                else if (k.escapeKey.wasPressedThisFrame) Close();
                return;
            }
            if (mode == "dialog") { if (enter || k.eKey.wasPressedThisFrame || k.escapeKey.wasPressedThisFrame) Close(); return; }
            if (mode == "done")
            {
                if (currentFinale && k.rKey.wasPressedThisFrame)
                {
                    replayFinale = true;
                    ReturnToRoom();
                }
                else if (enter) ShowMenu();
                return;
            }
            if (mode == "finale-failed")
            {
                finaleRetryDelay -= Time.unscaledDeltaTime;
                if (finaleRetryDelay <= 0 || k.rKey.wasPressedThisFrame) ReturnToRoom();
                return;
            }
            if (k.escapeKey.wasPressedThisFrame)
            {
                mode = "pause";
                LayoutPauseMenu();
                FocusPauseOption(0);
                Panel("ПАУЗА", "Запись и копии приостановлены.");
                return;
            }
            if (k.rKey.wasPressedThisFrame) { RestartCycle(); return; }
            float dt = Mathf.Min(Time.deltaTime, .05f); if (recordingActive) elapsed += dt;
            UpdateGhosts();
            if (room2PreviewDoor) return;
            Vector2 move = new Vector2((k.dKey.isPressed || k.rightArrowKey.isPressed ? 1 : 0) - (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1 : 0),
                (k.wKey.isPressed || k.upArrowKey.isPressed ? 1 : 0) - (k.sKey.isPressed || k.downArrowKey.isPressed ? 1 : 0));
            bool toggledPistol = CanDrawPistol && k.qKey.wasPressedThisFrame;
            if (toggledPistol) pistolDrawn = !pistolDrawn;
            Vector2 previousPosition = position;
            if (move != Vector2.zero)
            {
                UpdateOfficerFacing(move, k);
                var delta = move.normalized * moveSpeed * dt;
                Vector2 step = position + new Vector2(delta.x, 0); if (CanMove(step)) position = step;
                step = position + new Vector2(0, delta.y); if (CanMove(step)) position = step;
            }
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
            if (recordingActive && (toggledPistol || elapsed >= nextSample)) { Record(); nextSample = elapsed + .05f; }
            if (currentFinale && UpdateFinale(dt, fire, k.eKey.wasPressedThisFrame)) return;
            var target = Nearby();
            if (target == Interaction.InspectRemote && !remoteLineShown)
            {
                remoteLineShown = true;
                StartSpeech(RemoteLine);
                return;
            }
            prompt.text = target == Interaction.None ? (history.remoteObtained && currentRoom ? "WASD — идти    E — осмотр    R — вернуться к точке пульта    ESC — пауза" : "WASD / СТРЕЛКИ — идти    E — осмотр    ESC — пауза") : "[ E ]  " +
                (target == Interaction.UseRoomPassage ? FindPassage().label : InteractionLabel(target));
            if (CanDrawPistol) prompt.text += pistolDrawn ? "    Q — убрать пистолет" : "    Q — достать пистолет";
            if ((currentRange || currentFinale) && pistolDrawn) prompt.text += "    ЛКМ / ПРОБЕЛ — выстрел";
            if (!room2PreviewDoor && k.eKey.wasPressedThisFrame && target != Interaction.None) Interact(target);
            HUD();
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
                var copyPosition = new Vector2(frame.x, frame.y);
                copyMoving = tape.Sample(Mathf.Max(0, elapsed - .08f), out var previous)
                    && previous.zone == frame.zone
                    && (copyPosition - new Vector2(previous.x, previous.y)).sqrMagnitude > .0001f;
                copyDirection = OfficerDirection(frame.facing);
                Place(ghost, copyPosition);
                ghost.sprite = officerWalk.GetFrame(frame.facing, copyMoving, elapsed);
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
            foreach (var sequence in sequences)
            {
                if (!sequence.isActiveAndEnabled) continue;
                sequence.Apply(sequence == currentSequence && plateA, sequence == currentSequence && plateB,
                    feet, immediateDoor);
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

        static string InteractionLabel(Interaction target) => target switch
        {
            Interaction.AnswerPhone => "Телефон дежурного",
            Interaction.EnterLaboratory => "Отправиться в лабораторию",
            Interaction.InspectRemote => "Осмотреть пульт",
            Interaction.PickUpRemote => "Взять пульт",
            Interaction.PressTimedButton => "Нажать кнопку",
            Interaction.ReadNoticeBoard => "Прочитать заметки",
            _ => ""
        };

        public void Interact(Interaction target)
        {
            if (stationFade.gameObject.activeSelf) return;
            switch (target)
            {
                case Interaction.ReadNoticeBoard: ReadNoticeBoard(); break;
                case Interaction.InspectRemote:
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
                case Interaction.AnswerPhone: phoneRead = true; Dialog("АВТОМАТИЧЕСКИЙ ВЫЗОВ", "Лаборатория в подвале предприятия. Сработала система безопасности. Автоматический вызов полиции.\n\nИван узнаёт адрес: здесь работают его отец и брат. Выход из участка справа у дальней стены."); break;
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
                ActivateRoom(introductionRoom);
                prompt.text = "";
                stationFadeTime = 0;
            }
            else
            {
                stationFade.gameObject.SetActive(false);
            }
        }
        void HUD()
        {
            var tape = ActiveTape;
            heading.text = zone == 0 ? "ПОЛИЦЕЙСКИЙ УЧАСТОК" : "ПОДВАЛ / КОМНАТА " + zone;
            objective.text = zone == 0 ? "Ответить на вызов и отправиться к отцу"
                : currentRange ? currentRange.Objective(tape != null)
                : currentRoom != introductionRoom ? currentRoom.objective
                : history.remoteObtained ? "Оставьте копию на панели и пройдите дверь справа"
                : history.firstReturnSeen ? "Копия повторяет ваш путь. Заберите пульт у стола" : "Осмотрите комнату и пульт, появившийся из-под стола";
            status.text = "ЦИКЛ " + history.cycle.ToString("00") + "   •   " + (recordingActive ? "ЗАПИСЬ " + elapsed.ToString("0.0") + " с" : "ЗАПИСЬ НЕ АКТИВНА");
            if (currentRoom && !currentRoom.disableRecording) status.text += "   /   КОПИЙ: 1";
            if (currentRoom && tape != null)
                status.text += currentFinale && currentFinale.CopyHit ? "\nКОПИЯ ИСЧЕЗЛА" :
                    "\nКОПИЯ: " + Mathf.Max(0, tape.duration - elapsed).ToString("0.0") + " с";
            if (currentRoom && (currentRoom == introductionRoom || currentSequence))
                status.text += "\nПЛИТА А: " + (plateA ? "АКТИВНА" : "—");
            if (currentSequence) status.text += "   ПЛИТА Б: " + (plateB ? "АКТИВНА" : "—");
            if (currentButtons) status.text += "\n" + currentButtons.Status(elapsed);
            if (currentRange) status.text += "\n" + currentRange.Status(elapsed, tape != null);
            if (currentFinale)
            {
                objective.text = currentFinale.Cleared ? "Освободите Фёдора и Алексея" : currentRoom.objective;
                status.text += "\n" + currentFinale.Status(elapsed);
            }
        }

        void ActivateRoom(LaboratoryRoom targetRoom, Transform arrival = null, bool keepPosition = false, bool returning = false)
        {
            HideSpeechUI();
            shotAudio.Stop();
            room2PreviewDoor = null;
            bool enteringLaboratory = targetRoom && !currentRoom;
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
            zone = targetRoom ? targetRoom.roomNumber : 0;
            if (!keepPosition || !CanDrawPistol)
                pistolDrawn = returning && CanDrawPistol;
            bool inStation = !targetRoom;
            SetRoomVisible(inStation ? laboratoryRoots : stationRoots, false);
            SetRoomVisible(inStation ? stationRoots : laboratoryRoots, true);
            UpdateFurnitureSorting(inStation ? station.furniture : room01.furniture,
                inStation ? station.phoneSprite : room01.remoteSprite);
            if (!keepPosition)
                position = arrival ? arrival.position : (inStation ? station.entrance.position : targetRoom.entrance.position);
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
            if (currentFinale) currentFinale.Begin(position);

            stationExitLabel.enabled = inStation;
            if (enteringLaboratory) CacheLaboratoryCameraBounds();
            FitRoomCamera(snap: enteringLaboratory);
            PositionStationExitLabel();
            RefreshActors();
            if (currentRange && !returning && !rangeGuideShown) StartSpeech(RangeLine, showPistolHelp: true);
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
                finaleRetryDelay = 2f;
                mode = "finale-failed";
                Panel("ПОПЫТКА ПРОВАЛЕНА", currentFinale.Failure + "\n\nВозврат ко входу…    R — сразу");
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
            HUD();
            return true;
        }

        void FinishFinale()
        {
            if (!history.completed)
            {
                history.completed = true;
                Save();
            }
            recordingActive = false;
            recording = new EchoTape();
            UpdateGhosts();
            mode = "done";
            HUD();
            Panel("ЗАЛОЖНИКИ СПАСЕНЫ", "Фёдор и Алексей живы.\n\nENTER — главное меню    R — повторить финал");
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

        void RefreshActors()
        {
            officerMoving = false;
            hero.sprite = officerWalk.GetFrame(facing, false, 0);
            hero.transform.localScale = heroBaseScale * (zone > 0 ? .65f : 1f);
            Place(hero, position);
            ghost.transform.localScale = hero.transform.localScale;
            UpdateGhosts(immediateDoor: true);
            HUD();
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
            stationExitLabel = Text("WorldLabel", go.transform, new Vector2(280, 34), Vector2.zero, 17, amber, TextAnchor.MiddleCenter);
            stationExitLabel.text = "В ЛАБОРАТОРИЮ / E";
            heading = Text("Location", go.transform, new Vector2(700, 40), new Vector2(-230, 320), 25, new Color(.87f, .91f, .9f));
            objective = Text("Objective", go.transform, new Vector2(700, 38), new Vector2(-225, 260), 19, new Color(.58f, .73f, .73f));
            status = Text("Status", go.transform, new Vector2(520, 80), new Vector2(330, 300), 17, amber, TextAnchor.UpperRight);
            prompt = Text("Prompt", go.transform, new Vector2(1220, 42), new Vector2(0, -292), 20, Color.white, TextAnchor.MiddleCenter);
            caution = Text("SaveStatus", go.transform, new Vector2(1180, 30), new Vector2(0, -250), 15, amber, TextAnchor.MiddleCenter);
            Text("Credits", go.transform, new Vector2(1200, 30), new Vector2(0, -318), 14, new Color(.45f, .57f, .58f), TextAnchor.MiddleCenter).text = "GOLOVOMKA • MVP КОПИЙ   /   Графика создана с помощью AI • Некоммерческий прототип";
            var panelGo = new GameObject("Dialog", typeof(RectTransform), typeof(UnityEngine.UI.Image)); panelGo.transform.SetParent(go.transform, false); panel = panelGo.GetComponent<RectTransform>(); panel.sizeDelta = new Vector2(920, 490); panelGo.GetComponent<UnityEngine.UI.Image>().color = new Color(.035f, .065f, .085f, .99f);
            dialogTitle = Text("Title", panel, new Vector2(820, 55), new Vector2(0, 180), 27, amber);
            dialogBody = Text("Body", panel, new Vector2(820, 340), new Vector2(0, TitledDialogBodyY), DialogFontSize, new Color(.86f, .9f, .89f));
            CreatePauseMenu();
            ArrangeOverlayHUD();
            CreateSpeechUI();
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
        void Panel(string title, string body)
        {
            if (mode == "pause" || mode == "dialog") shotAudio.Stop();
            pauseMenu.gameObject.SetActive(mode == "pause");
            panel.gameObject.SetActive(true);
            panel.SetAsLastSibling();
            dialogTitle.text = title;
            dialogBody.text = body;
            bool hasTitle = !string.IsNullOrEmpty(title);
            dialogBody.rectTransform.anchoredPosition = new Vector2(0, hasTitle ? TitledDialogBodyY : 0f);
            dialogBody.fontSize = hasTitle ? DialogFontSize : DialogFontSize + 1;
        }
        void Dialog(string title, string body, string continueHint = "E / ENTER — продолжить")
        {
            mode = "dialog";
            Panel(title, body + "\n\n" + continueHint);
        }
        void Close() { mode = "play"; panel.gameObject.SetActive(false); }

        void CreatePauseMenu()
        {
            pauseMenu = MenuRegion("PauseMenu", panel, Vector2.zero, Vector2.one);
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = new Color(1f, 1f, 1f, .06f);
            colors.highlightedColor = new Color(1f, 1f, 1f, .16f);
            colors.pressedColor = new Color(1f, .74f, .36f, .25f);
            for (int i = 0; i < PauseOptions.Length; i++)
            {
                var row = new GameObject("Option" + (i + 1), typeof(RectTransform), typeof(UnityEngine.UI.Image));
                var rect = row.GetComponent<RectTransform>();
                rect.SetParent(pauseMenu, false);
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.sizeDelta = new Vector2(820, 40);
                var button = row.AddComponent<UnityEngine.UI.Button>();
                button.targetGraphic = row.GetComponent<UnityEngine.UI.Image>();
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                button.colors = colors;
                int index = i;
                button.onClick.AddListener(() => UsePauseOption(index));
                pauseLabels[i] = Text("Label", rect, new Vector2(794, 40), Vector2.zero, 23, Color.white, TextAnchor.MiddleLeft);
            }
            pauseMenu.gameObject.SetActive(false);
        }

        void LayoutPauseMenu()
        {
            int visibleRow = 0;
            for (int i = 0; i < pauseLabels.Length; i++)
            {
                var row = (RectTransform)pauseLabels[i].transform.parent;
                bool visible = i != PauseReturnOption || currentRoom != null;
                row.gameObject.SetActive(visible);
                if (visible) row.anchoredPosition = new Vector2(0, 12 - visibleRow++ * 46);
            }
        }

        void FocusPauseOption(int index)
        {
            pauseSelection = index;
            for (int i = 0; i < PauseOptions.Length; i++)
                pauseLabels[i].text = (i == index ? ">  " : "    ") + PauseOptions[i];
        }

        void UsePauseOption(int index)
        {
            if (mode != "pause") return;
            if (index == 0) Close();
            else if (index == PauseReturnOption) RestartCycle();
            else if (index == 2) ShowMenu();
            else QuitGame();
        }
    }
}

