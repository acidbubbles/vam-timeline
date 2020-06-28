using System.Collections.Generic;
using UnityEngine;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class FloatParamAnimationTarget : AnimationTargetBase, IAnimationTargetWithCurves
    {
        public readonly JSONStorable storable;
        public readonly JSONStorableFloat floatParam;
        public readonly AnimationCurve value = new AnimationCurve();

        public string name => storable != null ? $"{storable.name}/{floatParam.name}" : floatParam.name;

        public FloatParamAnimationTarget(JSONStorable storable, JSONStorableFloat floatParam)
        {
            this.storable = storable;
            this.floatParam = floatParam;
        }

        public string GetShortName()
        {
            return floatParam.name;
        }

        public void Sample(float clipTime, float weight)
        {
            floatParam.val = Mathf.Lerp(floatParam.val, value.Evaluate(clipTime), weight);
        }

        public void Validate()
        {
            if (value.length < 2)
            {
                SuperController.LogError($"Target {name} has {value.length} frames");
                return;
            }
        }

        public AnimationCurve GetLeadCurve()
        {
            return value;
        }

        public IEnumerable<AnimationCurve> GetCurves()
        {
            return new[] { value };
        }

        public void SetKeyframe(float time, float value, bool dirty = true)
        {
            this.value.SetKeyframe(time, value);
            if (dirty) base.dirty = true;
        }

        public void DeleteFrame(float time)
        {
            var key = value.KeyframeBinarySearch(time);
            if (key == -1) return;
            DeleteFrameByKey(key);
        }

        public void DeleteFrameByKey(int key)
        {
            value.RemoveKey(key);
            dirty = true;
        }

        public float[] GetAllKeyframesTime()
        {
            var curve = value;
            var keyframes = new float[curve.length];
            for (var i = 0; i < curve.length; i++)
                keyframes[i] = curve[i].time;
            return keyframes;
        }

        public bool HasKeyframe(float time)
        {
            return value.KeyframeBinarySearch(time) != -1;
        }

        public bool TargetsSameAs(IAtomAnimationTarget target)
        {
            var t = target as FloatParamAnimationTarget;
            if (t == null) return false;
            return t.storable == storable && t.floatParam == floatParam;
        }

        public class Comparer : IComparer<FloatParamAnimationTarget>
        {
            public int Compare(FloatParamAnimationTarget t1, FloatParamAnimationTarget t2)
            {
                return t1.name.CompareTo(t2.name);
            }
        }
    }
}
