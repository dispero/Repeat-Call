using UnityEngine;
using UnityEngine.Rendering;

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
        float openingTravel;
        float opening;
        float target;
        BoxCollider2D passage;
        public BoxCollider2D Passage => passage ? passage : (passage = GetComponent<BoxCollider2D>());

        void Awake()
        {
            leftClosed = leftLeaf.localPosition;
            rightClosed = rightLeaf.localPosition;
            openingTravel = travel;
            ConfigureMask();
        }

        void ConfigureMask()
        {
            var mask = GetComponentInChildren<SpriteMask>(true);
            if (!mask || !mask.sprite ||
                !leftLeaf.TryGetComponent<SpriteRenderer>(out var leftRenderer) ||
                !rightLeaf.TryGetComponent<SpriteRenderer>(out var rightRenderer)) return;

            var left = LocalSpriteBounds(leftRenderer);
            var right = LocalSpriteBounds(rightRenderer);
            var aperture = left;
            aperture.Encapsulate(right);
            var maskBounds = mask.sprite.bounds;
            var scale = new Vector3(aperture.size.x / maskBounds.size.x, aperture.size.y / maskBounds.size.y, 1);
            mask.transform.SetParent(transform, false);
            mask.transform.localRotation = Quaternion.identity;
            mask.transform.localScale = scale;
            mask.transform.localPosition = aperture.center - Vector3.Scale(maskBounds.center, scale);

            if (!TryGetComponent<SortingGroup>(out var group))
                group = gameObject.AddComponent<SortingGroup>();
            group.sortingLayerID = leftRenderer.sortingLayerID;
            group.sortingOrder = leftRenderer.sortingOrder;
            mask.isCustomRangeActive = false;
            mask.enabled = true;
            leftRenderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            rightRenderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

            float requiredTravel = Mathf.Max(left.max.x - aperture.min.x, aperture.max.x - right.min.x);
            openingTravel = Mathf.Max(travel, requiredTravel + .001f);
        }

        Bounds LocalSpriteBounds(SpriteRenderer renderer)
        {
            var bounds = renderer.localBounds;
            var matrix = transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            var result = new Bounds(matrix.MultiplyPoint3x4(bounds.center), Vector3.zero);
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
                result.Encapsulate(matrix.MultiplyPoint3x4(bounds.center +
                    new Vector3(x * bounds.extents.x, y * bounds.extents.y, 0)));
            return result;
        }

        public void SetOpen(bool open, bool immediate = false)
        {
            target = open ? 1 : 0;
            if (immediate) Apply(target);
        }

        public void SetPassage(bool open, Bounds feet, bool immediate = false)
        {
            SetOpen(open, immediate);
            var box = Passage;
            // Коллайдер остаётся доступен для проверки области, пока работает как триггер.
            bool passable = open || box.bounds.Intersects(feet);
            if (box.isTrigger != passable) box.isTrigger = passable;
        }

        void LateUpdate()
        {
            if (opening != target)
                Apply(Mathf.MoveTowards(opening, target, Time.deltaTime / duration));
        }

        void Apply(float amount)
        {
            opening = amount;
            Vector3 offset = Vector3.right * (openingTravel * Mathf.SmoothStep(0, 1, opening));
            leftLeaf.localPosition = leftClosed - offset;
            rightLeaf.localPosition = rightClosed + offset;
        }
    }
}
