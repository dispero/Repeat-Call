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
        // Внешние края верхней и нижней стен коридора в мировых координатах.
        const float LaboratoryWallTop = 5.64f;
        const float LaboratoryWallBottom = -7.36f;
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

            if (currentFinale)
            {
                var frame = currentRoom.CameraBounds;
                roomCamera.orthographicSize = Mathf.Max(frame.extents.y, frame.extents.x / aspect) * 1.015f;
                roomCamera.transform.position = frame.center + Vector3.back * 10;
                laboratoryCameraVelocity = 0;
                return;
            }

            var bounds = laboratoryCameraBounds;
            roomCamera.orthographicSize = (LaboratoryWallTop - LaboratoryWallBottom) * .5f;
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
            float cameraY = (LaboratoryWallTop + LaboratoryWallBottom) * .5f;
            roomCamera.transform.position = new Vector3(cameraX, cameraY, bounds.center.z - 10);
        }

        void LateUpdate()
        {
            FitRoomCamera();
            UpdateStationExitLabel();
            if (returnHint) returnHint.enabled = ShouldShowReturnHint();
            if (currentSequence) currentSequence.UpdateCabinetPlant(hero);
            UpdatePhoneRing();
        }

        bool ShouldShowReturnHint()
        {
            if (mode != "play" || !recordingActive || !currentRoom || !history.remoteObtained || room2PreviewDoor)
                return false;

            if (currentRoom == introductionRoom) return true;
            if (currentSequence) return elapsed >= 30f && history.returnRoom <= zone;
            if (currentButtons) return currentButtons.CanRetry(elapsed);
            return currentRange && currentRange.ShouldShowRetryHint(elapsed);
        }

        void UpdateStationExitLabel()
        {
            if (!stationExitLabel) return;
            stationExitLabel.enabled = !currentRoom && phoneRead && mode == "play";
            if (!stationExitLabel.enabled) return;
            Vector3 viewport = roomCamera.WorldToViewportPoint(station.exitLabel.position);
            var rect = stationExitLabel.rectTransform;
            // Подпись остаётся в безопасной области экрана.
            Vector2 canvasSize = ((RectTransform)canvas.transform).rect.size;
            float horizontalMargin = Mathf.Min(.5f, (rect.rect.width * .5f + 16f) / canvasSize.x);
            float verticalMargin = Mathf.Min(.5f, (rect.rect.height * .5f + 8f) / canvasSize.y);
            viewport.x = Mathf.Clamp(viewport.x, horizontalMargin, 1f - horizontalMargin);
            viewport.y = Mathf.Clamp(viewport.y, verticalMargin, 1f - verticalMargin);
            rect.anchorMin = rect.anchorMax = new Vector2(viewport.x, viewport.y);
            rect.anchoredPosition = Vector2.zero;
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
            BottomHUD(prompt, 28, 55);
            BottomHUD(returnHint, 6, 28);
            BottomHUD(caution, 85, 30);
            foreach (var text in new[] { prompt, returnHint, caution, stationExitLabel })
            {
                var outline = text.gameObject.AddComponent<UnityEngine.UI.Outline>();
                outline.effectColor = new Color(0, 0, 0, .9f);
                outline.effectDistance = new Vector2(1, -1);
            }
            prompt.resizeTextForBestFit = true; prompt.resizeTextMinSize = 14; prompt.resizeTextMaxSize = 20;
        }
    }
}
