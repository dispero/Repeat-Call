using System;
using UnityEngine;

namespace Golovomka
{
    public sealed class SequenceRoomLayout : MonoBehaviour
    {
        [Serializable]
        public class Gate
        {
            public SpriteRenderer plate;
            public SlidingDoor door;
            public bool IsConfigured => plate && door && door.Passage;

            public void Apply(bool pressed, bool cleared, Bounds feet, bool immediate)
            {
                door.SetPassage(pressed || cleared, feet, immediate);
                plate.color = pressed ? new Color(0, 0, 0, .15f) : Color.clear;
            }
        }

        public Gate first = new Gate(), second = new Gate();
        public bool IsConfigured => first.IsConfigured && second.IsConfigured;
    }
}
