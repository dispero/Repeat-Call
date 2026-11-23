using UnityEngine;
using UnityEngine.UI;

namespace Golovomka
{
    public partial class EchoMvp
    {
        const string RemoteLine = "Откуда тут взялся пульт и зачем он нужен?";
        const string RangeLine = "Здесь я воспользуюсь пистолетом";
        const float SpeechLettersPerSecond = 35f;
        const float SpeechReadTime = 1.7f;
        const float SpeechPadding = 36f;

        ComicPanel speechBubble, pistolHelp;
        Text speechBody;
        string speechLine;
        float speechTime;
        int speechLetters;
        bool pistolHelpFollowsSpeech;
        bool remoteLineShown, rangeGuideShown;

        void CreateSpeechUI()
        {
            speechBubble = CreateComicPanel("SpeechBubble");
            speechBody = Text("SpeechText", speechBubble.transform, Vector2.zero, Vector2.zero, 22,
                Color.white, TextAnchor.MiddleCenter);
            speechBody.supportRichText = true;

            pistolHelp = CreateComicPanel("PistolHelp");
            pistolHelp.rectTransform.sizeDelta = new Vector2(448, 296);
            var title = Text("Heading", pistolHelp.transform, new Vector2(400, 36), new Vector2(0, 112),
                27, Color.white, TextAnchor.MiddleCenter);
            title.text = "Пистолет";
            title.fontStyle = FontStyle.Bold;

            var tile = new GameObject("PistolPreview", typeof(RectTransform), typeof(UnityEngine.UI.Image))
                .GetComponent<UnityEngine.UI.Image>();
            tile.transform.SetParent(pistolHelp.transform, false);
            tile.rectTransform.sizeDelta = new Vector2(138, 78);
            tile.rectTransform.anchoredPosition = new Vector2(0, 49);
            tile.color = new Color(.68f, .71f, .73f);
            tile.raycastTarget = false;
            var icon = new GameObject("Pistol", typeof(RectTransform), typeof(UnityEngine.UI.Image))
                .GetComponent<UnityEngine.UI.Image>();
            icon.transform.SetParent(tile.transform, false);
            icon.rectTransform.sizeDelta = new Vector2(94, 54);
            icon.sprite = Resources.Load<Sprite>("Art/BanditPistol");
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            tile.gameObject.SetActive(icon.sprite != null);

            var controls = Text("Controls", pistolHelp.transform, new Vector2(408, 80), new Vector2(0, -34),
                21, Color.white, TextAnchor.MiddleCenter);
            controls.supportRichText = true;
            controls.text = "<b>Q</b> — достать или убрать\n<b>ЛКМ / ПРОБЕЛ</b> — выстрел\nИван стреляет туда, куда смотрит";
            Text("Continue", pistolHelp.transform, new Vector2(408, 30), new Vector2(0, -108),
                18, Color.white, TextAnchor.MiddleCenter).text = "E/ENTER - продолжить";
        }

        ComicPanel CreateComicPanel(string name)
        {
            var window = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(ComicPanel))
                .GetComponent<ComicPanel>();
            window.transform.SetParent(canvas.transform, false);
            window.raycastTarget = false;
            window.gameObject.SetActive(false);
            return window;
        }

        void StartSpeech(string line, bool showPistolHelp = false)
        {
            speechLine = line;
            pistolHelpFollowsSpeech = showPistolHelp;
            speechTime = 0f;
            speechLetters = -1;
            mode = "speech";
            prompt.text = string.Empty;
            RevealSpeech(0);
            speechBubble.transform.SetAsLastSibling();
            speechBubble.gameObject.SetActive(true);
            PositionSpeechBubble();
        }

        void RevealSpeech(int count)
        {
            if (count == speechLetters) return;
            speechLetters = count;
            // Прозрачный остаток сохраняет переносы строк, пока открываются буквы.
            speechBody.text = count == speechLine.Length ? speechLine
                : speechLine.Substring(0, count) + "<color=#FFFFFF00>" + speechLine.Substring(count) + "</color>";
        }

        void UpdateSpeech(float dt, bool advance)
        {
            speechTime += dt;
            float revealDuration = speechLine.Length / SpeechLettersPerSecond;
            if (advance && speechLetters < speechLine.Length)
            {
                speechTime = revealDuration;
                RevealSpeech(speechLine.Length);
                return;
            }
            RevealSpeech(Mathf.Min(speechLine.Length, Mathf.FloorToInt(speechTime * SpeechLettersPerSecond)));
            if (!advance && speechTime < revealDuration + SpeechReadTime) return;

            speechBubble.gameObject.SetActive(false);
            if (pistolHelpFollowsSpeech)
            {
                mode = "pistol-help";
                pistolHelp.transform.SetAsLastSibling();
                pistolHelp.gameObject.SetActive(true);
            }
            else mode = "play";
        }

        void HideSpeechUI()
        {
            speechBubble.gameObject.SetActive(false);
            pistolHelp.gameObject.SetActive(false);
            if (mode == "speech" || mode == "pistol-help") mode = "play";
        }

        void PositionSpeechBubble()
        {
            if (!speechBubble || !speechBubble.gameObject.activeSelf) return;
            var canvasRect = (RectTransform)canvas.transform;
            Rect area = canvasRect.rect;
            var bounds = hero.bounds;
            var head = new Vector3(bounds.center.x, Mathf.Lerp(bounds.min.y, bounds.max.y, .78f), bounds.center.z);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, roomCamera.WorldToScreenPoint(head),
                null, out Vector2 anchor);

            const float margin = 18f;
            float width = Mathf.Min(310f, area.width - 2f * margin);
            var textRect = speechBody.rectTransform;
            textRect.sizeDelta = new Vector2(width - SpeechPadding, 180f);
            float height = Mathf.Max(64f, Mathf.Ceil(speechBody.preferredHeight) + 26f);
            speechBubble.rectTransform.sizeDelta = new Vector2(width, height);

            float halfWidth = width * .5f, halfHeight = height * .5f;
            float left = area.xMin + margin, right = area.xMax - margin;
            float bottom = area.yMin + 78f, top = area.yMax - 90f;
            float side = anchor.x + width < right ? 1f : -1f;
            float x = Mathf.Clamp(anchor.x + side * (halfWidth - 38f), left + halfWidth, right - halfWidth);
            bool below = anchor.y + 24f + height > top;
            float y = Mathf.Clamp(anchor.y + (below ? -1f : 1f) * (halfHeight + 24f),
                bottom + halfHeight, top - halfHeight);
            var center = new Vector2(x, y);
            speechBubble.rectTransform.anchoredPosition = center;
            float tipX = Mathf.Clamp(anchor.x - x, -halfWidth + 5f, halfWidth - 5f);
            speechBubble.PointAt(new Vector2(tipX, (below ? 1f : -1f) * (halfHeight + 21f)), below);
        }
    }
}
