using System.Collections.Generic;
using System.Linq;
using UnityEngine.Events;

namespace VamTimeline
{
    public class AnimatablesRegistry
    {
        public List<StorableFloatParamRef> storableFloats = new List<StorableFloatParamRef>();
        public List<FreeControllerV3Ref> controllers = new List<FreeControllerV3Ref>();
        public List<TriggerTrackRef> triggers = new List<TriggerTrackRef>();

        public readonly UnityEvent onTargetsSelectionChanged = new UnityEvent();

        public StorableFloatParamRef GetOrCreateStorableFloat(Atom atom, string storableId, string floatParamName)
        {
            var t = storableFloats.FirstOrDefault(x => x.Targets(storableId, floatParamName));
            if (t != null) return t;
            t = new StorableFloatParamRef(atom, storableId, floatParamName);
            storableFloats.Add(t);
            RegisterAnimatableRef(t);
            return t;
        }

        public StorableFloatParamRef GetOrCreateStorableFloat(JSONStorable storable, JSONStorableFloat floatParam)
        {
            var t = storableFloats.FirstOrDefault(x => x.Targets(storable, floatParam));
            if (t != null) return t;
            t = new StorableFloatParamRef(storable, floatParam);
            storableFloats.Add(t);
            RegisterAnimatableRef(t);
            return t;
        }

        public FreeControllerV3Ref GetOrCreateController(Atom atom, string controllerName)
        {
            var t = controllers.FirstOrDefault(x => x.Targets(controllerName));
            if (t != null) return t;
            var controller = atom.freeControllers.FirstOrDefault(fc => fc.name == controllerName);
            if (ReferenceEquals(controller, null))
            {
                SuperController.LogError($"Timeline: Atom '{atom.uid}' does not have a controller '{controllerName}'");
                return null;
            }
            t = new FreeControllerV3Ref(controller);
            controllers.Add(t);
            RegisterAnimatableRef(t);
            return t;
        }

        public FreeControllerV3Ref GetOrCreateController(FreeControllerV3 controller)
        {
            var t = controllers.FirstOrDefault(x => x.Targets(controller));
            if (t != null) return t;
            t = new FreeControllerV3Ref(controller);
            controllers.Add(t);
            RegisterAnimatableRef(t);
            return t;
        }

        public TriggerTrackRef GetOrCreateTriggerTrack(string triggerTrackName)
        {
            var t = triggers.FirstOrDefault(x => x.Targets(triggerTrackName));
            if (t != null) return t;
            t = new TriggerTrackRef(triggerTrackName);
            triggers.Add(t);
            RegisterAnimatableRef(t);
            return t;
        }

        public void RegisterAnimatableRef(AnimatableRefBase animatableRef)
        {
            animatableRef.onSelectedChanged.AddListener(OnSelectedChanged);
        }

        private void OnSelectedChanged()
        {
            onTargetsSelectionChanged.Invoke();
        }
    }
}
