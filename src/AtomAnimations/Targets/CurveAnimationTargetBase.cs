using System.Collections.Generic;
using System.Globalization;
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
                SuperController.LogError($"Target {name} has no start frame. Frames: {string.Join(", ", curve.keys.Select(k => k.time.ToString(CultureInfo.InvariantCulture)).ToArray())}");
                return;
            }
            if (curve.duration > animationLength)
            {
                SuperController.LogError($"Target {name} has  duration of {curve.duration} but the animation should be {animationLength}. Auto-repairing extraneous keys.");
                foreach (var c in GetCurves())
                    while (c.GetKeyframeByKey(c.length - 1).time > animationLength && c.length > 2)
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
                    c.SetLastFrame(keyframe);
                }
            }
        }

        public void ChangeCurve(float time, int curveType, bool makeDirty = true)
        {
            foreach (var curve in GetCurves())
            {
                var key = curve.KeyframeBinarySearch(time);
                if (key == -1) continue;
                var keyframe = curve.GetKeyframeByKey(key);
                keyframe.curveType = curveType;
                curve.SetKeyframeByKey(key, keyframe);
                if (curve.loop && time == 0)
                {
                    var last = curve.GetLastFrame();
                    last.curveType = curveType;
                    curve.SetLastFrame(last);
                }
            }
            if (makeDirty) dirty = true;
        }

        protected int SelectCurveType(float time, int curveType)
        {
            if (curveType != CurveTypeValues.Undefined)
                return curveType;
            var curve = GetLeadCurve();
            if (curve.keys.Count == 0)
                return CurveTypeValues.SmoothLocal;
            var key = curve.KeyframeBinarySearch(time, true);
            if (key == -1)
                return CurveTypeValues.SmoothLocal;
            var keyframe = curve.keys[key];
            if (keyframe.curveType != CurveTypeValues.CopyPrevious)
                return keyframe.curveType;
            return CurveTypeValues.SmoothLocal;
        }

        public int GetKeyframeCurveType(float time)
        {
            return GetLeadCurve().GetKeyframeAt(time).curveType;
        }
    }
}
