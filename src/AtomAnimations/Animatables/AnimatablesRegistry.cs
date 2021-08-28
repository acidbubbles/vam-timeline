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

        public IList<JSONStorableFloatRef> storableFloats => _storableFloats;

        public JSONStorableFloatRef GetOrCreateStorableFloat(Atom atom, string storableId, string floatParamName)
        {
            var t = _storableFloats.FirstOrDefault(x => x.Targets(storableId, floatParamName));
            if (t != null) return t;
            t = new JSONStorableFloatRef(atom, storableId, floatParamName);
            _storableFloats.Add(t);
            RegisterAnimatableRef(t);
            return t;
        }

        public JSONStorableFloatRef GetOrCreateStorableFloat(JSONStorable storable, JSONStorableFloat floatParam)
        {
            var t = _storableFloats.FirstOrDefault(x => x.Targets(storable, floatParam));
            if (t != null) return t;
            t = new JSONStorableFloatRef(storable, floatParam);
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

        public FreeControllerV3Ref GetOrCreateController(Atom atom, string controllerName)
        {
            var t = _controllers.FirstOrDefault(x => x.Targets(controllerName));
            if (t != null) return t;
            var controller = atom.freeControllers.FirstOrDefault(fc => fc.name == controllerName);
            if (ReferenceEquals(controller, null))
            {
                SuperController.LogError($"Timeline: Atom '{atom.uid}' does not have a controller '{controllerName}'");
                return null;
            }
            t = new FreeControllerV3Ref(controller);
            _controllers.Add(t);
            RegisterAnimatableRef(t);
            return t;
        }

        public FreeControllerV3Ref GetOrCreateController(FreeControllerV3 controller)
        {
            var t = _controllers.FirstOrDefault(x => x.Targets(controller));
            if (t != null) return t;
            t = new FreeControllerV3Ref(controller);
            _controllers.Add(t);
            RegisterAnimatableRef(t);
            return t;
        }

        public void RemoveController(FreeControllerV3Ref t)
        {
            _controllers.Remove(t);
            UnregisterAnimatableRef(t);
        }

        public IList<TriggersTrackRef> triggers => _triggers;

        public TriggersTrackRef GetOrCreateTriggerTrack(string triggerTrackName)
        {
            var t = _triggers.FirstOrDefault(x => x.Targets(triggerTrackName));
            if (t != null) return t;
            t = new TriggersTrackRef(triggerTrackName);
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
    }
}
