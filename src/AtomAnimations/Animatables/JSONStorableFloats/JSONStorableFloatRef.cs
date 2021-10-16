using System.Runtime.CompilerServices;
using UnityEngine;

namespace VamTimeline
{
    public class JSONStorableFloatRef : AnimatableRefBase
    {
        public override string name => $"{storableId}/{floatParamName}";

        private bool _available;
        private readonly Atom _atom;
        private int _lastAvailableCheck;
        public float? assignMinValueOnBound {get; private set; }
        public float? assignMaxValueOnBound {get; private set; }
        public readonly string storableId;
        public JSONStorable storable { get; private set; }
        public string floatParamName;
        public JSONStorableFloat floatParam { get; private set; }

        public JSONStorableFloatRef(Atom atom, string storableId, string floatParamName, float? assignMinValueOnBound = null, float? assignMaxValueOnBound = null)
        {
            _atom = atom;
            this.storableId = storableId;
            this.floatParamName = floatParamName;
            if (assignMinValueOnBound == 0 && assignMaxValueOnBound == 0)
            {
                this.assignMinValueOnBound = null;
                this.assignMaxValueOnBound = null;
            }
            else
            {
                this.assignMinValueOnBound = assignMinValueOnBound;
                this.assignMaxValueOnBound = assignMaxValueOnBound;
            }
        }

        public JSONStorableFloatRef(JSONStorable storable, JSONStorableFloat floatParam)
            : this(storable.containingAtom, storable.storeId, floatParam.name)
        {
            this.storable = storable;
            this.floatParam = floatParam;
            _available = true;
        }

        public override string GetShortName()
        {
            if (floatParam != null && !string.IsNullOrEmpty(floatParam.altName))
                return floatParam.altName;
            return floatParamName;
        }

        public float val
        {
            get
            {
                return floatParam.val;
            }
            set
            {
                floatParam.val = Mathf.Clamp(value, floatParam.min, floatParam.max);
            }
        }

        public bool EnsureAvailable(bool silent = true)
        {
            if (_available)
            {
                if (storable != null) return true;
                _available = false;
                storable = null;
                floatParam = null;
            }
            if (Time.frameCount == _lastAvailableCheck) return false;
            if (TryBind(silent)) return true;
            _lastAvailableCheck = Time.frameCount;
            return false;
        }

        private bool TryBind(bool silent)
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
            if (assignMinValueOnBound != null)
            {
                floatParam.min = assignMinValueOnBound.Value;
                assignMinValueOnBound = null;
            }
            if (assignMaxValueOnBound != null)
            {
                floatParam.max = assignMaxValueOnBound.Value;
                assignMaxValueOnBound = null;
            }
            _available = true;
            return true;
        }

        public bool Targets(string storableId, string floatParamName)
        {
            if (this.storableId != storableId) return false;
            if (this.floatParamName == floatParamName) return true;
            if (floatParamName.StartsWith("morph: ")) floatParamName = floatParamName.Substring("morph: ".Length);
            if (floatParamName.StartsWith("morphOtherGender: ")) floatParamName = floatParamName.Substring("morphOtherGender: ".Length);
            return this.floatParamName == floatParamName;
        }

        public bool Targets(JSONStorable storable, JSONStorableFloat floatParam)
        {
            return Targets(storable.storeId, floatParam.name);
        }
    }
}
