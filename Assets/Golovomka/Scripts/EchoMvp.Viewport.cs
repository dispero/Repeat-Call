using UnityEngine;
using UnityEngine.UI;

namespace Golovomka
{
    public partial class EchoMvp
    {
        [SerializeField] Camera roomCamera;
        [SerializeField] bool autoFitRoom = true;

        // В лаборатории расширяем кадр вправо, сохраняя левую границу комнаты.
        void FitRoomCamera()
        {
            if (!canvas || !autoFitRoom) return;
            Vector2 size = zone == 0 ? new Vector2(1586, 992) * (13.2f / 992f) : new Vector2(1599, 984) * .0145f;
            Vector3 center = zone == 0 ? station.transform.position : room01.transform.position;
            float previewWidth = zone == 0 ? 0 : room01.nextRoomPreviewWidth;
            float aspect = Mathf.Max(.1f, roomCamera.aspect);
            float roomHalfHeight = Mathf.Max(size.y * .5f, size.x * .5f / aspect);
            roomCamera.rect = new Rect(0, 0, 1, 1);
            roomCamera.transform.position = center + Vector3.right * (previewWidth * .5f) + Vector3.back * 10;
            roomCamera.orthographicSize = (roomHalfHeight + previewWidth * .5f / aspect) * 1.005f;
        }

        void LateUpdate()
        {
            FitRoomCamera();
            PositionStationExitLabel();
        }

        void PositionStationExitLabel()
        {
            if (!stationExitLabel || !stationExitLabel.enabled) return;
            Vector3 viewport = roomCamera.WorldToViewportPoint(station.exitLabel.position);
            // Подпись не пересекает строки HUD.
            viewport.y = Mathf.Clamp(viewport.y, .14f, .82f);
            var rect = stationExitLabel.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(viewport.x, viewport.y);
            rect.anchoredPosition = Vector2.zero;
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
