using UnityEngine;

namespace Golovomka
{
    public partial class EchoMvp
    {
        sealed class DepartedEcho
        {
            public EchoTape tape;
            public SpriteRenderer sprite;
            public TargetRangeRoom range;
            public EchoFrame frame;
            public float time;
        }

        readonly DepartedEcho[] departedEchoes = new DepartedEcho[EchoHistory.MaxRoomNumber + 1];

        void KeepDepartedEcho()
        {
            var tape = ActiveTape;
            if (!ghost.enabled || tape == null || !tape.Sample(elapsed, out var frame) || frame.zone != zone) return;

            departedEchoes[zone] = new DepartedEcho
            {
                tape = tape, time = elapsed, frame = frame, range = currentRange,
                sprite = Instantiate(ghost, transform)
            };
        }

        void AdvanceDepartedEchoes(float deltaTime)
        {
            for (int room = 1; room < departedEchoes.Length; room++)
            {
                var echo = departedEchoes[room];
                if (echo == null) continue;
                echo.time += deltaTime;
                if (!echo.tape.Sample(echo.time, out echo.frame) || echo.frame.zone != room)
                {
                    ClearDepartedEcho(room);
                    continue;
                }

                bool moving = DrawEcho(echo.sprite, echo.tape, echo.frame, echo.time);
                if (echo.range && echo.frame.pistolDrawn)
                    echo.range.ShowDepartedEchoPose(echo.sprite, OfficerDirection(echo.frame.facing), moving, echo.time);
            }
        }

        void ClearDepartedEcho(int room)
        {
            var echo = departedEchoes[room];
            if (echo == null) return;
            echo.sprite.enabled = false;
            Destroy(echo.sprite.gameObject);
            departedEchoes[room] = null;
        }

        bool DepartedEchoOnPlate(int room, Vector2 plate) =>
            departedEchoes[room] is DepartedEcho echo && EchoHistory.OnPlate(echo.frame, plate);

        bool DrawEcho(SpriteRenderer sprite, EchoTape tape, EchoFrame frame, float time)
        {
            var copyPosition = new Vector2(frame.x, frame.y);
            bool moving = tape.Sample(Mathf.Max(0, time - .08f), out var previous)
                && previous.zone == frame.zone
                && (copyPosition - new Vector2(previous.x, previous.y)).sqrMagnitude > .0001f;
            Place(sprite, copyPosition);
            sprite.sprite = officerWalk.GetFrame(frame.facing, moving, time);
            return moving;
        }

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
                case LaboratoryRoom.Destination.Message:
                    Dialog(passage.messageTitle, passage.message);
                    break;
                case LaboratoryRoom.Destination.DemoEnd:
                    recordingActive = false;
                    recording = new EchoTape();
                    UpdateGhosts();
                    mode = "done";
                    Panel(passage.messageTitle, passage.message);
                    break;
            }
        }
    }
}
