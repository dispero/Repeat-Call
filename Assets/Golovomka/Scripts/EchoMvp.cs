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
            UseRoomPassage
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
        readonly SpriteRenderer[] ghosts = new SpriteRenderer[LaboratoryRoom.MaxCopies];
        bool GateOpen => history.clearedRooms >= introductionRoom.roomNumber || plateA;
        static readonly Vector2 FeetSize = new Vector2(.44f, .28f);
        static readonly ContactFilter2D SolidContacts = new ContactFilter2D { useTriggers = false };
        readonly List<Collider2D> overlapHits = new List<Collider2D>();
        readonly List<RaycastHit2D> movementHits = new List<RaycastHit2D>();
        readonly List<Sprite> createdSprites = new List<Sprite>();
        Texture2D walk;
        Sprite[] frames;
        Font font;
        Canvas canvas;
        RectTransform panel;
        Text heading, objective, status, prompt, dialogTitle, dialogBody, menu, caution;
        Text stationExitLabel;
        int selection;
        bool phoneRead;
        float nextSample;
        PoliceStationLayout station;
        Room01Layout room01;
        readonly List<SequenceRoomLayout> sequences = new List<SequenceRoomLayout>();
        SequenceRoomLayout currentSequence;
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
            var roomNumbers = new HashSet<int>();
            foreach (var root in laboratoryRoots)
            foreach (var room in root.GetComponentsInChildren<LaboratoryRoom>(true))
            {
                if (room.TryGetComponent<SequenceRoomLayout>(out var puzzle))
                {
                    if (!puzzle.IsConfigured || (puzzle.requireBothCopies && room.copyLimit != LaboratoryRoom.MaxCopies))
                    {
                        Debug.LogError("Проверьте ссылки плит, дверей и лимит копий комнаты.", puzzle);
                        yield break;
                    }
                    sequences.Add(puzzle);
                }
                if (!room.entrance || room.roomNumber < 1 || room.roomNumber > EchoHistory.MaxRoomNumber ||
                    !roomNumbers.Add(room.roomNumber))
                {
                    Debug.LogError("Задайте точку входа и уникальный номер комнаты от 1 до 4.", room);
                    yield break;
                }
                if (!room.cameraArea && (room.cameraSize.x <= 0 || room.cameraSize.y <= 0))
                {
                    Debug.LogError("Назначьте область камеры или укажите положительные размеры кадра.", room);
                    yield break;
                }
            }

            Application.targetFrameRate = 60;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            walk = Resources.Load<Texture2D>("Art/IvanOfficerWalk");
            frames = LoadOfficerFrames();
            for (int i = 0; i < ghosts.Length; i++)
            {
                var ghost = new GameObject("Echo" + i, typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                ghost.transform.SetParent(transform, false);
                ghost.sharedMaterial = hero.sharedMaterial;
                ghost.color = new Color(.3f, .85f, 1, .55f);
                ghost.enabled = false;
                ghosts[i] = ghost;
            }
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

        void OnDestroy() { foreach (var s in createdSprites) if (s) Destroy(s); }

        public void BeginCycle()
        {
            facing = 3; phoneRead = false; mode = "play";
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
            if (!currentRoom || currentRoom.disableRecording || !history.remoteObtained) return;
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
            var k = Keyboard.current; if (k == null) return;
            bool enter = k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame;
            if (mode == "menu" || mode == "pause" || mode == "confirm")
            {
                string[] choices = Choices();
                if (k.downArrowKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame) selection = (selection + 1) % choices.Length;
                if (k.upArrowKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame) selection = (selection + choices.Length - 1) % choices.Length;
                MenuText();
                if (enter) Select();
                else if (k.escapeKey.wasPressedThisFrame && mode == "pause") Close();
                return;
            }
            if (mode == "dialog") { if (enter || k.eKey.wasPressedThisFrame || k.escapeKey.wasPressedThisFrame) Close(); return; }
            if (mode == "done") { if (enter) ShowMenu(); return; }
            if (k.escapeKey.wasPressedThisFrame) { mode = "pause"; selection = 0; Panel("ПАУЗА", "Запись и копии приостановлены."); MenuText(); return; }
            if (k.rKey.wasPressedThisFrame) { RestartCycle(); return; }
            float dt = Mathf.Min(Time.deltaTime, .05f); if (recordingActive) elapsed += dt;
            UpdateGhosts();
            Vector2 move = new Vector2((k.dKey.isPressed || k.rightArrowKey.isPressed ? 1 : 0) - (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1 : 0),
                (k.wKey.isPressed || k.upArrowKey.isPressed ? 1 : 0) - (k.sKey.isPressed || k.downArrowKey.isPressed ? 1 : 0));
            if (move != Vector2.zero)
            {
                facing = Mathf.Abs(move.x) > Mathf.Abs(move.y) ? (move.x > 0 ? 0 : 1) : (move.y > 0 ? 2 : 3);
                var delta = move.normalized * moveSpeed * dt;
                Vector2 step = position + new Vector2(delta.x, 0); if (CanMove(step)) position = step;
                step = position + new Vector2(0, delta.y); if (CanMove(step)) position = step;
            }
            hero.sprite = OfficerFrame(facing, move != Vector2.zero, Time.time);
            Place(hero, position); UpdateGhosts();
            var crossing = FindPassage(automatic: true);
            if (crossing != null) { UsePassage(crossing); return; }
            if (recordingActive && elapsed >= nextSample) { Record(); nextSample = elapsed + .05f; }
            var target = Nearby();
            prompt.text = target == Interaction.None ? (history.remoteObtained && recordingActive ? "WASD — идти    E — осмотр    R — к входу комнаты    ESC — пауза" : "WASD / СТРЕЛКИ — идти    E — осмотр    ESC — пауза") : "[ E ]  " +
                (target == Interaction.UseRoomPassage ? FindPassage(automatic: false).label : InteractionLabel(target));
            if (k.eKey.wasPressedThisFrame && target != Interaction.None) Interact(target);
            HUD();
        }
        public void Record() { if (recordingActive && zone > 0) recording.frames.Add(new EchoFrame(elapsed, position, zone, facing)); }
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
            bool playerPressesPlates = !currentSequence || !currentSequence.requireBothCopies;
            plateA = playerPressesPlates && hasPlateA && EchoHistory.OnPlate(actor, CurrentPlateA);
            plateB = playerPressesPlates && currentSequence &&
                EchoHistory.OnPlate(actor, currentSequence.second.plate.transform.position);
            var tapes = ActiveTapes;
            for (int i = 0; i < ghosts.Length; i++)
            {
                var ghost = ghosts[i];
                ghost.enabled = false;
                if (!recordingActive || !currentRoom || i >= currentRoom.copyLimit || i >= tapes.Count) continue;
                var tape = tapes[tapes.Count - i - 1];
                if (!tape.Sample(elapsed, out var frame) || frame.zone != zone) continue;
                ghost.enabled = true;
                bool moving = tape.Sample(Mathf.Max(0, elapsed - .08f), out var previous)
                    && previous.zone == frame.zone
                    && new Vector2(frame.x - previous.x, frame.y - previous.y).sqrMagnitude > .0001f;
                Place(ghost, new Vector2(frame.x, frame.y));
                ghost.sprite = OfficerFrame(frame.facing, moving, elapsed);
                if (hasPlateA) plateA |= EchoHistory.OnPlate(frame, CurrentPlateA);
                if (currentSequence) plateB |= EchoHistory.OnPlate(frame, currentSequence.second.plate.transform.position);
            }
            if (currentRoom && currentRoom == introductionRoom) UpdateIntroductionVisuals(immediateDoor);
            if (currentSequence)
            {
                var feet = new Bounds(FeetCenter(position), FeetSize);
                bool cleared = history.clearedRooms >= zone;
                currentSequence.Apply(plateA, plateB, cleared, feet, immediateDoor);
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
            return FindPassage(automatic: false) != null ? Interaction.UseRoomPassage : Interaction.None;
        }

        static string InteractionLabel(Interaction target) => target switch
        {
            Interaction.AnswerPhone => "Телефон дежурного",
            Interaction.EnterLaboratory => "Отправиться в лабораторию",
            Interaction.InspectRemote => "Осмотреть пульт",
            Interaction.PickUpRemote => "Взять пульт",
            _ => ""
        };

        public void Interact(Interaction target)
        {
            switch (target)
            {
                case Interaction.InspectRemote:
                case Interaction.PickUpRemote: UseIntroRemote(); break;
                case Interaction.UseRoomPassage:
                    var passage = FindPassage(automatic: false);
                    if (passage != null) UsePassage(passage);
                    break;
                case Interaction.AnswerPhone: phoneRead = true; Dialog("АВТОМАТИЧЕСКИЙ ВЫЗОВ", "Лаборатория в подвале предприятия. Сработала система безопасности. Автоматический вызов полиции.\n\nИван узнаёт адрес: здесь работают его отец и брат. Выход из участка справа у дальней стены."); break;
                case Interaction.EnterLaboratory:
                    if (!phoneRead) { Dialog("ДЕЖУРНЫЙ", "Сначала ответьте на телефон. Поступил вызов."); break; }
                    ActivateRoom(introductionRoom);
                    break;
            }
        }
        void HUD()
        {
            var tapes = ActiveTapes;
            heading.text = zone == 0 ? "ПОЛИЦЕЙСКИЙ УЧАСТОК" : "ПОДВАЛ / КОМНАТА " + zone;
            objective.text = zone == 0 ? "Ответить на вызов и отправиться к отцу"
                : currentRoom != introductionRoom ? (history.clearedRooms >= zone ? "Прохождение сохранено" : currentRoom.objective)
                : history.remoteObtained ? "Оставьте копию на панели и пройдите дверь справа"
                : history.firstReturnSeen ? "Копия повторяет ваш путь. Заберите пульт у стола" : "Осмотрите комнату и пульт, появившийся из-под стола";
            status.text = "ЦИКЛ " + history.cycle.ToString("00") + "   •   " + (recordingActive ? "ЗАПИСЬ " + elapsed.ToString("0.0") + " с" : "ЗАПИСЬ НЕ АКТИВНА");
            if (currentRoom && !currentRoom.disableRecording) status.text += "   /   КОПИЙ: " + currentRoom.copyLimit;
            status.text += "\n";
            for (int i = 0; currentRoom && i < currentRoom.copyLimit && i < tapes.Count; i++)
            {
                if (i > 0) status.text += "   ";
                status.text += "КОПИЯ: " + Mathf.Max(0, tapes[tapes.Count - i - 1].duration - elapsed).ToString("0.0") + " с";
            }
            if (currentRoom && (currentRoom == introductionRoom || currentSequence))
                status.text += "\nПЛИТА А: " + (plateA ? "АКТИВНА" : "—");
            if (currentSequence) status.text += "   ПЛИТА Б: " + (plateB ? "АКТИВНА" : "—");
        }

        void ActivateRoom(LaboratoryRoom targetRoom, Transform arrival = null)
        {
            bool enteringLaboratory = targetRoom && !currentRoom;
            currentRoom = targetRoom;
            currentSequence = targetRoom ? targetRoom.GetComponent<SequenceRoomLayout>() : null;
            zone = targetRoom ? targetRoom.roomNumber : 0;
            bool inStation = !targetRoom;
            SetRoomVisible(inStation ? laboratoryRoots : stationRoots, false);
            SetRoomVisible(inStation ? stationRoots : laboratoryRoots, true);
            UpdateFurnitureSorting(inStation ? station.furniture : room01.furniture,
                inStation ? station.phoneSprite : room01.remoteSprite);
            position = arrival ? arrival.position : (inStation ? station.entrance.position : targetRoom.entrance.position);
            if (inStation || targetRoom.disableRecording)
            {
                elapsed = 0;
                nextSample = 0;
                recording = new EchoTape();
                recordingActive = false;
            }
            else ResetRoomRecording();

            foreach (var sequence in sequences)
            {
                if (inStation || sequence == currentSequence || !sequence.isActiveAndEnabled) continue;
                bool cleared = history.clearedRooms >= sequence.GetComponent<LaboratoryRoom>().roomNumber;
                sequence.Apply(false, false, cleared, new Bounds(FeetCenter(position), FeetSize), true);
            }

            stationExitLabel.enabled = inStation;
            if (enteringLaboratory) CacheLaboratoryCameraBounds();
            FitRoomCamera(snap: enteringLaboratory);
            PositionStationExitLabel();
            RefreshActors();
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
            hero.sprite = OfficerFrame(facing, false, 0);
            hero.transform.localScale = heroBaseScale * (zone > 0 ? .65f : 1f);
            Place(hero, position);
            foreach (var ghost in ghosts) ghost.transform.localScale = hero.transform.localScale;
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
            dialogBody = Text("Body", panel, new Vector2(820, 340), new Vector2(0, -20), 21, new Color(.86f, .9f, .89f));
            menu = Text("Menu", panel, new Vector2(820, 210), new Vector2(0, -70), 23, Color.white);
            ArrangeOverlayHUD();
        }
        void Panel(string title, string body) { panel.gameObject.SetActive(true); panel.SetAsLastSibling(); dialogTitle.text = title; dialogBody.text = body; menu.text = ""; }
        void Dialog(string title, string body) { mode = "dialog"; Panel(title, body + "\n\nE / ENTER — продолжить"); }
        void Close() { mode = "play"; panel.gameObject.SetActive(false); }
        void ShowMenu() { mode = "menu"; selection = 0; Panel("GOLOVOMKA / ЭХО", "Лаборатория захвачена. Ваш прошлый путь может помочь вам сейчас."); MenuText(); }
        string[] Choices() { return mode == "confirm" ? new[] { "Новая игра — заменить сохранение", "Отмена" } : mode == "pause" ? new[] { "Продолжить", "Вернуться к входу комнаты", "Главное меню", "Выход" } : new[] { "Продолжить", "Новая игра", "Выход" }; }
        void MenuText() { var items = Choices(); menu.text = ""; for (int i = 0; i < items.Length; i++) menu.text += (i == selection ? ">  " : "    ") + items[i] + "\n"; }
        void Select()
        {
            if (mode == "confirm") { if (selection == 0) { history = new EchoHistory(); Save(); BeginCycle(); } else ShowMenu(); return; }
            if (mode == "pause")
            {
                if (selection == 0) Close(); else if (selection == 1) RestartCycle(); else if (selection == 2) ShowMenu(); else Application.Quit(); return;
            }
            if (selection == 0) { history = EchoHistory.Load() ?? new EchoHistory(); BeginCycle(); }
            else if (selection == 1) { mode = "confirm"; selection = 1; Panel("НОВАЯ ИГРА", "Существующие записи и прогресс будут заменены."); MenuText(); }
            else Application.Quit();
        }
    }
}

