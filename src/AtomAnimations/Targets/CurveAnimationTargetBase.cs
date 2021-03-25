using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

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
            if (curve.duration > animationLength + 0.0001f)
            {
                SuperController.LogError($"Target {name} has  duration of {curve.duration:0.0000} but the animation should be {animationLength:0.0000}. Auto-repairing extraneous keys.");
                foreach (var c in GetCurves())
                    while (c.GetKeyframeByKey(c.length - 1).time > animationLength && c.length > 2)
                        c.RemoveKey(c.length - 1);
                dirty = true;
            }
            if (curve.duration != animationLength)
            {
                if(Mathf.Abs(curve.duration - animationLength) > 0.0009f)
                    SuperController.LogError($"Target {name} ends with frame {curve.duration:0.0000} instead of expected {animationLength:0.0000}. Auto-repairing last frame.");
                foreach (var c in GetCurves())
                {
                    var keyframe = c.GetLastFrame();
                    if (keyframe.time == animationLength) continue;
                    keyframe.time = animationLength;
                    c.SetLastFrame(keyframe);
                }
                dirty = true;
            }
        }

        public void ChangeCurve(float time, int curveType, bool makeDirty = true)
        {
            var key = GetLeadCurve().KeyframeBinarySearch(time);
            ChangeCurveByKey(key, curveType, makeDirty);
        }

        public void ChangeCurveByKey(int key, int curveType, bool makeDirty = true)
        {
            if (key == -1) return;
            foreach (var curve in GetCurves())
            {
                var keyframe = curve.GetKeyframeByKey(key);
                keyframe.curveType = curveType;
                curve.SetKeyframeByKey(key, keyframe);
                if (curve.loop && key == 0)
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
