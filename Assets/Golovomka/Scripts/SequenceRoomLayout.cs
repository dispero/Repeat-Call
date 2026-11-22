using System;
using UnityEngine;
using UnityEngine.Serialization;

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

            public void Apply(bool pressed, Bounds feet, bool immediate)
            {
                door.SetPassage(pressed, feet, immediate);
                ShowSignal(pressed);
            }

            public void ShowSignal(bool pressed)
            {
                plate.color = pressed ? new Color(0, 0, 0, .15f) : Color.clear;
            }
        }

        public Gate first = new Gate(), second = new Gate();
        [FormerlySerializedAs("requireBothCopies")]
        public bool requireBothPlates;
        public bool IsConfigured => first.IsConfigured && second.IsConfigured &&
            (requireBothPlates ? first.door == second.door : first.door != second.door);

        public void Apply(bool plateA, bool plateB, Bounds feet, bool immediate)
        {
            if (requireBothPlates)
            {
                first.ShowSignal(plateA);
                second.ShowSignal(plateB);
                first.door.SetPassage(plateA && plateB, feet, immediate);
                return;
            }

            first.Apply(plateA, feet, immediate);
            second.Apply(plateB, feet, immediate);
        }
    }
}
