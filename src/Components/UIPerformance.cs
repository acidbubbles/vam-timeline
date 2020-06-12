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
        public static bool ShouldSkip()
        {
            return Time.frameCount % 2 != 0;
        }
    }
}
