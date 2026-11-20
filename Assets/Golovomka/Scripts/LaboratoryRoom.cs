using System;
using UnityEngine;

namespace Golovomka
{
    [DisallowMultipleComponent]
    public sealed class LaboratoryRoom : MonoBehaviour
    {
        public const string ScenePath = "Assets/Golovomka/Scenes/Laboratory.unity";
        public const int MaxCopies = 2;

        [Min(1)] public int roomNumber = 1;
        public Transform entrance;
        public BoxCollider2D cameraArea;
        public Vector2 cameraSize;
        [Min(0)] public float previewWidth;
        [Range(1, MaxCopies)] public int copyLimit = 1;
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
            public bool requiresRemote;
            public bool completesRoom;
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
                if (!marker || (requiresRemote && !hasRemote)) return false;
                Vector2 point = marker.position;
                if (crossingDoor)
                    return position.x > point.x && position.y > point.y &&
                        crossingDoor.Passage.OverlapPoint(position);
                return (!approachFromRight || position.x >= point.x + minimumXOffset) &&
                    Vector2.Distance(position, point) < interactionRadius;
            }
        }
    }
}
