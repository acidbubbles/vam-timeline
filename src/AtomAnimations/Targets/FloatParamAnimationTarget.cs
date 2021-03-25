using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class FloatParamAnimationTarget : CurveAnimationTargetBase, ICurveAnimationTarget
    {
        public readonly string storableId;
        public JSONStorable storable { get; private set; }
        public string floatParamName;
        public JSONStorableFloat floatParam { get; private set; }
        public readonly BezierAnimationCurve value = new BezierAnimationCurve();
        public bool recording;

        public override string name => $"{storableId}/{floatParamName}";

        private bool _available;
        private readonly Atom _atom;
        private int _lastAvailableCheck;

        public FloatParamAnimationTarget(Atom atom, string storableId, string floatParamName)
        {
            _atom = atom;
            this.storableId = storableId;
            this.floatParamName = floatParamName;
        }

        public FloatParamAnimationTarget(JSONStorable storable, JSONStorableFloat floatParam)
            : this(storable.containingAtom, storable.storeId, floatParam.name)
        {
            this.storable = storable;
            this.floatParam = floatParam;
            _available = true;
        }

        public FloatParamAnimationTarget(FloatParamAnimationTarget source)
        {
            if (source.storable != null)
            {
                storable = source.storable;
                floatParam = source.floatParam;
                _available = true;
            }
            _atom = source._atom;
            storableId = source.storableId;
            floatParamName = source.floatParamName;
        }

        public bool EnsureAvailable(bool silent = true)
        {
            if (_available)
            {
                if (storable == null)
                {
                    _available = false;
                    storable = null;
                    floatParam = null;
                    return false;
                }
                return true;
            }
            if (Time.frameCount == _lastAvailableCheck) return false;
            if (TryBind(silent)) return true;
            _lastAvailableCheck = Time.frameCount;
            return false;
        }

        public bool TryBind(bool silent)
        {
            if (SuperController.singleton.isLoading) return false;
            var storable = _atom.GetStorableByID(storableId);
            if (storable == null)
            {
                if (!silent) SuperController.LogError($"Timeline: Atom '{_atom.uid}' does not have a storable '{storableId}'. It might be loading, try again later.");
                return false;
            }
#if (!VAM_GT_1_20)
            if (storableId == "geometry")
            {
                // This allows loading an animation even though the animatable option was checked off (e.g. loading a pose)
                var morph = (storable as DAZCharacterSelector)?.morphsControlUI?.GetMorphByDisplayName(floatParamName);
                if (morph == null)
                {
                    if (!silent) SuperController.LogError($"Timeline: Atom '{_atom.uid}' does not have a morph (geometry) '{floatParamName}'. Try upgrading to a more recent version of Virt-A-Mate (1.20+).");
                    return false;
                }
                if (!morph.animatable)
                    morph.animatable = true;
            }
#endif
            var floatParam = storable.GetFloatJSONParam(floatParamName);
            if (floatParam == null)
            {
                if (!silent) SuperController.LogError($"Timeline: Atom '{_atom.uid}' does not have a param '{storableId}/{floatParamName}'");
                return false;
            }

            this.storable = storable;
            this.floatParam = floatParam;
            // May be replaced (might use alt name)
            floatParamName = floatParam.name;
            _available = true;
            return true;
        }

        public string GetShortName()
        {
            if (floatParam != null && !string.IsNullOrEmpty(floatParam.altName))
                return floatParam.altName;
            return floatParamName;
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
            var clone = new FloatParamAnimationTarget(_atom, storableId, floatParamName);
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
            var target = backup as FloatParamAnimationTarget;
            if (target == null) return;
            var maxTime = value.GetLastFrame().time;
            value.keys.Clear();
            value.keys.AddRange(target.value.keys.Where(k => k.time < maxTime + 0.0001f));
            value.AddEdgeFramesIfMissing(maxTime, CurveTypeValues.SmoothLocal);
            dirty = true;
        }

        public int SetKeyframe(float time, float setValue, bool makeDirty = true)
        {
            var curveType = SelectCurveType(time, CurveTypeValues.Undefined);
            if (makeDirty) dirty = true;
            return value.SetKeyframe(time, setValue, curveType);
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
            var t = target as FloatParamAnimationTarget;
            if (t == null) return false;
            return Targets(t.storableId, t.floatParamName);
        }

        public bool Targets(string storableId, string floatParamName)
        {
            if (this.storableId != storableId) return false;
            if (this.floatParamName == floatParamName) return true;
            if (floatParamName.StartsWith("morph: ")) floatParamName = floatParamName.Substring("morph: ".Length);
            if (floatParamName.StartsWith("morphOtherGender: ")) floatParamName = floatParamName.Substring("morphOtherGender: ".Length);
            return this.floatParamName == floatParamName;
        }

        public override string ToString()
        {
            return $"[Float Param Target: {name}]";
        }

        public class Comparer : IComparer<FloatParamAnimationTarget>
        {
            public int Compare(FloatParamAnimationTarget t1, FloatParamAnimationTarget t2)
            {
                return string.Compare(t1.name, t2.name, StringComparison.Ordinal);
            }
        }
    }
}
