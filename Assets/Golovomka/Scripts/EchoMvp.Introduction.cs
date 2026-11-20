using System.Collections.Generic;
using UnityEngine;

namespace Golovomka
{
    public partial class EchoMvp
    {
        public Vector2 CurrentPlateA => room01.plateOverlay.transform.position;
        List<EchoTape> ActiveTapes => history.ForRoom(zone);

        void UpdateIntroductionVisuals(bool immediate = false)
        {
            room01.door.SetOpen(GateOpen, immediate);
            var feet = new Bounds(FeetCenter(position), FeetSize);
            // Если сигнал пропал в проёме, завершаем блокировку после выхода Ивана.
            room01.doorBlocker.enabled = !GateOpen && !room01.doorway.bounds.Intersects(feet);
            room01.plateOverlay.color = plateA ? new Color(0, 0, 0, .15f) : Color.clear;
            room01.remoteSprite.enabled = !history.remoteObtained;
        }

        Interaction IntroNearby()
        {
            Vector2 remote = room01.remote.position;
            if (!history.remoteObtained && Vector2.Distance(position, remote) < 1.65f)
                return history.firstReturnSeen ? Interaction.PickUpRemote : Interaction.InspectRemote;
            if (Vector2.Distance(position, room01.entrance.position) < .95f) return Interaction.ReturnToStation;
            return Interaction.None;
        }

        void UseIntroRemote()
        {
            if (!history.firstReturnSeen)
            {
                history.firstReturnSeen = true;
                ReturnToRoom();
            }
            else if (!history.remoteObtained)
            {
                history.remoteObtained = true;
                Save(); UpdateIntroductionVisuals();
            }
        }

        bool AtIntroductionExit()
        {
            Vector2 exit = room01.exit.position;
            return position.x > exit.x && position.y > exit.y && room01.doorway.OverlapPoint(position);
        }

        void ReturnToRoom()
        {
            if (recordingActive)
            {
                Record(); recording.duration = elapsed;
                if (elapsed > 0) history.Push(recording); else history.cycle++;
            }
            else history.cycle++;
            Save(); position = room01.entrance.position; facing = 0; mode = "play";
            panel.gameObject.SetActive(false);
            ResetRoomRecording();
            RefreshActors();
        }

        void FinishIntroduction()
        {
            history.clearedRooms = Mathf.Max(history.clearedRooms, 1);
            recordingActive = false;
            recording = new EchoTape();
            UpdateGhosts();
            mode = "done";
            HUD();
            Save();
            Panel("ПЕРВАЯ КОМНАТА ПРОЙДЕНА", "Иван открыл проход вглубь лаборатории.\n\nКонец демо.\n\nENTER — главное меню");
        }
    }
}
