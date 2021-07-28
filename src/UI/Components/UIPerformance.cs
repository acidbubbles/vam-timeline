using UnityEngine;

namespace VamTimeline
{
    public static class UIPerformance
    {
        public const int HighFrequency = 2;
        public const int LowFrequency = 6;

        public static bool ShouldSkip(int everyNFrames)
        {
            return Time.frameCount % everyNFrames != 0;
        }

        public static bool ShouldRun(int everyNFrames)
        {
            return Time.frameCount % everyNFrames == 0;
        }
    }
}
