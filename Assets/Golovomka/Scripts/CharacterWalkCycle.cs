using UnityEngine;

namespace Golovomka
{
    internal static class CharacterWalkCycle
    {
        const float StepsPerSecond = 3f;
        const int FramesPerDirection = 4;
        public const int FrameCount = 4 * FramesPerDirection;

        public static int FrameIndex(int direction, bool moving, float time)
        {
            // Покой — отдельный кадр. Два шага: первая опора, проход, вторая опора, проход.
            int phaseIndex = (int)(time * (StepsPerSecond * 2f)) % FramesPerDirection;
            int phase = moving ? (phaseIndex == 3 ? 2 : phaseIndex + 1) : 0;
            return Mathf.Clamp(direction, 0, 3) * FramesPerDirection + phase;
        }
    }
}
