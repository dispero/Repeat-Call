using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Golovomka
{
    public partial class EchoMvp
    {
        Camera roomCamera;
        readonly List<Vector2> worldLabelPoints = new List<Vector2>();

        // Максимальный равномерный масштаб, при котором вся комната видна.
        // Лаборатория учитывает запас над дальней стеной для головы героя.
        void FitRoomCamera()
        {
            if (!roomCamera) return;
            Vector2 size = zone == 0 ? new Vector2(1586 * StationScale, 992 * StationScale) : zone == 1 ? new Vector2(1599 * IntroScale, 984 * IntroScale) : new Vector2(24, 12.7f);
            float centerY = zone <= 1 ? 0 : .85f;
            roomCamera.rect = new Rect(0, 0, 1, 1);
            roomCamera.transform.position = new Vector3(0, centerY, -10);
            roomCamera.orthographicSize = Mathf.Max(size.y * .5f, size.x * .5f / Mathf.Max(.1f, roomCamera.aspect)) * 1.005f;
        }

        void LateUpdate()
        {
            FitRoomCamera();
            PositionWorldLabels();
        }

        void PositionWorldLabels()
        {
            if (!roomCamera) return;
            for (int i = 0; i < worldLabels.Count; i++)
            {
                if (!worldLabels[i]) continue;
                Vector3 viewport = roomCamera.WorldToViewportPoint(worldLabelPoints[i]);
                // Подписи предметов не пересекают верхние строки HUD и
                // нижнюю подсказку, которые теперь лежат поверх комнаты.
                viewport.y = Mathf.Clamp(viewport.y, .14f, .82f);
                var rect = worldLabels[i].rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(viewport.x, viewport.y);
                rect.anchoredPosition = Vector2.zero;
            }
        }

        void TopHUD(Text text, float left, float right, float y, float height)
        {
            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(left, 1); rect.anchorMax = new Vector2(right, 1);
            rect.pivot = new Vector2(.5f, 1); rect.sizeDelta = new Vector2(-32, height);
            rect.anchoredPosition = new Vector2(0, -y);
        }

        void BottomHUD(Text text, float y, float height)
        {
            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0, 0); rect.anchorMax = new Vector2(1, 0);
            rect.pivot = new Vector2(.5f, 0); rect.sizeDelta = new Vector2(-32, height);
            rect.anchoredPosition = new Vector2(0, y);
        }

        void ArrangeOverlayHUD()
        {
            TopHUD(heading, 0, .55f, 12, 36);
            TopHUD(objective, 0, .55f, 50, 38);
            TopHUD(status, .55f, 1, 14, 96);
            BottomHUD(prompt, 28, 55);
            BottomHUD(caution, 85, 30);
            var credits = canvas.transform.Find("Credits").GetComponent<Text>();
            BottomHUD(credits, 3, 23);
            foreach (var text in new[] { heading, objective, status, prompt, caution, credits })
            {
                var outline = text.gameObject.AddComponent<UnityEngine.UI.Outline>();
                outline.effectColor = new Color(0, 0, 0, .9f);
                outline.effectDistance = new Vector2(1, -1);
            }
            prompt.resizeTextForBestFit = true; prompt.resizeTextMinSize = 14; prompt.resizeTextMaxSize = 20;
        }
    }
}
