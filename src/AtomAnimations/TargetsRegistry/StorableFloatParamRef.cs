using UnityEngine;

namespace VamTimeline
{
    public class StorableFloatParamRef : AnimatableRefBase
    {
        public override string name => $"{storableId}/{floatParamName}";

        private bool _available;
        private readonly Atom _atom;
        private int _lastAvailableCheck;
        public readonly string storableId;
        public JSONStorable storable { get; private set; }
        public string floatParamName;
        public JSONStorableFloat floatParam { get; private set; }

        public StorableFloatParamRef(Atom atom, string storableId, string floatParamName)
        {
            _atom = atom;
            this.storableId = storableId;
            this.floatParamName = floatParamName;
        }

        public StorableFloatParamRef(JSONStorable storable, JSONStorableFloat floatParam)
            : this(storable.containingAtom, storable.storeId, floatParam.name)
        {
            this.storable = storable;
            this.floatParam = floatParam;
            _available = true;
        }

        public StorableFloatParamRef(StorableFloatParamRef source)
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

        public string GetShortName()
        {
            if (floatParam != null && !string.IsNullOrEmpty(floatParam.altName))
                return floatParam.altName;
            return floatParamName;
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
