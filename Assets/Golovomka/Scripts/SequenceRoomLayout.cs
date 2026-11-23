using System;
using UnityEngine;
using UnityEngine.Rendering;

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
        [SerializeField] SpriteRenderer cabinetPlant;
        [SerializeField] SortingGroup cabinet;
        public bool IsConfigured => first.IsConfigured && second.IsConfigured && first.door != second.door;

        public void UpdateCabinetPlant(SpriteRenderer actor)
        {
            if (!cabinetPlant || !cabinet) return;
            bool covered = false;
            if (actor && actor.sortingOrder > cabinet.sortingOrder)
            {
                var body = actor.bounds;
                var plant = cabinetPlant.bounds;
                float headHalfWidth = body.extents.x * .55f;
                float headBottom = body.min.y + body.size.y * .7f;
                covered = Mathf.Abs(body.center.x - plant.center.x) < headHalfWidth + plant.extents.x &&
                    headBottom < plant.max.y && body.max.y > plant.min.y;
            }
            cabinetPlant.enabled = !covered;
        }

        public void Apply(bool plateA, bool plateB, Bounds feet, bool immediate)
        {
            first.Apply(plateA, feet, immediate);
            second.Apply(plateB, feet, immediate);
        }
    }
}
