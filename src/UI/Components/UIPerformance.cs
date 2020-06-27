using UnityEngine;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public static class UIPerformance
    {
        public const int ReducedFPSUIRate = 2;
        public const int LowFPSUIRate = 6;

        public static bool ShouldSkip(int everyNFrames = ReducedFPSUIRate)
        {
            return Time.frameCount % everyNFrames != 0;
        }

        public static bool ShouldRun(int everyNFrames = ReducedFPSUIRate)
        {
            return Time.frameCount % everyNFrames == 0;
        }
    }
}
