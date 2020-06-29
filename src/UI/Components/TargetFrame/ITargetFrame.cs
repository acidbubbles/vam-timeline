using UnityEngine;

namespace VamTimeline
{
    public interface ITargetFrame
    {
        GameObject gameObject { get; }
        void SetTime(float time, bool stopped);
        void ToggleKeyframe(bool enable);
    }
}
