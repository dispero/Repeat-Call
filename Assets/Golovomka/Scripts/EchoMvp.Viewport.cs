using UnityEngine;
using UnityEngine.UI;

namespace Golovomka
{
    public partial class EchoMvp
    {
        [SerializeField] Camera roomCamera;
        [SerializeField] bool autoFitRoom = true;
        [SerializeField, Range(.1f, .8f)] float laboratoryDeadZoneWidth = .4f;
        [SerializeField, Min(.01f)] float laboratoryCameraSmoothTime = .22f;
        Bounds laboratoryCameraBounds;
        float laboratoryCameraVelocity;
        const float DoorPreviewPan = .55f;
        const float DoorPreviewHold = .65f;
        SlidingDoor room2PreviewDoor;
        float doorPreviewTime;
        float doorPreviewStartX;

        void BeginRoom2DoorPreview(SlidingDoor door)
        {
            room2PreviewDoor = door;
            doorPreviewTime = 0f;
            doorPreviewStartX = roomCamera.transform.position.x;
            laboratoryCameraVelocity = 0f;
        }

        void AdvanceRoom2DoorPreview(float deltaTime)
        {
            float previous = doorPreviewTime;
            doorPreviewTime += deltaTime;
            if (previous < DoorPreviewPan && doorPreviewTime >= DoorPreviewPan)
                room2PreviewDoor.SetOpen(true);
            if (doorPreviewTime < 2 * DoorPreviewPan + DoorPreviewHold) return;

            FitRoomCamera();
            room2PreviewDoor = null;
            history.room2DoorPreviewSeen = true;
            Save();
        }

        float Room2PreviewCameraX(float minX, float maxX)
        {
            float doorX = Mathf.Clamp(room2PreviewDoor.Passage.bounds.center.x, minX, maxX);
            float atDoor = Mathf.Min(doorPreviewTime / DoorPreviewPan,
                (2 * DoorPreviewPan + DoorPreviewHold - doorPreviewTime) / DoorPreviewPan);
            return Mathf.SmoothStep(Mathf.Clamp(doorPreviewStartX, minX, maxX), doorX, atDoor);
        }

        void CacheLaboratoryCameraBounds()
        {
            laboratoryCameraBounds = currentRoom.CameraBounds;
            foreach (var root in laboratoryRoots)
            foreach (var room in root.GetComponentsInChildren<LaboratoryRoom>())
            {
                if (!room.isActiveAndEnabled) continue;
                var bounds = room.CameraBounds;
                laboratoryCameraBounds.Encapsulate(bounds.min);
                laboratoryCameraBounds.Encapsulate(bounds.max + Vector3.right * room.previewWidth);
            }
        }

        void FitRoomCamera(bool snap = false)
        {
            if (!canvas || !autoFitRoom) return;
            float aspect = Mathf.Max(.1f, roomCamera.aspect);
            roomCamera.rect = new Rect(0, 0, 1, 1);
            if (!currentRoom)
            {
                var size = new Vector2(1586, 992) * (13.2f / 992f);
                roomCamera.orthographicSize = Mathf.Max(size.y * .5f, size.x * .5f / aspect) * 1.005f;
                roomCamera.transform.position = station.transform.position + Vector3.back * 10;
                laboratoryCameraVelocity = 0;
                return;
            }

            var bounds = laboratoryCameraBounds;
            roomCamera.orthographicSize = Mathf.Max(.01f, bounds.extents.y) * 1.005f;
            float halfWidth = roomCamera.orthographicSize * aspect;
            float minX = Mathf.Min(bounds.center.x, bounds.min.x + halfWidth);
            float maxX = Mathf.Max(bounds.center.x, bounds.max.x - halfWidth);
            float cameraX = Mathf.Clamp(roomCamera.transform.position.x, minX, maxX);
            if (room2PreviewDoor)
            {
                cameraX = Room2PreviewCameraX(minX, maxX);
            }
            else if (snap)
            {
                laboratoryCameraVelocity = 0;
                cameraX = Mathf.Clamp(position.x, minX, maxX);
            }
            else
            {
                float deadZone = halfWidth * laboratoryDeadZoneWidth;
                float target = Mathf.Clamp(cameraX, position.x - deadZone, position.x + deadZone);
                target = Mathf.Clamp(target, minX, maxX);
                if (Mathf.Approximately(cameraX, target))
                    laboratoryCameraVelocity = 0;
                else
                    cameraX = Mathf.Clamp(Mathf.SmoothDamp(cameraX, target, ref laboratoryCameraVelocity,
                        laboratoryCameraSmoothTime), minX, maxX);
            }
            roomCamera.transform.position = new Vector3(cameraX, bounds.center.y, bounds.center.z - 10);
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
