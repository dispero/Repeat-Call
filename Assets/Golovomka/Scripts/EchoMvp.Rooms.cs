using UnityEngine;

namespace Golovomka
{
    public partial class EchoMvp
    {
        LaboratoryRoom.Passage FindPassage(bool automatic)
        {
            if (!currentRoom) return null;
            foreach (var passage in currentRoom.passages)
                if (passage.IsAutomatic == automatic && passage.Matches(position, history.remoteObtained))
                    return passage;
            return null;
        }

        void UsePassage(LaboratoryRoom.Passage passage)
        {
            var destination = passage.destination;
            if (destination == LaboratoryRoom.Destination.Room &&
                (!passage.targetRoom || !passage.targetRoom.isActiveAndEnabled))
            {
                if (!passage.finishDemoWithoutTarget) return;
                destination = LaboratoryRoom.Destination.DemoEnd;
            }
            if (passage.completesRoom)
            {
                history.clearedRooms = Mathf.Max(history.clearedRooms, zone);
                UpdateGhosts(immediateDoor: passage.IsAutomatic);
                Save();
            }
            switch (destination)
            {
                case LaboratoryRoom.Destination.Room:
                    ActivateRoom(passage.targetRoom, passage.arrival);
                    break;
                case LaboratoryRoom.Destination.Station:
                    if (!passage.completesRoom) Save();
                    BeginCycle();
                    break;
                case LaboratoryRoom.Destination.Message:
                    Dialog(passage.messageTitle, passage.message);
                    break;
                case LaboratoryRoom.Destination.DemoEnd:
                    recordingActive = false;
                    recording = new EchoTape();
                    UpdateGhosts();
                    mode = "done";
                    HUD();
                    Panel(passage.messageTitle, passage.message);
                    break;
            }
        }
    }
}
