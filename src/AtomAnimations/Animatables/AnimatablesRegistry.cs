using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Events;

namespace VamTimeline
{
    public class AnimatablesRegistry : IDisposable
    {
        private readonly List<JSONStorableFloatRef> _storableFloats = new List<JSONStorableFloatRef>();
        private readonly List<FreeControllerV3Ref> _controllers = new List<FreeControllerV3Ref>();
        private readonly List<TriggersTrackRef> _triggers = new List<TriggersTrackRef>();

        public readonly UnityEvent onTargetsSelectionChanged = new UnityEvent();
        public readonly UnityEvent onControllersListChanged = new UnityEvent();
        public bool locked;

        public IList<JSONStorableFloatRef> storableFloats => _storableFloats;

        public JSONStorableFloatRef GetOrCreateStorableFloat(Atom atom, string storableId, string floatParamName, bool owned, float? assignMinValueOnBound = null, float? assignMaxValueOnBound = null)
        {
            var t = _storableFloats.FirstOrDefault(x => x.Targets(atom, storableId, floatParamName));
            if (t != null) return t;
            t = new JSONStorableFloatRef(atom, storableId, floatParamName, owned, assignMinValueOnBound, assignMaxValueOnBound);
            _storableFloats.Add(t);
            RegisterAnimatableRef(t);
            return t;
        }

        public JSONStorableFloatRef GetOrCreateStorableFloat(JSONStorable storable, JSONStorableFloat floatParam, bool owned)
        {
            var t = _storableFloats.FirstOrDefault(x => x.Targets(storable, floatParam));
            if (t != null) return t;
            t = new JSONStorableFloatRef(storable, floatParam, owned);
            _storableFloats.Add(t);
            RegisterAnimatableRef(t);
            return t;
        }

        public void RemoveStorableFloat(JSONStorableFloatRef t)
        {
            _storableFloats.Remove(t);
            UnregisterAnimatableRef(t);
        }

        public IList<FreeControllerV3Ref> controllers => _controllers;

        public FreeControllerV3Ref GetOrCreateController(FreeControllerV3 controller, bool owned)
        {
            var t = _controllers.FirstOrDefault(x => x.Targets(controller));
            if (t != null) return t;
            t = new FreeControllerV3Ref(controller, owned);
            _controllers.Add(t);
            onControllersListChanged.Invoke();
            RegisterAnimatableRef(t);
            return t;
        }

        public void RemoveController(FreeControllerV3Ref t)
        {
            _controllers.Remove(t);
            onControllersListChanged.Invoke();
            UnregisterAnimatableRef(t);
        }

        public IList<TriggersTrackRef> triggers => _triggers;

        public TriggersTrackRef GetOrCreateTriggerTrack(int animationLayerQualifiedId, string triggerTrackName)
        {
            var t = _triggers.FirstOrDefault(x => x.Targets(animationLayerQualifiedId, triggerTrackName));
            if (t != null) return t;
            t = new TriggersTrackRef(animationLayerQualifiedId, triggerTrackName);
            _triggers.Add(t);
            RegisterAnimatableRef(t);
            return t;
        }

        public void RemoveTriggerTrack(TriggersTrackRef t)
        {
            _triggers.Remove(t);
            UnregisterAnimatableRef(t);
        }

        private void RegisterAnimatableRef(AnimatableRefBase animatableRef)
        {
            animatableRef.onSelectedChanged.AddListener(OnSelectedChanged);
        }

        private void UnregisterAnimatableRef(AnimatableRefBase animatableRef)
        {
            animatableRef.onSelectedChanged.RemoveListener(OnSelectedChanged);
        }

        private void OnSelectedChanged()
        {
            onTargetsSelectionChanged.Invoke();
        }

        public void Dispose()
        {
            foreach (var t in _storableFloats)
                t.onSelectedChanged.RemoveAllListeners();

            foreach (var t in _controllers)
                t.onSelectedChanged.RemoveAllListeners();

            foreach (var t in _triggers)
                t.onSelectedChanged.RemoveAllListeners();
        }

        public void RemoveAllListeners()
        {
            onTargetsSelectionChanged.RemoveAllListeners();
            onControllersListChanged.RemoveAllListeners();
        }
    }
}
