using System;
using System.Collections.Generic;
using System.Linq;

namespace VamTimeline
{
    public class JSONStorableFloatAnimationTarget : CurveAnimationTargetBase<JSONStorableFloatRef>, ICurveAnimationTarget
    {
        public readonly BezierAnimationCurve value = new BezierAnimationCurve();
        public bool recording { get; set; }

        public JSONStorableFloatAnimationTarget(JSONStorableFloatAnimationTarget source)
            : this(source.animatableRef)
        {
        }

        public JSONStorableFloatAnimationTarget(JSONStorableFloatRef animatableRef)
            : base(animatableRef)
        {
        }

        public void Validate(float animationLength)
        {
            Validate(value, animationLength);
        }

        public override BezierAnimationCurve GetLeadCurve()
        {
            return value;
        }

        public override IEnumerable<BezierAnimationCurve> GetCurves()
        {
            return new[] { value };
        }

        public ICurveAnimationTarget Clone(bool copyKeyframes)
        {
            var clone = new JSONStorableFloatAnimationTarget(animatableRef);
            if (copyKeyframes)
            {
                clone.value.keys.AddRange(value.keys);
            }
            else
            {
                clone.value.SetKeyframe(0f, value.keys[0].value, CurveTypeValues.SmoothLocal);
                clone.value.SetKeyframe(value.length - 1, value.keys[value.length - 1].value, CurveTypeValues.SmoothLocal);
                clone.value.ComputeCurves();
            }
            return clone;
        }

        public void RestoreFrom(ICurveAnimationTarget backup)
        {
            var target = backup as JSONStorableFloatAnimationTarget;
            if (target == null) return;
            var maxTime = value.GetLastFrame().time;
            value.keys.Clear();
            value.keys.AddRange(target.value.keys.Where(k => k.time < maxTime + 0.0001f));
            value.AddEdgeFramesIfMissing(maxTime, CurveTypeValues.SmoothLocal);
            dirty = true;
        }

        public void IncreaseCapacity(int capacity)
        {
            value.keys.Capacity = Math.Max(value.keys.Capacity, capacity);
        }

        public void TrimCapacity()
        {
            value.keys.TrimExcess();
        }

        public int SetKeyframe(float time, float setValue, bool makeDirty = true)
        {
            var curveType = SelectCurveType(time, CurveTypeValues.Undefined);
            if (makeDirty) dirty = true;
            return value.SetKeyframe(time, setValue, curveType);
        }

        public int SetKeyframeToCurrent(float time, bool makeDirty = true)
        {
            return SetKeyframe(time, animatableRef.val, makeDirty);
        }

        public int AddKeyframeAtTime(float time, bool makeDirty = true)
        {
            return SetKeyframe(time, value.Evaluate(time), makeDirty);
        }

        public void DeleteFrame(float time)
        {
            var key = value.KeyframeBinarySearch(time);
            if (key == -1) return;
            value.RemoveKey(key);
            dirty = true;
        }

        public void AddEdgeFramesIfMissing(float animationLength)
        {
            var lastCurveType = value.length > 0 ? value.GetLastFrame().curveType : CurveTypeValues.SmoothLocal;
            if (!value.AddEdgeFramesIfMissing(animationLength, lastCurveType)) return;
            if (value.length > 2 && value.keys[value.length - 2].curveType == CurveTypeValues.CopyPrevious)
                value.RemoveKey(value.length - 2);
            dirty = true;
        }

        public float[] GetAllKeyframesTime()
        {
            var curve = value;
            var keyframes = new float[curve.length];
            for (var i = 0; i < curve.length; i++)
                keyframes[i] = curve.GetKeyframeByKey(i).time;
            return keyframes;
        }

        public float GetTimeClosestTo(float time)
        {
            return value.GetKeyframeByKey(value.KeyframeBinarySearch(time, true)).time;
        }

        public bool HasKeyframe(float time)
        {
            return value.KeyframeBinarySearch(time) != -1;
        }

        #region Snapshots

        ISnapshot IAtomAnimationTarget.GetSnapshot(float time)
        {
            return GetCurveSnapshot(time);
        }
        void IAtomAnimationTarget.SetSnapshot(float time, ISnapshot snapshot)
        {
            SetCurveSnapshot(time, (FloatParamTargetSnapshot)snapshot);
        }

        public FloatParamTargetSnapshot GetCurveSnapshot(float time)
        {
            var key = value.KeyframeBinarySearch(time);
            if (key == -1) return null;
            return new FloatParamTargetSnapshot
            {
                value = value.GetKeyframeByKey(key)
            };
        }

        public void SetCurveSnapshot(float time, FloatParamTargetSnapshot snapshot, bool makeDirty = true)
        {
            value.SetKeySnapshot(time, snapshot.value);
            if (makeDirty) dirty = true;
        }

        #endregion

        public bool TargetsSameAs(IAtomAnimationTarget target)
        {
            var t = target as JSONStorableFloatAnimationTarget;
            if (t == null) return false;
            return t.animatableRef == animatableRef;
        }

        public override string ToString()
        {
            return $"[Float Param Target: {name}]";
        }

        public class Comparer : IComparer<JSONStorableFloatAnimationTarget>
        {
            public int Compare(JSONStorableFloatAnimationTarget x, JSONStorableFloatAnimationTarget y)
            {
                if (x?.animatableRef.storable == null || y?.animatableRef.storable == null)
                    return 0;

                var xAtom = x.animatableRef.storable.containingAtom;
                var yAtom = y.animatableRef.storable.containingAtom;
                if (xAtom != yAtom)
                {
                    if (x.animatableRef.owned)
                        return -1;
                    if (y.animatableRef.owned)
                        return 1;
                    return string.Compare(xAtom.name, yAtom.name, StringComparison.Ordinal);
                }

                var result = string.Compare(x.animatableRef.storable.name, y.animatableRef.storable.name, StringComparison.Ordinal);
                if (result != 0) return result;
                return string.Compare(x.animatableRef.floatParamName, y.animatableRef.floatParamName, StringComparison.Ordinal);
            }
        }
    }
}
