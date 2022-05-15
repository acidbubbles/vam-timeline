using System;
using System.Linq;
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
            if (this.floatParamName.StartsWith("morph: "))
                this.floatParamName = this.floatParamName.Substring("morph: ".Length);
            if (this.floatParamName.StartsWith("morphOtherGender: "))
                this.floatParamName = this.floatParamName.Substring("morphOtherGender: ".Length);
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

        public override string GetFullName()
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
                if (storable != null)
                {
                    TryAssignMinMax();
                    return true;
                }
                _available = false;
                storable = null;
                floatParam = null;
            }
            if (Time.frameCount == _lastAvailableCheck) return false;
            if (TryBind(silent))
            {
                TryAssignMinMax();
                return true;
            }
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
            floatParamName = IsMorph() ? floatParam.altName : floatParam.name;
            _available = true;
            return true;
        }

        private void TryAssignMinMax()
        {
            if (assignMinValueOnBound.HasValue)
            {
                var min = assignMinValueOnBound.Value;
                if (IsMorph())
                {
                    var dazMorph = AsMorph();
                    if (dazMorph != null)
                    {
                        dazMorph.min = min;
                        floatParam.min = min;
                        assignMinValueOnBound = null;
                    }
                }
                else
                {
                    floatParam.min = min;
                    assignMinValueOnBound = null;
                }
            }

            if (assignMaxValueOnBound.HasValue)
            {
                var max = assignMaxValueOnBound.Value;
                if (IsMorph())
                {
                    var dazMorph = AsMorph();
                    if (dazMorph != null)
                    {
                        floatParam.max = max;
                        dazMorph.max = max;
                        assignMaxValueOnBound = null;
                    }
                }
                else
                {
                    floatParam.max = max;
                    assignMaxValueOnBound = null;
                }
            }
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

        public bool IsMorph()
        {
            return storableId == "geometry";
        }

        public DAZMorph AsMorph()
        {
            if (storable == null) throw new NullReferenceException("Storable was not set");
            var selector = storable as DAZCharacterSelector;
            if (selector == null) throw new InvalidOperationException($"Storable '{storable.name}' expected to be {nameof(DAZCharacterSelector)} but was {storable}");
            DAZMorph morph;
            if (selector.morphsControlUI != null)
            {
                morph = selector.morphsControlUI.GetMorphByUid(floatParamName);
                if (morph != null) return morph;
            }
            if (selector.morphBank1 != null)
            {
                morph = selector.morphBank1.morphs.FirstOrDefault(m => m.resolvedDisplayName == floatParamName);
                if (morph != null) return morph;
            }
            if (selector.morphBank2 != null)
            {
                morph = selector.morphBank2.morphs.FirstOrDefault(m => m.resolvedDisplayName == floatParamName);
                if (morph != null) return morph;
            }
            if (selector.morphBank3 != null)
            {
                morph = selector.morphBank3.morphs.FirstOrDefault(m => m.resolvedDisplayName == floatParamName);
                if (morph != null) return morph;
            }
            return null;
        }
    }
}
