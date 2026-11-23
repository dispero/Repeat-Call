using UnityEngine;
using UnityEngine.UI;

namespace Golovomka
{
    public partial class EchoMvp
    {
        const string RangeLine = "Здесь я воспользуюсь пистолетом";
        const float SpeechLettersPerSecond = 35f;
        const float SpeechReadTime = 1.7f;
        const float SpeechPadding = 36f;

        ComicPanel speechBubble;
        RectTransform pistolHelp;
        Text speechBody;
        string speechLine;
        float speechTime;
        int speechLetters;
        bool pistolHelpFollowsSpeech;
        bool rangeGuideShown;

        void CreateSpeechUI()
        {
            speechBubble = CreateComicPanel("SpeechBubble");
            speechBody = Text("SpeechText", speechBubble.transform, Vector2.zero, Vector2.zero, 22,
                Color.white, TextAnchor.MiddleCenter);
            speechBody.supportRichText = true;

            pistolHelp = new GameObject("PistolHelp", typeof(RectTransform)).GetComponent<RectTransform>();
            pistolHelp.SetParent(canvas.transform, false);
            pistolHelp.anchorMin = new Vector2(.11f, .09f);
            pistolHelp.anchorMax = new Vector2(.89f, .91f);
            pistolHelp.offsetMin = pistolHelp.offsetMax = Vector2.zero;

            var card = new GameObject("ControlsCard", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            card.transform.SetParent(pistolHelp, false);
            var image = card.GetComponent<RawImage>();
            image.texture = Resources.Load<Texture2D>("Menu/PistolHelp");
            image.raycastTarget = false;
            var fit = card.GetComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = (float)image.texture.width / image.texture.height;
            pistolHelp.gameObject.SetActive(false);
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
