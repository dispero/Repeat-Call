using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Golovomka
{
    public partial class EchoMvp
    {
        static readonly string[] TitleLabels = { "Продолжить", "Новая игра", "Выход" };
        static readonly string[] ConfirmLabels = { "Начать заново", "Отмена" };
        static readonly Color TitleAccent = new Color32(255, 195, 107, 255);
        static readonly Color TitlePaper = new Color32(230, 214, 181, 255);
        static readonly Color TitleDisabled = new Color32(147, 148, 132, 255);
        readonly Button[] titleButtons = new Button[3];
        readonly Text[] titleLabels = new Text[3];
        readonly RawImage[] titleIcons = new RawImage[3];
        Sprite titlePlate;
        Sprite titlePlateSelected;
        GameObject titleScreen;
        Text titleNotice;
        int titleSelection;

        void CreateTitleMenu()
        {
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            var input = new GameObject("TitleMenuInput", typeof(EventSystem), typeof(InputSystemUIInputModule));
            input.transform.SetParent(canvas.transform, false);
            input.GetComponent<EventSystem>().sendNavigationEvents = false;

            var screen = MenuRegion("TitleScreen", canvas.transform, Vector2.zero, Vector2.one);
            titleScreen = screen.gameObject;
            var backdrop = MenuRegion("LaboratoryBackdrop", screen, Vector2.zero, Vector2.one);
            var art = backdrop.gameObject.AddComponent<RawImage>();
            art.texture = Resources.Load<Texture2D>("Menu/LaboratoryTitle");
            art.color = art.texture ? Color.white : new Color32(12, 29, 32, 255);
            art.raycastTarget = false;
            var cover = backdrop.gameObject.AddComponent<AspectRatioFitter>();
            cover.aspectRatio = art.texture ? (float)art.texture.width / art.texture.height : 16f / 9f;
            cover.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;

            var menuFont = Resources.Load<Font>("Menu/PixelifySans");
            if (!menuFont) menuFont = font;
            var wordmark = Resources.Load<Texture2D>("Menu/Wordmark");
            var titleMin = new Vector2(.086f, .643f);
            var titleMax = new Vector2(.58f, .805f);
            if (wordmark)
            {
                var logo = MenuRegion("Wordmark", screen, titleMin, titleMax).gameObject.AddComponent<RawImage>();
                logo.texture = wordmark;
                logo.raycastTarget = false;
                FitMenuGraphic(logo.rectTransform, (float)wordmark.width / wordmark.height);
            }
            else
            {
                var logo = MenuLabel("Wordmark", screen, titleMin, titleMax, menuFont, 96, TitlePaper, "GOLOVOMKA");
                AddMenuOutline(logo, 3);
            }

            titleNotice = MenuLabel("Confirmation", screen, new Vector2(.105f, .594f), new Vector2(.50f, .637f),
                menuFont, 23, TitlePaper, "Заменить текущее сохранение?");
            titleNotice.alignment = TextAnchor.MiddleLeft;
            AddMenuOutline(titleNotice, 1);

            titlePlate = Resources.Load<Sprite>("Menu/MetalPlate");
            titlePlateSelected = Resources.Load<Sprite>("Menu/MetalPlateSelected");
            var iconAtlas = Resources.Load<Texture2D>("Menu/ControlIcons");

            for (int i = 0; i < titleButtons.Length; i++)
            {
                float top = .595f - i * .109f;
                var frame = MenuRegion("Option" + (i + 1), screen, new Vector2(.106f, top - .093f),
                    new Vector2(.395f, top));
                var image = frame.gameObject.AddComponent<Image>();
                var button = frame.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                button.transition = Selectable.Transition.None;
                titleButtons[i] = button;
                titleLabels[i] = MenuLabel("Label", frame, new Vector2(.34f, .09f), new Vector2(.95f, .91f),
                    menuFont, 33, TitlePaper, "");
                titleLabels[i].alignment = TextAnchor.MiddleLeft;
                AddMenuOutline(titleLabels[i], 1);

                var icon = MenuRegion("Icon", frame, new Vector2(.09f, .21f), new Vector2(.23f, .79f))
                    .gameObject.AddComponent<RawImage>();
                icon.texture = iconAtlas;
                icon.enabled = iconAtlas != null;
                icon.raycastTarget = false;
                FitMenuGraphic(icon.rectTransform, 1);
                titleIcons[i] = icon;

                int index = i;
                button.onClick.AddListener(() => UseTitleOption(index));
                var hover = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                hover.callback.AddListener(_ =>
                {
                    if (button.interactable) FocusTitleOption(index);
                });
                frame.gameObject.AddComponent<EventTrigger>().triggers.Add(hover);
            }
            titleScreen.SetActive(false);
        }

        static void FitMenuGraphic(RectTransform target, float aspectRatio)
        {
            // FitInParent управляет якорями, поэтому вписываем графику внутри области.
            var area = MenuRegion("Area", target.parent, target.anchorMin, target.anchorMax);
            target.SetParent(area, false);
            var aspect = target.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectRatio = aspectRatio;
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        }

        static void AddMenuOutline(Graphic graphic, float width)
        {
            var outline = graphic.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color32(24, 23, 19, 255);
            outline.effectDistance = new Vector2(width, width);
        }

        static RectTransform MenuRegion(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        static Text MenuLabel(string name, Transform parent, Vector2 min, Vector2 max, Font menuFont,
            int size, Color color, string value)
        {
            var text = MenuRegion(name, parent, min, max).gameObject.AddComponent<Text>();
            text.font = menuFont;
            text.fontSize = size;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 10;
            text.resizeTextMaxSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.text = value;
            text.raycastTarget = false;
            return text;
        }

        void ShowMenu()
        {
            shotAudio.Stop();
            bool canContinue = EchoHistory.Load(saveFileOverride) != null;
            mode = "menu";
            panel.gameObject.SetActive(false);
            titleScreen.transform.SetAsLastSibling();
            titleScreen.SetActive(true);
            SetTitleOptions(TitleLabels, canContinue, canContinue ? 0 : 1);
        }

        void SetTitleOptions(string[] labels, bool firstEnabled, int selected)
        {
            titleNotice.gameObject.SetActive(mode == "confirm");
            for (int i = 0; i < titleButtons.Length; i++)
            {
                titleButtons[i].gameObject.SetActive(i < labels.Length);
                if (i >= labels.Length) continue;
                titleButtons[i].interactable = i != 0 || firstEnabled;
                titleLabels[i].text = labels[i];
                titleIcons[i].uvRect = new Rect((mode == "confirm" ? i + 1 : i) / 3f, 0, 1f / 3f, 1);
            }
            FocusTitleOption(selected);
        }

        void FocusTitleOption(int index)
        {
            titleSelection = index;
            for (int i = 0; i < titleButtons.Length; i++)
            {
                var button = titleButtons[i];
                bool selected = i == index;
                var plate = selected && titlePlateSelected ? titlePlateSelected : titlePlate;
                button.image.sprite = plate;
                button.image.color = plate ? (button.interactable ? Color.white : new Color32(184, 187, 178, 255))
                    : selected ? new Color32(83, 66, 43, 255) : new Color32(56, 68, 63, 255);
                var ink = selected ? TitleAccent : button.interactable ? TitlePaper : TitleDisabled;
                titleLabels[i].color = ink;
                titleIcons[i].color = ink;
            }
        }

        void UpdateTitleMenu(Keyboard keyboard, bool enter)
        {
            if (mode == "confirm" && keyboard.escapeKey.wasPressedThisFrame)
            {
                ShowMenu();
                return;
            }

            int direction = keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame ? 1
                : keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame ? -1 : 0;
            if (direction != 0)
            {
                int count = mode == "confirm" ? ConfirmLabels.Length : TitleLabels.Length;
                int index = titleSelection;
                do index = (index + direction + count) % count;
                while (!titleButtons[index].interactable);
                FocusTitleOption(index);
            }
            if (enter) UseTitleOption(titleSelection);
        }

        void UseTitleOption(int index)
        {
            if (mode == "confirm")
            {
                if (index == 0) StartNewGame();
                else ShowMenu();
                return;
            }
            if (mode != "menu") return;

            switch (index)
            {
                case 0:
                    history = EchoHistory.Load(saveFileOverride);
                    if (history != null) BeginCycle();
                    else ShowMenu();
                    break;
                case 1:
                    if (titleButtons[0].interactable)
                    {
                        mode = "confirm";
                        SetTitleOptions(ConfirmLabels, true, 1);
                    }
                    else StartNewGame();
                    break;
                case 2:
                    QuitGame();
                    break;
            }
        }

        static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void StartNewGame()
        {
            history = new EchoHistory();
            Save();
            BeginCycle();
        }
    }
}
