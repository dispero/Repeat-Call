using System;
using UnityEngine;

namespace Golovomka
{
    [DisallowMultipleComponent, RequireComponent(typeof(SpriteRenderer))]
    public sealed class CharacterWalk : MonoBehaviour
    {
        const int CardinalDirectionCount = 4;
        const int CardinalFrameCount = CardinalDirectionCount * CharacterWalkCycle.FramesPerDirection;
        const int DiagonalPoseFrameCount = CardinalFrameCount + CardinalDirectionCount;

        [SerializeField, Tooltip("По четыре кадра: вправо, влево, назад, вперёд. Затем, если есть, по одной позе или по четыре кадра для диагоналей: вверх-вправо, вверх-влево, вниз-влево, вниз-вправо.")]
        Sprite[] frames = Array.Empty<Sprite>();

        [SerializeField] Sprite[] armedWalk = Array.Empty<Sprite>();
        [SerializeField] Sprite[] armedShoot = Array.Empty<Sprite>();
        [SerializeField] Vector2[] armedMuzzles = Array.Empty<Vector2>();
        [SerializeField] Sprite[] deathFrames = Array.Empty<Sprite>();
        [SerializeField] Sprite armedLeftReady;
        [SerializeField] Sprite[] armedLeftShoot = Array.Empty<Sprite>();
        [SerializeField] Vector2 armedLeftMuzzle;
        bool HasLeftArmedAnimation => armedLeftReady && armedLeftShoot.Length == 4 &&
            Array.TrueForAll(armedLeftShoot, frame => frame);

        internal bool HasDeathAnimation => deathFrames.Length == 12 && Array.TrueForAll(deathFrames, frame => frame);
        internal const float DeathAnimationDuration = .54f;
        internal Sprite GetDeathFrame(int facing, float time) =>
            deathFrames[facing * 3 + Mathf.Clamp(Mathf.FloorToInt(time / (DeathAnimationDuration / 3f)), 0, 2)];

        internal bool HasArmedAnimation => HasLeftArmedAnimation || armedWalk.Length == CardinalFrameCount && armedShoot.Length == CardinalFrameCount &&
            armedMuzzles.Length == CardinalDirectionCount && Array.TrueForAll(armedWalk, frame => frame) &&
            Array.TrueForAll(armedShoot, frame => frame);

        internal Sprite GetArmedWalkFrame(int facing, bool moving, float time)
        {
            if (HasLeftArmedAnimation)
                return facing == 1 ? armedLeftReady : GetFrame(facing, moving, time);
            return armedWalk[CharacterWalkCycle.FrameIndex(facing, CardinalDirectionCount, moving, time)];
        }

        internal Sprite GetArmedShootFrame(int facing, int phase)
        {
            if (HasLeftArmedAnimation)
                return facing == 1 ? armedLeftShoot[phase] : GetFrame(facing, false, 0);
            return armedShoot[facing * CharacterWalkCycle.FramesPerDirection + phase];
        }

        internal Vector2 GetArmedMuzzle(int facing) =>
            transform.TransformPoint(HasLeftArmedAnimation ? armedLeftMuzzle : armedMuzzles[facing]);

        SpriteRenderer spriteRenderer;
        Vector2 previousPosition;
        float walkTime;
        int direction;

        bool HasDiagonalPosesOnly => frames.Length == DiagonalPoseFrameCount;
        int DirectionCount => HasDiagonalPosesOnly ? 8 : frames.Length / CharacterWalkCycle.FramesPerDirection;

        internal bool IsConfigured =>
            (frames.Length == CardinalFrameCount || HasDiagonalPosesOnly || frames.Length == 2 * CardinalFrameCount) &&
            Array.TrueForAll(frames, frame => frame);

        internal int GetFacing(Vector2 look, int fallback)
        {
            if (DirectionCount != 8 || look == Vector2.zero) return fallback;
            return CharacterWalkCycle.Octant(look) switch
            {
                0 => 0, 1 => 4, 2 => 2, 3 => 5,
                4 => 1, 5 => 6, 6 => 3, _ => 7
            };
        }

        internal Sprite GetFrame(int facing, bool moving, float time)
        {
            if (HasDiagonalPosesOnly && facing >= CardinalDirectionCount)
                return frames[CardinalFrameCount + Mathf.Min(facing, DirectionCount - 1) - CardinalDirectionCount];

            return frames[CharacterWalkCycle.FrameIndex(facing, DirectionCount, moving, time)];
        }

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
                direction = GetFacing(displacement, useHorizontal ? sideways : forwards);
            }
            else
                walkTime = 0;

            spriteRenderer.sprite = GetFrame(direction, moving, walkTime);
            if (moving) walkTime += Time.deltaTime;
        }
    }
}
