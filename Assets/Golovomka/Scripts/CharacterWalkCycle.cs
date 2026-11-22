using UnityEngine;

namespace Golovomka
{
    internal static class CharacterWalkCycle
    {
        const float StepsPerSecond = 3f;
        public const int FramesPerDirection = 4;
        public const int WalkingFrameCount = 3;

        public static int WalkingFrame(float time)
        {
            int phase = (int)(time * (StepsPerSecond * 2f)) % FramesPerDirection;
            return phase == 3 ? 1 : phase;
        }

        public static int FrameIndex(int direction, int directionCount, bool moving, float time)
        {
            // Покой — отдельный кадр. Два шага: первая опора, проход, вторая опора, проход.
            int phase = moving ? WalkingFrame(time) + 1 : 0;
            return Mathf.Clamp(direction, 0, directionCount - 1) * FramesPerDirection + phase;
        }

        public static int Octant(Vector2 direction) =>
            Mathf.RoundToInt(Mathf.Atan2(direction.y, direction.x) * (4f / Mathf.PI)) & 7;
    }
}
