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
        public float min { get; set; }
        public float max { get; set; }
        public bool graphDirty { get; set; } = true;
        public bool animationDirty { get; set; } = true;

        public StorableAnimationCurve(AnimationCurve curve)
        {
            val = curve;
        }

        public void NotifyUpdated()
        {
            SuperController.LogError("A curve was updated, but it should be readonly.");
        }

        public void Update()
        {
            animationDirty = true;
            graphDirty = true;

            float min;
            float max;
            if (val.length == 0)
            {
                min = max = 0f;
            }
            else
            {
                min = Mathf.Infinity;
                max = Mathf.NegativeInfinity;
                for (var k = 0; k < val.length; k++)
                {
                    var v = val[k].value;
                    min = Mathf.Min(min, v);
                    max = Mathf.Max(max, v);
                }
            }

            this.min = min;
            this.max = max;
        }
    }
}

