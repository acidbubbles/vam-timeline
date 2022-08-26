using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class TargetsHelper
    {
        private readonly TestContext _context;

        public TargetsHelper(TestContext context)
        {
            _context = context;
        }

        public FreeControllerV3Ref GivenFreeController(string name = "Test Controller")
        {
            var existing = _context.animation.animatables.controllers.FirstOrDefault(c => c.name == name);
            if (existing != null) return existing;

            var controller = new GameObject(name);
            controller.SetActive(false);
            controller.transform.SetParent(_context.gameObject.transform, false);
            var fc = controller.AddComponent<FreeControllerV3>();
            fc.UITransforms = new Transform[0];
            fc.UITransformsPlayMode = new Transform[0];
            var animatable = _context.animation.animatables.GetOrCreateController(fc, true);
            return animatable;
        }

        public JSONStorableFloatRef GivenFloatParam(string storableName = "Test Storable", string floatParamName = "Test Float")
        {
            var existing = _context.animation.animatables.storableFloats.FirstOrDefault(c => c.storableId == storableName && c.floatParamName == floatParamName);
            if (existing != null) return existing;

            var storableGo = new GameObject(storableName);
            storableGo.transform.SetParent(_context.gameObject.transform, false);
            var storable = storableGo.AddComponent<JSONStorable>();
            var param = new JSONStorableFloat("Test", 0, 0, 1);
            storable.RegisterFloat(param);
            var animatable = _context.animation.animatables.GetOrCreateStorableFloat(storable, param, true);
            return animatable;
        }

        public TriggersTrackRef GivenTriggers(int animationLayerQualifiedId = 0, string name = "Test Triggers")
        {
            var existing = _context.animation.animatables.triggers.FirstOrDefault(c => c.animationLayerQualifiedId == animationLayerQualifiedId && c.name == name);
            if (existing != null) return existing;

            var animatable = _context.animation.animatables.GetOrCreateTriggerTrack(animationLayerQualifiedId, name);
            return animatable;
        }
    }
}
