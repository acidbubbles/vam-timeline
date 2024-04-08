using System;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;

namespace VamTimeline
{
    public class JSONStorableFloatRef : AnimatableRefBase
    {
        public override string name => $"{storableId}/{floatParamName}";

        private bool _available;
        private readonly Atom _atom;
        private int _lastAvailableCheck;
        public readonly bool owned;
        public float? assignMinValueOnBound {get; private set; }
        public float? assignMaxValueOnBound {get; private set; }
        public readonly string storableId;
        private readonly string _shortenedStorableId;
        private readonly string _lastKnownAtomUid;
        public JSONStorable storable { get; private set; }
        public string floatParamName;
        public JSONStorableFloat floatParam { get; private set; }
        private readonly string _floatParamDisplayName;
        private float _nextCheck;
        private bool? _isMorph;
        private DAZMorph _asMorph;
        public string lastKnownAtomUid => _lastKnownAtomUid;

        public JSONStorableFloatRef(Atom atom, string storableId, string floatParamName, bool owned, float? assignMinValueOnBound = null, float? assignMaxValueOnBound = null)
        {
            _atom = atom;
            if (!owned)
                _lastKnownAtomUid = atom.uid;
            this.owned = owned;
            if (storableId == null) throw new ArgumentNullException(nameof(storableId));
            this.storableId = storableId;
            this.floatParamName = floatParamName;
            if (floatParamName.StartsWith("morph: "))
                _floatParamDisplayName = floatParamName.Substring("morph: ".Length);
            else if (floatParamName.StartsWith("morphOtherGender: "))
                _floatParamDisplayName = floatParamName.Substring("morphOtherGender: ".Length);
            else
                _floatParamDisplayName = floatParamName;
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

            if (storableId.StartsWith("plugin#"))
                _shortenedStorableId = storableId.Substring(6);
            else
                _shortenedStorableId = storableId;
        }

        public JSONStorableFloatRef(JSONStorable storable, JSONStorableFloat floatParam, bool owned)
            : this(storable.containingAtom, storable.storeId, floatParam.name, owned)
        {
            _atom = storable.containingAtom;
            if (!owned)
                _lastKnownAtomUid = _atom.uid;
            this.storable = storable;
            this.floatParam = floatParam;
            _available = true;
        }

        public override object groupKey
        {
            get
            {
                if (storable == null)
                    return storableId;

                if (!owned)
                    return $"{_atom.uid} {storable.storeId}";

                return storable != null ? (object)storable : storableId;
            }
        }

        public override string groupLabel
        {
            get
            {
                if (!owned)
                {
                    if (storable == null)
                        return $"[Missing: {_shortenedStorableId}]";
                    return $"{storable.containingAtom.name} {_shortenedStorableId}";
                }
                return _shortenedStorableId;
            }
        }

        public override string GetShortName()
        {
            if (!owned && storable == null)
                return $"[Missing: {_shortenedStorableId} / {_floatParamDisplayName}]";

            if (floatParam != null && !string.IsNullOrEmpty(floatParam.altName))
                return floatParam.altName;

            return _floatParamDisplayName;
        }

        public override string GetFullName()
        {
            if (!owned)
            {
                if (storable == null)
                    return $"[Missing: {(_atom != null ? _atom.name : _lastKnownAtomUid)} / {_shortenedStorableId} / {_floatParamDisplayName}]";
                if (floatParam != null && !string.IsNullOrEmpty(floatParam.altName))
                    return $"{_atom.name} {floatParam.altName}";
                return $"{_atom.name} {_shortenedStorableId} {_floatParamDisplayName}";
            }

            if (floatParam != null && !string.IsNullOrEmpty(floatParam.altName))
                return floatParam.altName;
            return $"{_shortenedStorableId} {_floatParamDisplayName}";
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

        public bool EnsureAvailable(bool silent = true, bool forceCheck = false)
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
            if (silent && Time.frameCount == _lastAvailableCheck && !forceCheck)
            {
                return false;
            }
            if (TryBind(silent, forceCheck))
            {
                TryAssignMinMax();
                return true;
            }
            _lastAvailableCheck = Time.frameCount;
            return false;
        }

        private bool TryBind(bool silent, bool forceCheck)
        {
            if (!forceCheck)
            {
                if (SuperController.singleton.isLoading) return false;
                if (_nextCheck > Time.unscaledTime) return false;
            }

            _nextCheck = Time.unscaledTime + 1f;
            storable = _atom.GetStorableByID(storableId);
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
            floatParam = storable.GetFloatJSONParam(floatParamName);
            if (floatParam == null)
            {
                // This should theoretically never happen, but this was the old behavior so it's safer to keep it (Timeline )
                floatParam = storable.GetFloatJSONParam(_floatParamDisplayName);
                if (floatParam == null)
                {
                    if (!silent) SuperController.LogError($"Timeline: Atom '{_atom.uid}' does not have a param '{storableId}/{floatParamName}'");
                    return false;
                }
            }

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

        public bool Targets(Atom atom, string storableId, string floatParamName)
        {
            if (_atom != atom) return false;
            if (this.storableId != storableId) return false;
            if (this.floatParamName == floatParamName) return true;
            if (IsMorph() && floatParamName.StartsWith("morph: ")) floatParamName = floatParamName.Substring("morph: ".Length);
            if (IsMorph() && floatParamName.StartsWith("morphOtherGender: ")) floatParamName = floatParamName.Substring("morphOtherGender: ".Length);
            return this.floatParamName == floatParamName;
        }

        public bool Targets(JSONStorable storable, JSONStorableFloat floatParam)
        {
            return Targets(storable.containingAtom, storable.storeId, floatParam.name);
        }

        public bool IsMorph()
        {
            if (_isMorph.HasValue) return _isMorph.Value;
            _isMorph = storableId == "geometry";
            return _isMorph.Value;
        }

        public DAZMorph AsMorph()
        {
            if (_isMorph == false) return null;
            if (_asMorph != null) return _asMorph;
            if (storable == null) throw new NullReferenceException("Storable was not set");
            var selector = storable as DAZCharacterSelector;
            if (selector == null) throw new InvalidOperationException($"Storable '{storable.name}' expected to be {nameof(DAZCharacterSelector)} but was {storable}");
            DAZMorph morph;
            var otherGender = floatParamName.StartsWith("morphOtherGender:");
            if (!otherGender && selector.morphsControlUI != null)
            {
                morph = selector.morphsControlUI.GetMorphByDisplayName(_floatParamDisplayName);
                if (morph != null)
                {
                    _isMorph = true;
                    return _asMorph = morph;
                }
            }
            else if (otherGender && selector.morphsControlUIOtherGender != null)
            {
                morph = selector.morphsControlUI.GetMorphByDisplayName(_floatParamDisplayName);
                if (morph != null)
                {
                    _isMorph = true;
                    return _asMorph = morph;
                }
            }

            if (selector.gender == DAZCharacterSelector.Gender.Female && !otherGender || selector.gender == DAZCharacterSelector.Gender.Male && otherGender)
            {
                if (selector.femaleMorphBank1 != null)
                {
                    morph = selector.femaleMorphBank1.GetMorphByDisplayName(_floatParamDisplayName);
                    if (morph != null)
                    {
                        _isMorph = true;
                        return _asMorph = morph;
                    }
                }
                if (selector.femaleMorphBank2 != null)
                {
                    morph = selector.femaleMorphBank2.GetMorphByDisplayName(_floatParamDisplayName);
                    if (morph != null)
                    {
                        _isMorph = true;
                        return _asMorph = morph;
                    }
                }
                if (selector.femaleMorphBank3 != null)
                {
                    morph = selector.femaleMorphBank3.GetMorphByDisplayName(_floatParamDisplayName);
                    if (morph != null)
                    {
                        _isMorph = true;
                        return _asMorph = morph;
                    }
                }
            }
            else
            {
                if (selector.maleMorphBank1 != null)
                {
                    morph = selector.maleMorphBank1.GetMorphByDisplayName(_floatParamDisplayName);
                    if (morph != null)
                    {
                        _isMorph = true;
                        return _asMorph = morph;
                    }
                }
                if (selector.maleMorphBank2 != null)
                {
                    morph = selector.maleMorphBank2.GetMorphByDisplayName(_floatParamDisplayName);
                    if (morph != null)
                    {
                        _isMorph = true;
                        return _asMorph = morph;
                    }
                }
                if (selector.maleMorphBank3 != null)
                {
                    morph = selector.maleMorphBank3.GetMorphByDisplayName(_floatParamDisplayName);
                    if (morph != null)
                    {
                        _isMorph = true;
                        return _asMorph = morph;
                    }
                }
            }

            return null;
        }
    }
}
