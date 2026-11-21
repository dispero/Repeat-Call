using System;
using UnityEngine;

namespace Golovomka
{
    [DisallowMultipleComponent, RequireComponent(typeof(SpriteRenderer))]
    public sealed class CharacterWalk : MonoBehaviour
    {
        [SerializeField, Tooltip("Четыре кадра на направление: вправо, влево, назад, вперёд.")]
        Sprite[] frames = Array.Empty<Sprite>();

        SpriteRenderer spriteRenderer;
        Vector2 previousPosition;
        float walkTime;
        int direction;

        internal bool IsConfigured => frames.Length == CharacterWalkCycle.FrameCount && Array.TrueForAll(frames, frame => frame);

        internal Sprite GetFrame(int facing, bool moving, float time) => frames[CharacterWalkCycle.FrameIndex(facing, moving, time)];

        void OnEnable()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            previousPosition = transform.position;
            walkTime = 0;
            direction = 0;
            if (!IsConfigured)
            {
                enabled = false;
                return;
            }
            spriteRenderer.sprite = frames[0];
        }

        void LateUpdate()
        {
            Vector2 position = transform.position;
            Vector2 displacement = position - previousPosition;
            previousPosition = position;
            bool moving = displacement.sqrMagnitude > 0.00000001f;
            if (moving)
            {
                float horizontal = Mathf.Abs(displacement.x);
                float vertical = Mathf.Abs(displacement.y);
                // На диагонали сохраняем подходящий прежний ракурс, чтобы он не дрожал.
                int sideways = displacement.x > 0 ? 0 : 1;
                int forwards = displacement.y > 0 ? 2 : 3;
                bool useHorizontal = Mathf.Approximately(horizontal, vertical) ? direction == sideways : horizontal > vertical;
                direction = useHorizontal ? sideways : forwards;
            }
            else
                walkTime = 0;

            spriteRenderer.sprite = GetFrame(direction, moving, walkTime);
            if (moving) walkTime += Time.deltaTime;
        }
    }
}
