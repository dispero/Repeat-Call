using UnityEngine;

namespace Golovomka
{
    public partial class EchoMvp
    {
        LaboratoryRoom.Passage FindPassage()
        {
            if (!currentRoom) return null;
            foreach (var passage in currentRoom.passages)
                if (passage.Matches(position, history.remoteObtained))
                    return passage;
            return null;
        }

        LaboratoryRoom.Passage FindCrossing(Vector2 previousPosition)
        {
            if (!currentRoom) return null;
            foreach (var passage in currentRoom.passages)
                if (passage.Crossed(previousPosition, position, history.remoteObtained))
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
            if (passage.IsAutomatic && passage.setsReturnPoint && destination == LaboratoryRoom.Destination.Room &&
                passage.targetRoom.roomNumber > history.returnRoom)
            {
                history.returnRoom = passage.targetRoom.roomNumber;
                Save();
            }
            switch (destination)
            {
                case LaboratoryRoom.Destination.Room:
                    ActivateRoom(passage.targetRoom, passage.arrival, keepPosition: passage.IsAutomatic);
                    break;
                case LaboratoryRoom.Destination.Station:
                    Save();
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
