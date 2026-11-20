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
                ShowSignal(pressed);
            }

            public void ShowSignal(bool pressed)
            {
                plate.color = pressed ? new Color(0, 0, 0, .15f) : Color.clear;
            }
        }

        public Gate first = new Gate(), second = new Gate();
        public bool requireBothCopies;
        public bool IsConfigured => first.IsConfigured && second.IsConfigured &&
            (requireBothCopies ? first.door == second.door : first.door != second.door);

        public void Apply(bool plateA, bool plateB, bool cleared, Bounds feet, bool immediate)
        {
            if (requireBothCopies)
            {
                first.ShowSignal(plateA);
                second.ShowSignal(plateB);
                first.door.SetPassage(cleared || (plateA && plateB), feet, immediate);
                return;
            }

            first.Apply(plateA, cleared, feet, immediate);
            second.Apply(plateB, cleared, feet, immediate);
        }
    }
}
