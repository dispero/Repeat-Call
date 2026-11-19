using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Golovomka
{
    public class MvpGame : MonoBehaviour
    {
        public AccessProgress Progress { get; private set; }
        public bool InLab { get; private set; }
        public bool ProgramLaunched { get; private set; }
        public bool WrongLock { get; private set; }
        public string Mode => mode;
        readonly List<Rect> obstacles = new List<Rect>();
        readonly List<SpriteRenderer> scenery = new List<SpriteRenderer>();
        Transform roomRoot;
        SpriteRenderer player;
        Texture2D interiors, roomTiles, walk;
        Sprite[] frames;
        Font font;
        Material spriteMaterial;
        Canvas canvas;
        RectTransform modal;
        Text title, cycle, objective, prompt, modalTitle, body, options, codeText, arrow, sourceLabel, saveError;
        string mode = "menu", typed = "", menuKind = "main";
        int choice, direction;
        float walkTime;
        Vector2 location;
        string nearby;
        Color ink = new Color(.86f, .9f, .88f), amber = new Color(1f, .74f, .38f);

        void Start()
        {
            Application.targetFrameRate = 60;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            spriteMaterial = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            interiors = Resources.Load<Texture2D>("Art/Interiors");
            roomTiles = Resources.Load<Texture2D>("Art/Room");
            walk = Resources.Load<Texture2D>("Art/IvanWalk");
            frames = new Sprite[24];
            for (int i = 0; i < frames.Length; i++) frames[i] = Slice(walk, i * 16, 0, 16, 32, new Vector2(.5f, 0));
            var cameraObject = new GameObject("RoomCamera", typeof(Camera));
            var cam = cameraObject.GetComponent<Camera>();
            cam.tag = "MainCamera";
            cam.transform.position = new Vector3(0, 0, -10);
            cam.orthographic = true;
            cam.orthographicSize = 8.4375f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(.035f, .055f, .065f);
            cam.allowHDR = cam.allowMSAA = false;
            var pp = cameraObject.AddComponent<UnityEngine.Rendering.Universal.PixelPerfectCamera>();
            pp.assetsPPU = 16;
            pp.refResolutionX = 480; pp.refResolutionY = 270;
            pp.gridSnapping = UnityEngine.Rendering.Universal.PixelPerfectCamera.GridSnapping.PixelSnapping;
            pp.cropFrame = UnityEngine.Rendering.Universal.PixelPerfectCamera.CropFrame.Windowbox;
            CreateUI();
            if (Keyboard.current != null) Keyboard.current.onTextInput += TextInput;
            Progress = ProgressStore.Load();
            BuildRoom(false);
            ShowMenu("main");
        }

        void OnDestroy() { if (Keyboard.current != null) Keyboard.current.onTextInput -= TextInput; }
        void TextInput(char c)
        {
            if (mode != "code" || c < '0' || c > '9' || typed.Length >= 4) return;
            typed += c; RefreshCode();
        }

        void Update()
        {
            var k = Keyboard.current; if (k == null) return;
            bool enter = k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame;
            if (mode == "menu" || mode == "pause" || mode == "confirm")
            {
                int count = mode == "confirm" ? 2 : menuKind == "pause" ? (Progress.firstLoopCompleted ? 4 : 3) : 3;
                if (k.downArrowKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame) { choice = (choice + 1) % count; RefreshMenu(); }
                if (k.upArrowKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame) { choice = (choice + count - 1) % count; RefreshMenu(); }
                if (k.escapeKey.wasPressedThisFrame && menuKind == "pause") CloseModal();
                else if (enter) ChooseMenu();
                return;
            }
            if (mode == "complete")
            {
                if (enter) ShowMenu("main");
                if (k.escapeKey.wasPressedThisFrame) Quit();
                return;
            }
            if (mode == "code")
            {
                if (k.backspaceKey.wasPressedThisFrame && typed.Length > 0) { typed = typed.Substring(0, typed.Length - 1); RefreshCode(); }
                if (k.escapeKey.wasPressedThisFrame) CloseModal();
                else if (enter && typed.Length == 4) Submit();
                return;
            }
            if (mode == "dialog")
            {
                if (enter || k.eKey.wasPressedThisFrame || k.escapeKey.wasPressedThisFrame) CloseModal();
                return;
            }
            if (k.escapeKey.wasPressedThisFrame) { ShowMenu("pause"); return; }
            Vector2 movement = new Vector2((k.dKey.isPressed || k.rightArrowKey.isPressed ? 1 : 0) - (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1 : 0),
                (k.wKey.isPressed || k.upArrowKey.isPressed ? 1 : 0) - (k.sKey.isPressed || k.downArrowKey.isPressed ? 1 : 0));
            if (movement != Vector2.zero)
            {
                direction = Mathf.Abs(movement.x) > Mathf.Abs(movement.y) ? (movement.x > 0 ? 0 : 1) : (movement.y > 0 ? 2 : 3);
                walkTime += Time.deltaTime * 8;
                Vector2 delta = movement.normalized * 3.8f * Mathf.Min(Time.deltaTime, .05f);
                Vector2 proposed = location + new Vector2(delta.x, 0);
                if (CanMove(proposed)) location = proposed;
                proposed = location + new Vector2(0, delta.y);
                if (CanMove(proposed)) location = proposed;
            }
            else walkTime = 0;
            player.sprite = frames[direction * 6 + ((int)walkTime % 6)];
            player.transform.position = new Vector3(Mathf.Round(location.x * 16) / 16, Mathf.Round(location.y * 16) / 16, 0);
            player.sortingOrder = 100 - Mathf.RoundToInt(location.y * 10);
            FindNearby();
            if (k.eKey.wasPressedThisFrame && nearby != null) Interact(nearby);
        }

        bool CanMove(Vector2 p)
        {
            if (p.x < -10.5f || p.x > 10.5f || p.y < -4.6f || p.y > 3.8f) return false;
            foreach (var obstacle in obstacles)
                if (obstacle.Overlaps(new Rect(p.x - .22f, p.y - .12f, .44f, .35f))) return false;
            return true;
        }

        void FindNearby()
        {
            nearby = null;
            float nearest = 1.55f;
            var spots = InLab ? new[] { "paper", "computer", "lock", "exit", "hint" } : new[] { "phone", "travel" };
            foreach (string spot in spots)
            {
                if (spot == "hint" && Progress.hint == 0) continue;
                float distance = Vector2.Distance(location, Spot(spot));
                if (distance < nearest) { nearest = distance; nearby = spot; }
            }
            prompt.text = nearby == null ? "WASD / СТРЕЛКИ — движение     E — осмотр     ESC — пауза" : "[ E ]  " + SpotLabel(nearby);
        }

        Vector2 Spot(string key)
        {
            switch (key)
            {
                case "paper": return new Vector2(-6, 1.5f);
                case "computer": return new Vector2(0, 1.5f);
                case "lock": return new Vector2(8.5f, .8f);
                case "exit": return new Vector2(-9, -3.5f);
                case "hint": return new Vector2(4, -1);
                case "phone": return new Vector2(-4, 1.5f);
                default: return new Vector2(9, -3.5f);
            }
        }
        string SpotLabel(string key)
        {
            switch (key) { case "paper": return "Лабораторная запись"; case "computer": return "Экспериментальная программа";
                case "lock": return Progress.solved ? "Открытый проход" : "Электронный замок";
                case "exit": return "Выход из лаборатории"; case "hint": return "Новая записка";
                case "phone": return "Принять вызов"; default: return "Отправиться в лабораторию"; }
        }

        public void Interact(string key)
        {
            switch (key)
            {
                case "paper": Dialog("ЛАБОРАТОРНАЯ ЗАПИСЬ №14", "Примеры преобразования:\n\n3837  →  9694\n9106  →  1103\n7375  →  9992\n\n«Правило одно. Применить к исходному числу из программы»."); break;
                case "computer":
                    if (Progress.solved) { Dialog("ТЕРМИНАЛ", "Протокол завершён. Проход открыт."); break; }
                    bool wasLaunched = ProgramLaunched;
                    ProgramLaunched = true;
                    if (!wasLaunched) UpdateLighting();
                    RefreshHUD(); Save();
                    Dialog("ПРОГРАММА ЗАПУЩЕНА", "Исходное число:  " + Progress.source + "\n\nСвет погас. На экране: «Система доступа заблокирована до перезапуска лаборатории».\n\nИван: «Что с электричеством? Нужно выбраться отсюда».\nСтрелка указывает на выход. Число остаётся на экране."); break;
                case "lock":
                    if (Progress.solved) { Complete(); break; }
                    if (!Progress.firstLoopCompleted) { Dialog("СИСТЕМА ДОСТУПА", "Терминал ввода пока не активен.\nЗапустите экспериментальную программу на центральном компьютере."); break; }
                    if (ProgramLaunched || WrongLock) { Dialog("ЗАМОК ЗАБЛОКИРОВАН", "Повторный ввод недоступен до следующего цикла.\nМожно выйти из лаборатории или использовать перезапуск в меню паузы."); break; }
                    typed = ""; Panel("code", "ПРОТОКОЛ ДОСТУПА", "Введите результат преобразования.\n\nОдна неверная попытка блокирует замок до следующего цикла."); RefreshCode(); break;
                case "exit": NextCycle(); break;
                case "phone": Dialog("ВЕЧЕРНИЙ ВЫЗОВ", "«Иван, поступило сообщение с предприятия. В лаборатории твоего отца беспорядок. Фёдора и Алексея не могут найти».\n\n" + (Progress.firstLoopCompleted ? "Тот же голос. Те же слова. Это уже было…" : "Нужно проверить, что произошло.")); break;
                case "travel": BuildRoom(true); break;
                case "hint": Dialog("ЗАПИСКА У РАБОЧЕГО СТОЛА", Progress.hint >= 2 ?
                    "«Проверь чередование: первая и третья позиции — последняя цифра квадрата. Вторая и четвёртая — первая.\n\n8² = 64: выбор цифры зависит от позиции»." :
                    "«Начни с квадратов каждой цифры.\nПочему одна и та же цифра может дать разный результат на разных местах?»"); break;
            }
        }

        void Submit()
        {
            if (typed.Length != 4) return;
            if (typed[0] == '0') { body.text = "Код содержит четыре цифры и не начинается с нуля. Исправьте ввод."; return; }
            bool locked = WrongLock;
            var result = Progress.TryCode(int.Parse(typed), ProgramLaunched, ref locked);
            WrongLock = locked; Save();
            if (result == AccessResult.Opened) { BuildRoom(true); Complete(); }
            else Dialog("НЕВЕРНЫЙ КОД", "Замок заблокирован до следующего цикла.\n\nВведённый ответ сохранён системой диагностики.");
        }

        public void NextCycle()
        {
            Progress.NextCycle(ProgramLaunched); ProgramLaunched = false; WrongLock = false;
            Save(); BuildRoom(false);
            Dialog("СНОВА В УЧАСТКЕ", Progress.firstLoopCompleted ? "Звонит телефон. На часах тот же вечер.\n\nИван: «Я только что был в лаборатории… Почему я снова здесь?»\n\nВ меню паузы доступен быстрый перезапуск цикла." : "Вы вернулись в участок.\nСнова поступает тот же вызов. В лаборатории ещё остались неисследованные приборы.");
        }
        void Save()
        {
            bool success = ProgressStore.Save(Progress);
            if (saveError != null) saveError.text = success ? "" : "Не удалось сохранить игру. Проверьте доступ к папке сохранений.";
        }
        void NewGame()
        {
            Progress = AccessProgress.NewGame(); ProgramLaunched = WrongLock = false;
            Save(); BuildRoom(false); CloseModal(); Interact("phone");
        }
        void Continue()
        {
            Progress = ProgressStore.Load();
            if (Progress == null) { NewGame(); return; }
            ProgramLaunched = WrongLock = false;
            BuildRoom(false); CloseModal();
            Dialog("ПРОДОЛЖИТЬ", "Начало текущего цикла. Пройденное испытание и исходное число сохранены.\n\n" + (Progress.solved ? "Проход в лаборатории уже открыт." : "Вызов снова ждёт ответа."));
        }
        void Complete()
        {
            Panel("complete", "ПЕРВОЕ ИСПЫТАНИЕ ПРОЙДЕНО", "Дверь открылась.\n\nКонец демо\n\nСпасибо за тестирование Golovomka.\n\nГрафика окружения: Modern Interiors — LimeZu.\nБесплатная версия, некоммерческая тестовая сборка.");
            options.text = "ENTER — главное меню     ESC — выйти";
        }
        void Quit() { Application.Quit(); }

        Sprite Slice(Texture2D texture, int x, int top, int width, int height, Vector2? pivot = null)
        {
            return Sprite.Create(texture, new Rect(x, texture.height - top - height, width, height), pivot ?? new Vector2(.5f, .5f), 16);
        }
        SpriteRenderer Draw(string name, Sprite sprite, Vector2 pos, int order, Color? tint = null)
        {
            var go = new GameObject(name); go.transform.SetParent(roomRoot);
            go.transform.position = new Vector3(pos.x, pos.y, 0);
            var r = go.AddComponent<SpriteRenderer>(); r.sprite = sprite;
            r.sharedMaterial = spriteMaterial;
            r.color = tint ?? Color.white; r.sortingOrder = order;
            scenery.Add(r); return r;
        }
        SpriteRenderer Block(string name, Vector2 pos, Vector2 size, Color color, int order)
        {
            var r = Draw(name, Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1), pos, order, color);
            r.transform.localScale = size; return r;
        }
        void Furniture(string name, int x, int y, int w, int h, float px, float py, bool solid = true)
        {
            Draw(name, Slice(interiors, x, y, w, h), new Vector2(px, py), 100 - Mathf.RoundToInt((py - h / 32f) * 10));
            if (solid) obstacles.Add(new Rect(px - w / 32f + .12f, py - h / 32f, w / 16f - .24f, h / 32f));
        }
        void BuildRoom(bool lab)
        {
            if (roomRoot != null) Destroy(roomRoot.gameObject);
            scenery.Clear(); obstacles.Clear(); InLab = lab;
            roomRoot = new GameObject(lab ? "Laboratory" : "PoliceStation").transform;
            var floor = Slice(roomTiles, 224, 112, 16, 16);
            for (int x = -11; x <= 11; x++) for (int y = -5; y <= 4; y++)
                Draw("Floor", floor, new Vector2(x, y), -100, lab ? new Color(.64f, .79f, .77f) : new Color(.76f, .75f, .68f));
            Block("BackWall", new Vector2(0, 5), new Vector2(24, 1.5f), new Color(.27f, .38f, .37f), -50);
            Block("WallTrim", new Vector2(0, 4.3f), new Vector2(24, .12f), new Color(.52f, .65f, .60f), -49);
            Block("LeftWall", new Vector2(-11.7f, 0), new Vector2(.5f, 10), new Color(.13f, .22f, .23f), 0);
            Block("RightWall", new Vector2(11.7f, 0), new Vector2(.5f, 10), new Color(.13f, .22f, .23f), 0);
            Block("BottomWall", new Vector2(0, -5.5f), new Vector2(24, .7f), new Color(.12f, .19f, .20f), 200);
            if (lab)
            {
                Furniture("FatherDesk", 0, 144, 48, 48, -6, 2.7f);
                Furniture("ComputerDesk", 0, 144, 48, 48, 0, 2.7f);
                Furniture("WorkTable", 128, 208, 32, 32, 4, .2f);
                Furniture("Shelf", 64, 224, 32, 48, -9.5f, 3);
                Furniture("SideDesk", 128, 208, 32, 32, 7, -2.7f);
                Block("Paper", new Vector2(-6, 2.5f), new Vector2(.6f, .4f), new Color(.94f, .88f, .66f), 230);
                Block("Monitor", new Vector2(0, 2.7f), new Vector2(1.5f, 1.05f), new Color(.06f, .12f, .14f), 230);
                Block("MonitorRim", new Vector2(0, 2.13f), new Vector2(1.8f, .12f), new Color(.63f, .65f, .58f), 229);
                Block("Door", new Vector2(9.8f, 1.8f), new Vector2(1.5f, 2.8f), Progress.solved ? new Color(.16f, .32f, .26f) : new Color(.25f, .33f, .36f), 80);
                Block("LockLight", new Vector2(8.6f, 1.5f), new Vector2(.35f, .6f), Progress.solved ? Color.green : amber, 250);
                Furniture("FallenChair", 48, 160, 16, 32, -3, -1.5f, false);
                for (int i = 0; i < 4; i++) Block("ScatteredPaper", new Vector2(-4 + i * .65f, -.5f + i % 2 * .5f), new Vector2(.45f, .3f), new Color(.74f, .76f, .67f), -20);
                if (Progress.hint > 0) Block("NewNote", new Vector2(4, .2f), new Vector2(.7f, .5f), new Color(1, .85f, .5f), 230);
                Block("ExitMat", new Vector2(-9, -3.7f), new Vector2(2, 1.5f), new Color(.25f, .38f, .35f), -90);
            }
            else
            {
                Furniture("DutyDesk", 0, 144, 48, 48, -4, 2.7f);
                Furniture("Desk", 0, 144, 48, 48, 2.5f, 2.7f);
                Furniture("Cabinet", 64, 224, 32, 48, -9, 3);
                Furniture("Table", 128, 208, 32, 32, 7, 2.5f);
                Block("Phone", new Vector2(-4, 2.5f), new Vector2(.75f, .45f), new Color(.13f, .16f, .2f), 230);
                Block("Door", new Vector2(9, -3.7f), new Vector2(2, 1.5f), new Color(.28f, .35f, .31f), -90);
                Block("NoticeBoard", new Vector2(4, 5), new Vector2(3, 1), new Color(.43f, .32f, .21f), 0);
                for (int i = 0; i < 3; i++) Block("Notice", new Vector2(3.2f + .8f * i, 5), new Vector2(.5f, .7f), new Color(.8f, .79f, .66f), 1);
            }
            player = Draw("Ivan", frames[18], new Vector2(0, -2.5f), 100);
            // A visible badge is an independent overlay; the source artwork is retained unchanged.
            var badge = new GameObject("PoliceBadge"); badge.transform.SetParent(player.transform);
            var badgeRenderer = badge.AddComponent<SpriteRenderer>();
            badgeRenderer.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1);
            badgeRenderer.sharedMaterial = player.sharedMaterial; badgeRenderer.color = amber;
            badge.transform.localPosition = new Vector3(-.18f, .8f, -.02f); badge.transform.localScale = new Vector3(.12f, .15f, 1);
            badgeRenderer.sortingOrder = 300;
            location = new Vector2(0, -2.5f); direction = 3;
            UpdateLighting(); RefreshHUD();
        }

        void UpdateLighting()
        {
            if (InLab && ProgramLaunched)
                foreach (var renderer in scenery) if (renderer != null && renderer != player && renderer.name != "Monitor") renderer.color *= new Color(.28f, .34f, .42f, 1);
            if (player != null) player.color = Color.white;
        }
        void RefreshHUD()
        {
            title.text = InLab ? "ЛАБОРАТОРИЯ / ПОДВАЛ" : "ПОЛИЦЕЙСКИЙ УЧАСТОК";
            cycle.text = Progress == null ? "GOLOVOMKA · MVP 01" : "ЦИКЛ " + Progress.cycle.ToString("00") + "     /     1996";
            objective.text = Progress == null ? "ПРОТОКОЛ ДОСТУПА" : Progress.solved ? "Проход открыт. Подойдите к двери." : !InLab ? "Примите вызов и отправляйтесь в лабораторию." : ProgramLaunched ? "Свет погас. Найдите выход из лаборатории." : "Осмотрите записи, компьютер и электронный замок.";
            arrow.text = InLab && ProgramLaunched ? "↓ ВЫХОД" : InLab ? "ВЫХОД" : "В ЛАБОРАТОРИЮ →";
            arrow.rectTransform.anchoredPosition = InLab ? new Vector2(-405, -176) : new Vector2(405, -176);
            sourceLabel.text = InLab ? ProgramLaunched ? Progress.source.ToString() : "READY" : "";
            modal.gameObject.SetActive(mode != "play");
        }

        RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
        }
        Text Label(string name, Transform parent, string value, int size, Vector2 dimensions, Vector2 position, Color color, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            var rect = Rect(name, parent, dimensions, position);
            var text = rect.gameObject.AddComponent<Text>(); text.font = font; text.text = value; text.fontSize = size;
            text.color = color; text.alignment = anchor; text.supportRichText = false; text.raycastTarget = false;
            return text;
        }
        void CreateUI()
        {
            var go = new GameObject("MvpUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
            title = Label("Location", go.transform, "", 24, new Vector2(700, 35), new Vector2(-240, 318), ink);
            cycle = Label("Cycle", go.transform, "", 18, new Vector2(380, 30), new Vector2(410, 318), amber, TextAnchor.UpperRight);
            objective = Label("Objective", go.transform, "", 19, new Vector2(1150, 40), new Vector2(0, 275), new Color(.58f, .71f, .71f));
            prompt = Label("Interaction", go.transform, "", 21, new Vector2(1180, 40), new Vector2(0, -290), ink, TextAnchor.MiddleCenter);
            Label("Credits", go.transform, "GOLOVOMKA / ТЕСТОВАЯ ВЕРСИЯ     •     Окружение: LimeZu — Modern Interiors", 14, new Vector2(1180, 25), new Vector2(0, -332), new Color(.43f, .56f, .58f), TextAnchor.MiddleCenter);
            arrow = Label("ExitArrow", go.transform, "", 18, new Vector2(240, 35), new Vector2(-405, -176), amber, TextAnchor.MiddleCenter);
            sourceLabel = Label("TerminalScreen", go.transform, "", 22, new Vector2(150, 35), new Vector2(0, 122), new Color(.6f, 1, .76f), TextAnchor.MiddleCenter);
            saveError = Label("SaveError", go.transform, "", 16, new Vector2(1180, 30), new Vector2(0, -253), new Color(1, .55f, .4f), TextAnchor.MiddleCenter);
            modal = Rect("Modal", go.transform, new Vector2(900, 490), Vector2.zero);
            var background = modal.gameObject.AddComponent<UnityEngine.UI.Image>(); background.color = new Color(.035f, .065f, .08f, .985f);
            modalTitle = Label("Title", modal, "", 27, new Vector2(800, 55), new Vector2(0, 178), amber);
            body = Label("Body", modal, "", 23, new Vector2(800, 305), new Vector2(0, -5), ink);
            options = Label("Options", modal, "", 21, new Vector2(800, 170), new Vector2(0, -70), ink);
            codeText = Label("Code", modal, "", 48, new Vector2(600, 65), new Vector2(0, -80), amber, TextAnchor.MiddleCenter);
        }
        void Panel(string state, string heading, string text)
        {
            mode = state; modal.gameObject.SetActive(true); modalTitle.text = heading; body.text = text;
            options.text = ""; codeText.text = "";
            options.rectTransform.anchoredPosition = new Vector2(0, state == "menu" || state == "pause" || state == "confirm" ? -60 : -190);
            options.rectTransform.sizeDelta = new Vector2(800, state == "menu" || state == "pause" || state == "confirm" ? 200 : 50);
        }
        void Dialog(string heading, string text) { Panel("dialog", heading, text); options.text = "E / ENTER — продолжить"; }
        void CloseModal() { mode = "play"; modal.gameObject.SetActive(false); }
        void RefreshCode() { codeText.text = typed.PadRight(4, '·'); options.text = "ЦИФРЫ — ввод    BACKSPACE — стереть    ENTER — подтвердить    ESC — назад"; options.fontSize = 17; }
        void ShowMenu(string kind)
        {
            menuKind = kind; choice = 0;
            Panel(kind == "pause" ? "pause" : "menu", kind == "pause" ? "ПАУЗА" : "GOLOVOMKA", kind == "pause" ? "" : "ПРОТОКОЛ ДОСТУПА\n\n1996 год. Вечерний вызов.");
            options.fontSize = 23; RefreshMenu();
        }
        void RefreshMenu()
        {
            string[] entries = mode == "confirm" ? new[] { "Начать заново", "Отмена" } : menuKind == "main" ? new[] { "Продолжить", "Новая игра", "Выход" } : Progress.firstLoopCompleted ? new[] { "Продолжить", "Перезапустить цикл", "Главное меню", "Выход" } : new[] { "Продолжить", "Главное меню", "Выход" };
            options.text = "";
            for (int i = 0; i < entries.Length; i++) options.text += (i == choice ? "▶  " : "     ") + entries[i] + "\n";
        }
        void ChooseMenu()
        {
            if (mode == "confirm") { if (choice == 0) NewGame(); else ShowMenu("main"); return; }
            if (menuKind == "main")
            {
                if (choice == 0) Continue();
                else if (choice == 1) { if (ProgressStore.Load() == null) NewGame(); else { Panel("confirm", "НОВАЯ ИГРА", "Текущий прогресс будет заменён новым прохождением."); choice = 1; RefreshMenu(); } }
                else Quit();
            }
            else
            {
                if (choice == 0) CloseModal();
                else if (Progress.firstLoopCompleted && choice == 1) NextCycle();
                else if (choice == (Progress.firstLoopCompleted ? 2 : 1)) { Save(); ShowMenu("main"); }
                else { Save(); Quit(); }
            }
        }
    }
}
