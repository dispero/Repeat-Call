using UnityEngine;

namespace Golovomka
{
    public sealed class Room01Layout : MonoBehaviour
    {
        public const string ScenePath = "Assets/Golovomka/Scenes/Room01.unity";

        public SlidingDoor door;
        public SpriteRenderer plateOverlay, remoteSprite;
        public Transform entrance, remote, exit, returnFromNext, furniture;
        public BoxCollider2D walkArea, doorway;
    }
}
