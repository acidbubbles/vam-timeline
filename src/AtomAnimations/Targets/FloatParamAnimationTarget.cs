using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class FloatParamAnimationTarget : CurveAnimationTargetBase, ICurveAnimationTarget
    {
        public readonly string storableId;
        public JSONStorable storable { get; private set; }
        public readonly string floatParamName;
        public JSONStorableFloat floatParam { get; private set; }
        public readonly AnimationCurve value = new AnimationCurve();

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
            else
            {
                _atom = source._atom;
                storableId = source.storableId;
                floatParamName = source.floatParamName;
                _available = false;
            }
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
            var floatParam = storable.GetFloatJSONParam(floatParamName);
            if (floatParam == null)
            {
                if (!silent) SuperController.LogError($"Timeline: Atom '{_atom.uid}' does not have a param '{storableId}/{floatParamName}'");
                return false;
            }

            this.storable = storable;
            this.floatParam = floatParam;
            _available = true;
            return true;
        }

        public string GetShortName()
        {
            return floatParamName;
        }

        public void Sample(float clipTime, float weight)
        {
            if (!EnsureAvailable()) return;
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

        public override IEnumerable<AnimationCurve> GetCurves()
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
            var before = value.length;
            value.AddEdgeFramesIfMissing(animationLength);
            AddEdgeKeyframeSettingsIfMissing(animationLength);
            if (value.length != before) dirty = true;
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

        ISnapshot IAtomAnimationTarget.GetSnapshot(float time)
        {
            return GetCurveSnapshot(time);
        }
        void IAtomAnimationTarget.SetSnapshot(float time, ISnapshot snapshot)
        {
            SetCurveSnapshot(time, (FloatParamSnapshot)snapshot);
        }

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
            return t.storableId == storableId && t.floatParamName == floatParamName;
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
