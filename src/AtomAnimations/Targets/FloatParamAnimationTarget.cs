using System.Collections.Generic;
using UnityEngine;

namespace VamTimeline
{
    public class FloatParamAnimationTarget : CurveAnimationTargetBase, ICurveAnimationTarget
    {
        public readonly JSONStorable storable;
        public readonly JSONStorableFloat floatParam;
        public readonly AnimationCurve value = new AnimationCurve();

        public override string name => storable != null ? $"{storable.name}/{floatParam.name}" : floatParam.name;

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

        public void Validate(float animationLength)
        {
            Validate(value, animationLength);
        }

        public void ReapplyCurveTypes(bool loop)
        {
            if (value.length < 2) return;

            ReapplyCurveTypes(value, loop);
        }

        public override AnimationCurve GetLeadCurve()
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
            EnsureKeyframeSettings(time, CurveTypeValues.Smooth);
            if (dirty) base.dirty = true;
        }

        public void DeleteFrame(float time)
        {
            var key = value.KeyframeBinarySearch(time);
            if (key == -1) return;
            value.RemoveKey(key);
            settings.Remove(time.ToMilliseconds());
            dirty = true;
        }

        public void AddEdgeFramesIfMissing(float animationLength)
        {
            value.AddEdgeFramesIfMissing(animationLength);
            AddEdgeKeyframeSettingsIfMissing(animationLength);
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

        public float GetTimeClosestTo(float time)
        {
            return value[value.KeyframeBinarySearch(time, true)].time;
        }

        public bool HasKeyframe(float time)
        {
            return value.KeyframeBinarySearch(time) != -1;
        }

        #region Snapshots

        public FloatParamSnapshot GetCurveSnapshot(float time)
        {
            var key = value.KeyframeBinarySearch(time);
            if (key == -1) return null;
            return new FloatParamSnapshot
            {
                value = value[key],
                curveType = GetKeyframeSettings(time) ?? CurveTypeValues.LeaveAsIs
            };
        }

        public void SetCurveSnapshot(float time, FloatParamSnapshot snapshot, bool dirty = true)
        {
            value.SetKeySnapshot(time, snapshot.value);
            UpdateSetting(time, snapshot.curveType, true);
            if (dirty) base.dirty = true;
        }

        #endregion

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
