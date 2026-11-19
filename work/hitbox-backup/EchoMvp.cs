using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Golovomka
{
    public partial class EchoMvp : MonoBehaviour
    {
        public EchoHistory history;
        public EchoTape recording;
        public float elapsed;
        public int zone, facing = 3;
        public Vector2 position;
        public bool plateA, plateB;
        public bool recordingActive;
        public string mode = "menu";
        [NonSerialized] public string saveFileOverride;
        public static readonly Vector2 PlateA = new Vector2(-6, -2.5f), PlateB = new Vector2(-5, 2.6f);
        SpriteRenderer hero, gate, secondGate, padA, padB;
        public Vector2 CurrentPlateB => zone == 2 || zone == 3 || zone == 4 ? new Vector2(6, -2.5f) : PlateB;
        public int RoomCopies => zone < 3 ? 1 : history.copySlots;
        bool GateOpen => history.clearedRooms >= zone || (zone < 3 ? plateA : plateA && plateB);
        readonly SpriteRenderer[] ghosts = new SpriteRenderer[2];
        readonly List<Rect> furniture = new List<Rect>();
        readonly List<Sprite> createdSprites = new List<Sprite>();
        Material material;
        Transform world;
        Texture2D atlas, tiles, walk;
        Sprite floor, white;
        Sprite[] frames;
        Font font;
        Canvas canvas;
        RectTransform panel;
        Text heading, objective, status, prompt, dialogTitle, dialogBody, menu, caution;
        readonly List<Text> worldLabels = new List<Text>();
        int selection;
        bool phoneRead;
        float nextSample;
        Color cyan = new Color(.32f, .88f, 1), amber = new Color(1, .74f, .36f);

        void Start()
        {
            Application.targetFrameRate = 60;
            material = new Material(Resources.Load<Material>("Art/MvpSprite"));
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            atlas = Resources.Load<Texture2D>("Art/Interiors"); tiles = Resources.Load<Texture2D>("Art/Room"); walk = Resources.Load<Texture2D>("Art/IvanOfficerWalk");
            floor = Crop(tiles, 224, 112, 16, 16);
            white = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1); createdSprites.Add(white);
            frames = LoadOfficerFrames();
            var cam = new GameObject("FixedRoomCamera", typeof(Camera)).GetComponent<Camera>(); cam.tag = "MainCamera";
            roomCamera = cam;
            cam.transform.position = new Vector3(0, 0, -10); cam.orthographic = true; cam.orthographicSize = 8.4375f;
            cam.backgroundColor = new Color(.035f, .055f, .065f); cam.clearFlags = CameraClearFlags.SolidColor;
            cam.allowHDR = cam.allowMSAA = false;
            var pixel = cam.gameObject.AddComponent<UnityEngine.Rendering.Universal.PixelPerfectCamera>();
            pixel.assetsPPU = 16; pixel.refResolutionX = 480; pixel.refResolutionY = 270;
            pixel.gridSnapping = UnityEngine.Rendering.Universal.PixelPerfectCamera.GridSnapping.PixelSnapping;
            pixel.cropFrame = UnityEngine.Rendering.Universal.PixelPerfectCamera.CropFrame.Windowbox;
            // Заполнение окна по согласованному режиму требует свободного
            // масштаба. Point и привязка движения к сетке остаются прежними.
            pixel.enabled = false;
            UI(); history = EchoHistory.Load() ?? new EchoHistory();
            zone = 0; position = StationSpawn; BuildRoom(); ShowMenu();
        }
        void OnDestroy() { foreach (var s in createdSprites) if (s) Destroy(s); if (material) Destroy(material); }
        public void BeginCycle()
        {
            elapsed = 0; nextSample = 0; zone = 0; facing = 3; position = StationSpawn;
            recording = new EchoTape(); recordingActive = false; phoneRead = false; mode = "play";
            BuildRoom(); panel.gameObject.SetActive(false);
        }
        public void StartRecording()
        {
            if (zone == 0 || recordingActive) return;
            elapsed = 0; nextSample = .05f; recording = new EchoTape { room = zone }; recordingActive = true;
            Record(); HUD();
        }
        public void RestartCycle()
        {
            if (zone == 0 || !history.remoteObtained) return;
            ReturnToRoom();
        }

        void Save()
        {
            try { history.Save(saveFileOverride); caution.text = ""; }
            catch (Exception ex) { caution.text = "Не удалось сохранить прогресс. Продолжение после закрытия может быть недоступно."; Debug.LogWarning(ex.Message); }
        }
        void Update()
        {
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
            if (k.tKey.wasPressedThisFrame && history.manualRecording) StartRecording();
            float dt = Mathf.Min(Time.deltaTime, .05f); if (recordingActive) elapsed += dt;
            UpdateGhosts();
            Vector2 move = new Vector2((k.dKey.isPressed || k.rightArrowKey.isPressed ? 1 : 0) - (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1 : 0),
                (k.wKey.isPressed || k.upArrowKey.isPressed ? 1 : 0) - (k.sKey.isPressed || k.downArrowKey.isPressed ? 1 : 0));
            if (move != Vector2.zero)
            {
                facing = Mathf.Abs(move.x) > Mathf.Abs(move.y) ? (move.x > 0 ? 0 : 1) : (move.y > 0 ? 2 : 3);
                var delta = move.normalized * 3.6f * dt;
                Vector2 step = position + new Vector2(delta.x, 0); if (CanMove(step)) position = step;
                step = position + new Vector2(0, delta.y); if (CanMove(step)) position = step;
            }
            hero.sprite = OfficerFrame(facing, move != Vector2.zero, Time.time);
            Place(hero, position); UpdateGhosts();
            if (zone == 1 && position.x > IntroPoint(1440, 0).x && position.y > IntroPoint(0, 725).y)
            { EnterSecondRoom(); return; }
            if (recordingActive && elapsed >= nextSample) { Record(); nextSample = elapsed + .05f; }
            string target = Nearby(); prompt.text = target == "" ? (history.manualRecording && zone == 4 && !recordingActive ? "T — начать запись и воспроизведение    E — осмотр    ESC — пауза" : history.remoteObtained && zone > 0 ? "WASD — идти    E — осмотр    R — к входу комнаты    ESC — пауза" : "WASD / СТРЕЛКИ — идти    E — осмотр    ESC — пауза") : "[ E ]  " + target;
            if (k.eKey.wasPressedThisFrame && target != "") Interact(target);
            HUD();
        }
        public void Record() { if (recordingActive && zone > 0) recording.frames.Add(new EchoFrame(elapsed, position, zone, facing)); }
        void Place(SpriteRenderer sprite, Vector2 p)
        {
            sprite.transform.position = new Vector3(Mathf.Round(p.x * 16) / 16, Mathf.Round(p.y * 16) / 16, 0);
            sprite.sortingOrder = 100 - Mathf.RoundToInt(p.y * 10);
            if (zone == 0 && sprite == hero) UpdateStationDepth(p);
            if (zone == 1 && sprite == hero)
                foreach (var layer in introDepth)
                    if (layer.renderer) layer.renderer.enabled = p.x >= layer.left && p.x <= layer.right;
        }
        public void UpdateGhosts()
        {
            plateA = EchoHistory.OnPlate(new EchoFrame(0, position, zone, facing), CurrentPlateA);
            plateB = EchoHistory.OnPlate(new EchoFrame(0, position, zone, facing), CurrentPlateB);
            var tapes = ActiveTapes;
            for (int i = 0; i < 2; i++)
            {
                EchoFrame f = default;
                int tapeIndex = tapes.Count - RoomCopies + i;
                bool active = recordingActive && zone > 0 && i < RoomCopies && tapeIndex >= 0 && tapeIndex < tapes.Count && tapes[tapeIndex].Sample(elapsed, out f);
                ghosts[i].enabled = active && f.zone == zone;
                if (!active) continue;
                bool moving = tapes[tapeIndex].Sample(Mathf.Max(0, elapsed - .08f), out var previous)
                    && previous.zone == f.zone && new Vector2(f.x - previous.x, f.y - previous.y).sqrMagnitude > .0001f;
                Place(ghosts[i], new Vector2(f.x, f.y)); ghosts[i].sprite = OfficerFrame(f.facing, moving, elapsed);
                if (f.zone == zone) { plateA |= EchoHistory.OnPlate(f, CurrentPlateA); plateB |= EchoHistory.OnPlate(f, CurrentPlateB); }
            }
            if (zone == 1) UpdateIntroductionVisuals();
            else if (zone > 0)
            {
                gate.color = GateOpen ? new Color(.22f, .65f, .56f, .2f) : new Color(.35f, .5f, .53f);
                if (secondGate) secondGate.color = plateB || history.clearedRooms >= zone ? new Color(.22f, .65f, .56f, .2f) : new Color(.6f, .4f, .2f);
                padA.color = plateA ? cyan : new Color(.22f, .35f, .39f);
                padB.color = plateB ? amber : new Color(.4f, .31f, .21f);
            }
        }
        bool CanMove(Vector2 p)
        {
            if (zone == 0) return CanMoveInStation(p);
            if (zone == 1) return CanMoveInIntroduction(p);
            if (p.x < -10.3f || p.x > 10.3f || p.y < -4.6f || p.y > 3.8f) return false;
            foreach (var rect in furniture) if (rect.Overlaps(new Rect(p.x - .22f, p.y - .1f, .44f, .28f))) return false;
            if (zone > 0 && Mathf.Abs(p.y) < .65f)
            {
                if (Mathf.Abs(p.x) > 1.2f) return false;
                bool alreadyInGap = Mathf.Abs(position.x) <= 1.2f && Mathf.Abs(position.y) < .65f;
                if (!GateOpen && !alreadyInGap) return false;
            }
            if (zone == 2 && Mathf.Abs(p.y - 2.5f) < .45f)
            {
                if (Mathf.Abs(p.x) > 1.2f) return false;
                bool inGap = Mathf.Abs(position.x) <= 1.2f && Mathf.Abs(position.y - 2.5f) < .45f;
                if (!plateB && history.clearedRooms < 2 && !inGap) return false;
            }
            return true;
        }
        string Nearby()
        {
            if (zone == 1) return IntroNearby();
            if (zone == 0)
            {
                if (Vector2.Distance(position, StationPhone) < 1.6f) return "Телефон дежурного";
                if (Vector2.Distance(position, StationExit) < 1.5f) return "Отправиться в лабораторию";
            }
            else
            {
                if (Vector2.Distance(position, new Vector2(-9, -3.5f)) < 1.4f) return zone == 1 ? "Выйти / завершить цикл" : "Предыдущая комната";
                if (Vector2.Distance(position, new Vector2(0, -2.5f)) < 1.4f) return zone == 4 ? "Пульт записи" : "Запись Фёдора";
                if (Vector2.Distance(position, zone == 2 ? new Vector2(0, 3.7f) : new Vector2(6, 2)) < 1.2f) return "Пульт аварийного прохода";
            }
            return "";
        }
        public void Interact(string target)
        {
            switch (target)
            {
                case "Осмотреть пульт":
                case "Взять пульт": UseIntroRemote(); break;
                case "Вернуться в участок": Save(); BeginCycle(); break;
                case "Телефон дежурного": phoneRead = true; Dialog("АВТОМАТИЧЕСКИЙ ВЫЗОВ", "Лаборатория в подвале предприятия. Сработала система безопасности. Автоматический вызов полиции.\n\nИван узнаёт адрес: здесь работают его отец и брат. Выход из участка справа у дальней стены."); break;
                case "Отправиться в лабораторию":
                    if (!phoneRead) { Dialog("ДЕЖУРНЫЙ", "Сначала ответьте на телефон. Поступил вызов."); break; }
                    zone = 1; position = IntroSpawn; ResetRoomRecording(); BuildRoom();
                    break;
                case "Выйти / завершить цикл": RestartCycle(); break;
                case "Предыдущая комната": zone--; position = zone == 1 ? IntroPoint(1340, 670) : zone == 2 ? new Vector2(0, 3.6f) : new Vector2(6, 1.5f); ResetRoomRecording(); BuildRoom(); break;
                case "Запись Фёдора": Dialog("КОМНАТА " + zone, zone == 1 ? "Одна копия. Запишите ожидание на плите А и нажмите R.\nВ следующем цикле копия удержит дверь, а вы пройдёте к выходу справа сверху." : zone == 2 ? "Одна копия должна по очереди открыть две двери.\nЗапишите: ожидание на А → переход на Б → ожидание.\nВ следующем цикле пройдите первую дверь, дождитесь второго сигнала и идите через верхнюю дверь. Обе плиты доступны снизу." : "Теперь доступны две копии.\nЗапишите ожидание на А, затем в следующем цикле — ожидание на Б.\nКогда обе копии займут плиты, пройдите к выходу.\nR — вернуться к входу комнаты."); break;
                case "Пульт записи":
                    if (!history.manualRecording) ActivateManualRoom();
                    StartRecording();
                    Dialog("ПУЛЬТ ЗАПИСИ", "Начало записи и копий теперь под вашим контролем.\nE у пульта или T в этой комнате — запуск. R — сохранить запись и начать цикл.\n\nПодготовьте две записи ожидания на разных плитах. В последнем цикле можно занять удобную позицию и только затем запустить копии.\nЗа пределами этой комнаты T не действует."); break;
                case "Пульт аварийного прохода":
                    if (zone < 4)
                    {
                        // Сигнал нужен для пересечения двери, а не для выхода из уже пройденной комнаты.
                        if (position.y < (zone == 2 ? 2.95f : .65f)) { Dialog("ПРОХОД ЗАКРЫТ", "Сначала пройдите двери с помощью записанных действий."); break; }
                        history.clearedRooms = Mathf.Max(history.clearedRooms, zone);
                        zone++; if (zone == 3) history.copySlots = 2;
                        position = RoomSpawn(zone);
                        if (zone == 4) ActivateManualRoom(); else ResetRoomRecording();
                        Save(); BuildRoom();
                        Dialog("КОМНАТА " + zone, zone == 2 ? "Вторая комната: один клон и две последовательные двери. Прочитайте запись у стола." : zone == 3 ? "Открыт второй слот. В третьей комнате нужны две копии одновременно." : "Новый этап: записи этой комнаты запускаются вручную.\nВ четвёртой комнате находится пульт записи. Подойдите к нему. Здесь запись не начинается автоматически. Прежние комнаты остаются пройденными."); break;
                    }
                    if (!plateA || !plateB) { Dialog("НЕ ХВАТАЕТ СИГНАЛОВ", "Нужны сигналы с обеих плит: А и Б.\n\nПока вы у пульта, их должны удерживать две копии. Подготовьте два последовательных цикла, оставив достаточно времени на ожидание."); break; }
                    history.completed = true; Save(); mode = "done";
                    Panel("ПУТЬ ОТКРЫТ", "Иван и две его копии активировали аварийный проход.\n\nЗа следующей дверью — Фёдор, Алексей и трое захватчиков.\n\nКонец MVP. Финальный бой пока не входит в эту версию.\n\nENTER — главное меню"); break;
            }
        }
        void ActivateManualRoom()
        {
            if (!history.manualRecording) { history.manualRecording = true; }
            recordingActive = false; elapsed = 0; recording = new EchoTape();
            Save();
        }
        void HUD()
        {
            var tapes = ActiveTapes;
            heading.text = zone == 0 ? "ПОЛИЦЕЙСКИЙ УЧАСТОК" : "ПОДВАЛ / КОМНАТА " + zone;
            objective.text = zone == 0 ? "Ответить на вызов и отправиться к отцу" : zone == 1 ? (history.remoteObtained ? "Оставьте копию на панели и пройдите дверь справа" : history.firstReturnSeen ? "Копия повторяет ваш путь. Заберите пульт у стола" : "Осмотрите комнату и пульт, появившийся из-под стола") : zone == 2 ? "Один клон: А → Б, две двери по очереди" : zone == 3 ? "Две копии: А и Б одновременно" : "Пульт записи: подготовьте пути и выберите момент старта";
            status.text = "ЦИКЛ " + history.cycle.ToString("00") + "   •   " + (recordingActive ? "ЗАПИСЬ " + elapsed.ToString("0.0") + " с" : "ЗАПИСЬ НЕ НАЧАТА") + "   /   КОПИЙ: " + RoomCopies + "\n";
            for (int i = Mathf.Max(0, tapes.Count - RoomCopies); i < tapes.Count; i++) status.text += "КОПИЯ " + (i + 1) + ": " + Mathf.Max(0, tapes[i].duration - elapsed).ToString("0.0") + " с   ";
            if (zone > 0) status.text += "\nПЛИТА А: " + (plateA ? "АКТИВНА" : "—") + (zone == 1 ? "" : "    ПЛИТА Б: " + (plateB ? "АКТИВНА" : "—"));
        }
        Sprite Crop(Texture2D t, int x, int top, int w, int h, Vector2? pivot = null)
        {
            var s = Sprite.Create(t, new Rect(x, t.height - top - h, w, h), pivot ?? Vector2.one * .5f, 16); createdSprites.Add(s); return s;
        }
        SpriteRenderer SpriteObject(string name, Sprite sprite, Vector2 p, int order, Color color)
        {
            var go = new GameObject(name); go.transform.SetParent(world); go.transform.position = p;
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = sprite; sr.sharedMaterial = material; sr.sortingOrder = order; sr.color = color; return sr;
        }
        SpriteRenderer Block(string name, Vector2 p, Vector2 size, Color c, int order = 0)
        { var sr = SpriteObject(name, white, p, order, c); sr.transform.localScale = size; return sr; }
        void Furnish(string name, int x, int top, int w, int h, Vector2 p)
        {
            SpriteObject(name, Crop(atlas, x, top, w, h), p, 100 - Mathf.RoundToInt((p.y - h / 32f) * 10), new Color(.8f, .88f, .88f));
            furniture.Add(new Rect(p.x - w / 32f + .2f, p.y - h / 32f, w / 16f - .4f, h / 32f));
        }
        public void BuildRoom()
        {
            if (world) { world.gameObject.SetActive(false); Destroy(world.gameObject); }
            foreach (var label in worldLabels) if (label) Destroy(label.gameObject); worldLabels.Clear(); worldLabelPoints.Clear(); furniture.Clear(); secondGate = null;
            world = new GameObject(zone == 0 ? "PoliceStation" : "Laboratory").transform;
            FitRoomCamera();
            if (zone == 0) BuildPoliceStation();
            else if (zone == 1) BuildIntroduction();
            else
            {
            for (int x = -11; x <= 11; x++) for (int y = -5; y <= 4; y++) SpriteObject("Floor", floor, new Vector2(x, y), -100, new Color(.66f, .79f, .77f));
            Block("BackWall", new Vector2(0, 4.7f), new Vector2(24, 1), new Color(.25f, .37f, .37f), -50);
            Block("LeftWall", new Vector2(-11.3f, 0), new Vector2(.4f, 10), new Color(.12f, .21f, .24f));
            Block("RightWall", new Vector2(11.3f, 0), new Vector2(.4f, 10), new Color(.12f, .21f, .24f));
                Block("PartitionLeft", new Vector2(-6.25f, 0), new Vector2(10, 1), new Color(.23f, .34f, .37f), 130);
                Block("PartitionRight", new Vector2(6.25f, 0), new Vector2(10, 1), new Color(.23f, .34f, .37f), 130);
                gate = Block("GateA", Vector2.zero, new Vector2(2.5f, 1), new Color(.4f, .5f, .53f), 130);
                padA = Block("PlateA", PlateA, new Vector2(1.5f, 1), new Color(.22f, .35f, .39f), -60);
                padB = Block("PlateB", CurrentPlateB, new Vector2(1.5f, 1), new Color(.4f, .31f, .21f), -60); padB.enabled = zone > 1;
                Block("PlateAInset", PlateA, new Vector2(1.2f, .7f), new Color(.15f, .23f, .27f), -59);
                if (zone > 1) Block("PlateBInset", CurrentPlateB, new Vector2(1.2f, .7f), new Color(.25f, .2f, .13f), -59);
                if (zone == 2)
                {
                    Block("SecondPartitionLeft", new Vector2(-6.25f, 2.5f), new Vector2(10, .7f), new Color(.27f, .31f, .34f), 140);
                    Block("SecondPartitionRight", new Vector2(6.25f, 2.5f), new Vector2(10, .7f), new Color(.27f, .31f, .34f), 140);
                    secondGate = Block("GateB", new Vector2(0, 2.5f), new Vector2(2.5f, .7f), amber, 140);
                    WorldLabel("ПРОХОД В КОМНАТУ 3", new Vector2(0, 4), amber);
                }
                Furnish("Desk", 0, 144, 48, 48, new Vector2(0, -1.3f));
                Furnish("ControlDesk", 0, 144, 48, 48, new Vector2(6, 3.3f));
                Block("ConsoleScreen", new Vector2(6, 3.5f), new Vector2(1.3f, .7f), new Color(.2f, .6f, .52f), 230);
                Block("ExitMat", new Vector2(-9, -3.5f), new Vector2(1.5f, 1), new Color(.3f, .34f, .28f), -70);
                WorldLabel("А", PlateA + new Vector2(0, -.9f), cyan); if (zone > 1) WorldLabel("Б", CurrentPlateB + new Vector2(0, -1), amber);
                WorldLabel(zone == 1 ? "ВЫХОД / R" : "НАЗАД", new Vector2(-9, -4.3f), amber);
                WorldLabel(zone == 4 ? "ПУЛЬТ ЗАПИСИ / E" : "ЗАПИСЬ ФЁДОРА", new Vector2(0, -3.4f), amber);
                if (zone != 2) WorldLabel(zone < 4 ? "В СЛЕДУЮЩУЮ КОМНАТУ" : "А + Б → ВЫХОД", new Vector2(6, 4.2f), amber);
            }
            hero = SpriteObject("Ivan", OfficerFrame(facing, false, 0), position, 100, Color.white); Place(hero, position);
            for (int i = 0; i < 2; i++) ghosts[i] = SpriteObject("Echo" + i, OfficerFrame(facing, false, 0), Vector2.zero, 100, i == 0 ? new Color(.3f, .85f, 1, .55f) : new Color(1, .65f, .25f, .55f));
            if (zone == 1)
            {
                hero.transform.localScale = Vector3.one * .65f;
                foreach (var ghost in ghosts) ghost.transform.localScale = Vector3.one * .65f;
            }
            UpdateGhosts(); HUD();
        }
        Text Text(string name, Transform parent, Vector2 size, Vector2 p, int fontSize, Color color, TextAnchor align = TextAnchor.UpperLeft)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f; rect.sizeDelta = size; rect.anchoredPosition = p;
            var text = go.AddComponent<Text>(); text.font = font; text.fontSize = fontSize; text.color = color; text.alignment = align; text.supportRichText = false; text.raycastTarget = false; return text;
        }
        void WorldLabel(string text, Vector2 p, Color color)
        {
            Vector3 screen = Camera.main.WorldToViewportPoint(p);
            var label = Text("WorldLabel", canvas.transform, new Vector2(280, 34), new Vector2((screen.x - .5f) * 1280, (screen.y - .5f) * 720), 17, color, TextAnchor.MiddleCenter);
            label.text = text; worldLabels.Add(label);
            worldLabelPoints.Add(p); PositionWorldLabels();
        }
        void UI()
        {
            var go = new GameObject("EchoUI", typeof(Canvas), typeof(CanvasScaler)); canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
            heading = Text("Location", go.transform, new Vector2(700, 40), new Vector2(-230, 320), 25, new Color(.87f, .91f, .9f));
            objective = Text("Objective", go.transform, new Vector2(700, 38), new Vector2(-225, 260), 19, new Color(.58f, .73f, .73f));
            status = Text("Status", go.transform, new Vector2(520, 80), new Vector2(330, 300), 17, amber, TextAnchor.UpperRight);
            prompt = Text("Prompt", go.transform, new Vector2(1220, 42), new Vector2(0, -292), 20, Color.white, TextAnchor.MiddleCenter);
            caution = Text("SaveStatus", go.transform, new Vector2(1180, 30), new Vector2(0, -250), 15, amber, TextAnchor.MiddleCenter);
            Text("Credits", go.transform, new Vector2(1200, 30), new Vector2(0, -318), 14, new Color(.45f, .57f, .58f), TextAnchor.MiddleCenter).text = "GOLOVOMKA • MVP КОПИЙ   /   Участок, комната 1 и Иван: собственная графика с AI • Остальной подвал: LimeZu • Некоммерческий прототип";
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

