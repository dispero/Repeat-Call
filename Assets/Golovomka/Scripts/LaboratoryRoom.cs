using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Golovomka
{
    [DisallowMultipleComponent]
    public sealed class LaboratoryRoom : MonoBehaviour
    {
        public const string ScenePath = "Assets/Golovomka/Scenes/Laboratory.unity";

        [Min(1)] public int roomNumber = 1;
        public Transform entrance;
        public BoxCollider2D cameraArea;
        public Vector2 cameraSize;
        [Min(0)] public float previewWidth;
        public bool disableRecording;
        [TextArea] public string objective;
        public Passage[] passages = Array.Empty<Passage>();

        public Bounds CameraBounds => cameraArea
            ? cameraArea.bounds
            : new Bounds(transform.position, cameraSize);

        public enum Destination { Room, Station, Message, DemoEnd }

        [Serializable]
        public sealed class Passage
        {
            public Transform marker;
            [Min(0)] public float interactionRadius = 1;
            public bool approachFromRight;
            public float minimumXOffset;
            public SlidingDoor crossingDoor;
            public bool crossToRight;
            public bool requiresRemote;
            [FormerlySerializedAs("completesRoom")] public bool setsReturnPoint;
            public Destination destination;
            public LaboratoryRoom targetRoom;
            public bool finishDemoWithoutTarget;
            public Transform arrival;
            public string label;
            public string messageTitle;
            [TextArea] public string message;

            public bool IsAutomatic => crossingDoor;

            public bool Matches(Vector2 position, bool hasRemote)
            {
                if (IsAutomatic || !marker || (requiresRemote && !hasRemote)) return false;
                Vector2 point = marker.position;
                return (!approachFromRight || position.x >= point.x + minimumXOffset) &&
                    Vector2.Distance(position, point) < interactionRadius;
            }

            public bool Crossed(Vector2 from, Vector2 to, bool hasRemote)
            {
                if (!crossingDoor || ((requiresRemote || setsReturnPoint) && !hasRemote)) return false;
                var doorway = crossingDoor.Passage;
                if (!doorway || !doorway.isTrigger) return false;
                float threshold = doorway.bounds.center.x;
                bool crossed = crossToRight ? from.x <= threshold && to.x > threshold
                    : from.x >= threshold && to.x < threshold;
                // Горизонтальный шаг контроллера выполняется до вертикального.
                return crossed && doorway.OverlapPoint(new Vector2(threshold, from.y));
            }
        }
    }
}
