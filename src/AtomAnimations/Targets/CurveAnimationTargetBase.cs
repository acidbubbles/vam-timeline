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
            if (curve.GetKeyframe(0).time != 0)
            {
                SuperController.LogError($"Target {name} has no start frame. Frames: {string.Join(", ", curve.keys.Select(k => k.time.ToString()).ToArray())}");
                return;
            }
            if (curve.GetKeyframe(curve.length - 1).time > animationLength)
            {
                var curveKeys = curve.keys.Select(k => k.time.ToMilliseconds()).ToList();
                SuperController.LogError($"Target {name} has  duration of {curve.GetKeyframe(curve.length - 1).time} but the animation should be {animationLength}. Auto-repairing extraneous keys.");
                foreach (var c in GetCurves())
                    while (c.GetKeyframe(c.length - 1).time > animationLength && c.length > 2)
                        c.RemoveKey(c.length - 1);
            }
            if (curve.GetKeyframe(curve.length - 1).time != animationLength)
            {
                SuperController.LogError($"Target {name} ends with frame {curve.GetKeyframe(curve.length - 1).time} instead of expected {animationLength}. Auto-repairing last frame.");
                var lastTime = curve.GetKeyframe(curve.length - 1).time;
                foreach (var c in GetCurves())
                {
                    var keyframe = c.GetKeyframe(c.length - 1);
                    if (keyframe.time == animationLength) continue;
                    keyframe.time = animationLength;
                    c.MoveKey(c.length - 1, keyframe);
                }
            }
        }

        protected void ReapplyCurveTypes(BezierAnimationCurve curve, bool loop)
        {
            curve.ComputeCurves();
        }

        public void ChangeCurve(float time, string curveType, bool loop)
        {
            if (string.IsNullOrEmpty(curveType)) return;

            foreach (var curve in GetCurves())
            {
                // TODO: Lookup once instead of for each frame (shared access?)
                var keyframe = curve.GetKeyframeAt(time);
                if (keyframe == null) continue;
                keyframe.curveType = CurveTypeValues.ToInt(curveType);
                if (loop && time == 0)
                {
                    curve.keys[curve.keys.Count - 1].curveType = CurveTypeValues.ToInt(curveType);
                }
            }
            dirty = true;
        }

        public string GetKeyframeSettings(float time)
        {
            // TODO: Replace by int
            var keyframe = GetLeadCurve().GetKeyframeAt(time);
            if (keyframe == null) return null;
            return CurveTypeValues.FromInt(keyframe.curveType);
        }
    }
}
