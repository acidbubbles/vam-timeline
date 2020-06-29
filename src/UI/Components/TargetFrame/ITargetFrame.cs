using UnityEngine;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public interface ITargetFrame
    {
        GameObject gameObject { get; }
        void SetTime(float time, bool stopped);
        void ToggleKeyframe(bool enable);
    }
}
