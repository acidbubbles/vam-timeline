using System.Collections.Generic;
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

        public override string name => $"{storableId}/{floatParamName}";

        private bool _available;
        private readonly Atom _atom;
        private int _lastAvailableCheck = 0;

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
                    if (!silent) SuperController.LogError($"Timeline: Atom '{_atom.uid}' does not have a morph (geometry) '{floatParamName}'.");
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
            this.floatParamName = floatParam.name;
            _available = true;
            return true;
        }

        public string GetShortName()
        {
            if (floatParam != null)
            {
                if (!string.IsNullOrEmpty(floatParam.altName))
                    return floatParam.altName;
            }
            return storableId == "geometry" ? floatParamName : $"{(storableId.Length > 4 ? storableId.Substring(0, 4) : storableId)}/{floatParamName}";
        }

        public void Validate(float animationLength)
        {
            Validate(value, animationLength);
        }

        public void ReapplyCurveTypes()
        {
            if (value.length < 2) return;

            ComputeCurves(value);
        }

        public override BezierAnimationCurve GetLeadCurve()
        {
            return value;
        }

        public override IEnumerable<BezierAnimationCurve> GetCurves()
        {
            return new[] { value };
        }

        public void SetKeyframe(float time, float value, bool dirty = true)
        {
            var curveType = SelectCurveType(time, CurveTypeValues.Undefined);
            this.value.SetKeyframe(time, value, curveType);
            if (dirty) base.dirty = true;
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
            var before = value.length;
            value.AddEdgeFramesIfMissing(animationLength, CurveTypeValues.SmoothLocal);
            if (value.length != before) dirty = true;
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

        public void SetCurveSnapshot(float time, FloatParamTargetSnapshot snapshot, bool dirty = true)
        {
            value.SetKeySnapshot(time, snapshot.value);
            if (dirty) base.dirty = true;
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
                return t1.name.CompareTo(t2.name);
            }
        }
    }
}
