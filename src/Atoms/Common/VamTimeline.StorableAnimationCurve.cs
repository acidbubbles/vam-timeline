using CurveEditor;
using UnityEngine;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class StorableAnimationCurve : IStorableAnimationCurve
    {
        public AnimationCurve val { get; set; }

        public StorableAnimationCurve(AnimationCurve curve)
        {
            val = curve;
        }

        public void NotifyUpdated()
        {
            SuperController.LogError("A curve was updated, but it should be readonly.");
        }
    }
}

