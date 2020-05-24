using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class FloatParamAnimationTarget : IAnimationTargetWithCurves
    {
        public JSONStorable Storable { get; }
        public JSONStorableFloat FloatParam { get; }
        public StorableAnimationCurve StorableValue;
        public AnimationCurve Value => StorableValue.val;

        public string Name => Storable != null ? $"{Storable.name}/{FloatParam.name}" : FloatParam.name;

        public FloatParamAnimationTarget(JSONStorable storable, JSONStorableFloat jsf)
        {
            Storable = storable;
            FloatParam = jsf;
            StorableValue = new StorableAnimationCurve(new AnimationCurve());
        }

        public string GetShortName()
        {
            return FloatParam.name;
        }

        public AnimationCurve GetLeadCurve()
        {
            return Value;
        }

        public IEnumerable<AnimationCurve> GetCurves()
        {
            return new[] { Value };
        }

        public IEnumerable<StorableAnimationCurve> GetStorableCurves()
        {
            return new[] { StorableValue };
        }

        public void SetKeyframe(float time, float value)
        {
            Value.SetKeyframe(time, value);
            StorableValue.Update();
        }

        public void DeleteFrame(float time)
        {
            var key = Value.KeyframeBinarySearch(time);
            if (key == -1) return;
            DeleteFrameByKey(key);
        }

        public void DeleteFrameByKey(int key)
        {
            Value.RemoveKey(key);
            StorableValue.Update();
        }

        public IEnumerable<float> GetAllKeyframesTime()
        {
            return Value.keys.Select(k => k.time);
        }

        public class Comparer : IComparer<FloatParamAnimationTarget>
        {
            public int Compare(FloatParamAnimationTarget t1, FloatParamAnimationTarget t2)
            {
                return t1.Name.CompareTo(t2.Name);

            }
        }
    }
}
