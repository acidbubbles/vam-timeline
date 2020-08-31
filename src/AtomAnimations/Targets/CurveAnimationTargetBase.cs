using System.Collections.Generic;
using System.Linq;

namespace VamTimeline
{
    public abstract class CurveAnimationTargetBase : AnimationTargetBase
    {
        public abstract string name { get; }

        public abstract BezierAnimationCurve GetLeadCurve();
        public abstract IEnumerable<BezierAnimationCurve> GetCurves();

        protected void Validate(BezierAnimationCurve curve, float animationLength)
        {
            if (animationLength <= 0)
            {
                SuperController.LogError($"Target {name} has an invalid animation length of {animationLength}");
                return;
            }
            if (curve.length < 2)
            {
                SuperController.LogError($"Target {name} has {curve.length} frames");
                return;
            }
            if (curve.GetFirstFrame().time != 0)
            {
                SuperController.LogError($"Target {name} has no start frame. Frames: {string.Join(", ", curve.keys.Select(k => k.time.ToString()).ToArray())}");
                return;
            }
            if (curve.duration > animationLength)
            {
                var curveKeys = curve.keys.Select(k => k.time.ToMilliseconds()).ToList();
                SuperController.LogError($"Target {name} has  duration of {curve.duration} but the animation should be {animationLength}. Auto-repairing extraneous keys.");
                foreach (var c in GetCurves())
                    while (c.GetKeyframe(c.length - 1).time > animationLength && c.length > 2)
                        c.RemoveKey(c.length - 1);
            }
            if (curve.duration != animationLength)
            {
                SuperController.LogError($"Target {name} ends with frame {curve.duration} instead of expected {animationLength}. Auto-repairing last frame.");
                foreach (var c in GetCurves())
                {
                    var keyframe = c.GetLastFrame();
                    if (keyframe.time == animationLength) continue;
                    keyframe.time = animationLength;
                }
            }
        }

        protected void ComputeCurves(BezierAnimationCurve curve, bool loop)
        {
            curve.ComputeCurves();
        }

        public void ChangeCurve(float time, int curveType, bool loop)
        {
            foreach (var curve in GetCurves())
            {
                var keyframe = curve.GetKeyframeAt(time);
                if (keyframe == null) continue;
                keyframe.curveType = curveType;
                if (loop && time == 0)
                {
                    curve.keys[curve.keys.Count - 1].curveType = curveType;
                }
            }
            dirty = true;
        }

        public int GetKeyframeCurveType(float time)
        {
            var keyframe = GetLeadCurve().GetKeyframeAt(time);
            if (keyframe == null) return -1;
            return keyframe.curveType;
        }
    }
}
