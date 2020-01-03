using System;
using System.Linq;
using SimpleJSON;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class AtomAnimationSerializer : AnimationSerializerBase<AtomAnimation, AtomAnimationClip, FreeControllerV3AnimationTarget>
    {
        private readonly Atom _atom;

        public AtomAnimationSerializer(Atom atom)
        {
            _atom = atom;
        }

        public override AtomAnimation CreateDefaultAnimation()
        {
            return new AtomAnimation(_atom);
        }

        protected override AtomAnimationClip CreateDefaultAnimationClip(string animationName)
        {
            return new AtomAnimationClip(animationName);
        }

        protected override void DeserializeClip(AtomAnimationClip clip, JSONClass clipJSON)
        {
            var animationPatternUID = clipJSON["AnimationPattern"]?.Value;
            if (!string.IsNullOrEmpty(animationPatternUID))
            {
                var animationPattern = SuperController.singleton.GetAtomByUid(animationPatternUID)?.GetComponentInChildren<AnimationPattern>();
                if (animationPattern == null)
                    SuperController.LogError($"Animation Pattern '{animationPatternUID}' linked to animation '{clip.AnimationName}' of atom '{_atom.uid}' was not found in scene");
                else
                    clip.AnimationPattern = animationPattern;
            }

            JSONArray controllersJSON = clipJSON["Controllers"].AsArray;
            if (controllersJSON == null) throw new NullReferenceException("Saved state does not have controllers");
            foreach (JSONClass controllerJSON in controllersJSON)
            {
                var controllerName = controllerJSON["Controller"].Value;
                var controller = _atom.freeControllers.Single(fc => fc.name == controllerName);
                if (controller == null) throw new NullReferenceException($"Atom '{_atom.uid}' does not have a controller '{controllerName}'");
                var target = clip.Add(controller);
                DeserializeCurve(target.X, controllerJSON["X"]);
                DeserializeCurve(target.X, controllerJSON["X"]);
                DeserializeCurve(target.Y, controllerJSON["Y"]);
                DeserializeCurve(target.Z, controllerJSON["Z"]);
                DeserializeCurve(target.RotX, controllerJSON["RotX"]);
                DeserializeCurve(target.RotY, controllerJSON["RotY"]);
                DeserializeCurve(target.RotZ, controllerJSON["RotZ"]);
                DeserializeCurve(target.RotW, controllerJSON["RotW"]);
            }
        }

        protected override void SerializeClip(AtomAnimationClip clip, JSONClass clipJSON)
        {
            if (clip.AnimationPattern != null)
                clipJSON.Add("AnimationPattern", clip.AnimationPattern.containingAtom.uid);

            var controllersJSON = new JSONArray();
            clipJSON.Add("Controllers", controllersJSON);
            foreach (var controller in clip.Targets)
            {
                var controllerJSON = new JSONClass
                    {
                        { "Controller", controller.Controller.name },
                        { "X", SerializeCurve(controller.X) },
                        { "Y", SerializeCurve(controller.Y) },
                        { "Z", SerializeCurve(controller.Z) },
                        { "RotX", SerializeCurve(controller.RotX) },
                        { "RotY", SerializeCurve(controller.RotY) },
                        { "RotZ", SerializeCurve(controller.RotZ) },
                        { "RotW", SerializeCurve(controller.RotW) }
                    };
                controllersJSON.Add(controllerJSON);
            }
        }
    }
}
