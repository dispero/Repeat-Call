using UnityEngine;

namespace Golovomka
{
    public sealed class SlidingDoor : MonoBehaviour
    {
        [SerializeField] Transform leftLeaf;
        [SerializeField] Transform rightLeaf;
        [SerializeField, Min(0)] float travel = .65f;
        [SerializeField, Min(.01f)] float duration = .24f;

        Vector3 leftClosed;
        Vector3 rightClosed;
        float opening;
        float target;

        void Awake()
        {
            leftClosed = leftLeaf.localPosition;
            rightClosed = rightLeaf.localPosition;
        }

        public void SetOpen(bool open, bool immediate = false)
        {
            target = open ? 1 : 0;
            if (immediate) Apply(target);
        }

        void LateUpdate()
        {
            if (opening != target)
                Apply(Mathf.MoveTowards(opening, target, Time.deltaTime / duration));
        }

        void Apply(float amount)
        {
            opening = amount;
            Vector3 offset = Vector3.right * (travel * Mathf.SmoothStep(0, 1, opening));
            leftLeaf.localPosition = leftClosed - offset;
            rightLeaf.localPosition = rightClosed + offset;
        }
    }
}
