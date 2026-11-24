using UnityEngine;

namespace Golovomka
{
    public partial class EchoMvp
    {
        public Vector2 CurrentPlateA => currentSequence
            ? currentSequence.first.plate.transform.position
            : room01.plateOverlay.transform.position;
        EchoTape ActiveTape
        {
            get
            {
                var tape = history.ForRoom(zone);
                if (currentFinale && (tape?.room != zone || tape.shots.Exists(shot => !TargetRangeRoom.IsCardinalShot(shot.direction))))
                    return null;
                // Старая серия с диагональными попаданиями может требовать другой позиции для выстрела.
                return currentRange && tape != null && tape.shots.Exists(shot => !TargetRangeRoom.IsCardinalShot(shot.direction))
                    ? null : tape;
            }
        }

        void UpdateIntroductionVisuals(bool immediate = false)
        {
            var feet = new Bounds(FeetCenter(position), FeetSize);
            bool pressed = currentRoom == introductionRoom ? plateA :
                DepartedEchoOnPlate(introductionRoom.roomNumber, room01.plateOverlay.transform.position);
            room01.door.SetPassage(pressed, feet, immediate);
            room01.plateOverlay.color = pressed ? new Color(0, 0, 0, .15f) : Color.clear;
            room01.remoteSprite.enabled = !history.remoteObtained;
        }

        Interaction IntroNearby()
        {
            Vector2 remote = room01.remote.position;
            if (!history.remoteObtained && Vector2.Distance(position, remote) < 1.65f)
                return Interaction.PickUpRemote;
            return Interaction.None;
        }

        void UseIntroRemote()
        {
            if (currentRoom != introductionRoom || IntroNearby() != Interaction.PickUpRemote) return;

            history.remoteObtained = true;
            Save();
            UpdateIntroductionVisuals();
            prompt.text = string.Empty;
            if (!remoteHelpArt) return;

            mode = "dialog";
            ShowIllustratedCard(remoteHelpArt, scale: .8f);
        }

        void ReturnToRoom()
        {
            if (recordingActive)
            {
                // Последнее нажатие может иметь ту же метку времени, что и следующий кадр с R.
                if (currentButtons && recording.buttonPresses.Count > 0 &&
                    recording.buttonPresses[recording.buttonPresses.Count - 1].time >= elapsed)
                    elapsed += .001f;
                if ((currentRange || currentFinale) && recording.shots.Count > 0 &&
                    recording.shots[recording.shots.Count - 1].time >= elapsed)
                    elapsed += .001f;
                if (currentButtons && recording.buttonPresses.Count > 0 &&
                    recording.buttonPresses[recording.buttonPresses.Count - 1].time >= elapsed)
                    elapsed += .001f;
                Record(); recording.duration = elapsed;
                if (elapsed > 0 &&
                    (!currentRange || currentRange.HasCompleteRecording) &&
                    (!currentFinale || currentFinale.HasRecording(recording)))
                    history.Push(recording);
                else history.cycle++;
            }
            else history.cycle++;
            Save();
            facing = 0;
            mode = "play";
            panel.gameObject.SetActive(false);
            var returnRoom = rooms.TryGetValue(history.returnRoom, out var savedRoom) && savedRoom.isActiveAndEnabled
                ? savedRoom : introductionRoom;
            ActivateRoom(returnRoom, returning: true);
            if (teleportAudio.clip) teleportAudio.Play();
            FitRoomCamera(snap: true);
        }

    }
}
