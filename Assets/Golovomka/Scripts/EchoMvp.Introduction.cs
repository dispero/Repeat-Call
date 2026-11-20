using System.Collections.Generic;
using UnityEngine;

namespace Golovomka
{
    public partial class EchoMvp
    {
        public Vector2 CurrentPlateA => currentSequence
            ? currentSequence.first.plate.transform.position
            : room01.plateOverlay.transform.position;
        List<EchoTape> ActiveTapes => history.ForRoom(zone);

        void UpdateIntroductionVisuals(bool immediate = false)
        {
            var feet = new Bounds(FeetCenter(position), FeetSize);
            room01.door.SetPassage(GateOpen, feet, immediate);
            room01.plateOverlay.color = plateA ? new Color(0, 0, 0, .15f) : Color.clear;
            room01.remoteSprite.enabled = !history.remoteObtained;
        }

        Interaction IntroNearby()
        {
            Vector2 remote = room01.remote.position;
            if (!history.remoteObtained && Vector2.Distance(position, remote) < 1.65f)
                return history.firstReturnSeen ? Interaction.PickUpRemote : Interaction.InspectRemote;
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

        void ReturnToRoom()
        {
            if (recordingActive)
            {
                Record(); recording.duration = elapsed;
                if (elapsed > 0) history.Push(recording, currentRoom.copyLimit); else history.cycle++;
            }
            else history.cycle++;
            Save(); position = currentRoom.entrance.position; facing = 0; mode = "play";
            panel.gameObject.SetActive(false);
            ResetRoomRecording();
            FitRoomCamera(snap: true);
            RefreshActors();
        }

    }
}
