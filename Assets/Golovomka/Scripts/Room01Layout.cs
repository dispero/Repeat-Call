using UnityEngine;

namespace Golovomka
{
    public sealed class Room01Layout : MonoBehaviour
    {
        public const string ScenePath = "Assets/Golovomka/Scenes/Laboratory.unity";

        [Min(0)] public float nextRoomPreviewWidth = 3f;
        public SlidingDoor door;
        public SpriteRenderer plateOverlay, remoteSprite;
        public Transform entrance, remote, exit, returnFromNext, furniture;
        public BoxCollider2D doorway, doorBlocker;
    }
}
