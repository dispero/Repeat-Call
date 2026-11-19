using System.Collections.Generic;
using UnityEngine;

namespace Golovomka
{
    public sealed class Room02Layout : MonoBehaviour
    {
        public const string ScenePath = "Assets/Golovomka/Scenes/Room02.unity";

        public SpriteRenderer plateA, plateB;
        public BoxCollider2D doorA, doorB;
        public Transform entrance, note, exit, returnFromNext, furniture;

        public void CopyFurniture(List<Rect> destination)
        {
            foreach (var area in furniture.GetComponentsInChildren<BoxCollider2D>())
                if (area.enabled) destination.Add(ToRect(area.bounds));
        }

        public static Rect ToRect(Bounds bounds) => new Rect(bounds.min, bounds.size);
    }
}
