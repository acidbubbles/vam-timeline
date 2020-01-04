using System;
using SimpleJSON;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class JSONStorableFloatsAnimationSerializer : AnimationSerializerBase<JSONStorableFloatAnimation, JSONStorableFloatAnimationClip, JSONStorableFloatAnimationTarget>
    {
        private readonly Atom _atom;

        public JSONStorableFloatsAnimationSerializer(Atom atom)
        {
            _atom = atom;
        }

        public override JSONStorableFloatAnimation CreateDefaultAnimation()
        {
            return new JSONStorableFloatAnimation();
        }

        protected override JSONStorableFloatAnimationClip CreateDefaultAnimationClip(string animationName)
        {
            return new JSONStorableFloatAnimationClip(animationName);
        }

        protected override void DeserializeClip(JSONStorableFloatAnimationClip clip, JSONClass clipJSON)
        {
            JSONArray morphsJSON = clipJSON["Params"].AsArray;
            if (morphsJSON == null) throw new NullReferenceException("Saved state does not have morphs");
            foreach (JSONClass morphJSON in morphsJSON)
            {
                var storableId = morphJSON["Storable"].Value;
                var floatParamName = morphJSON["FloatParam"].Value;
                JSONStorable storable = _atom.containingAtom.GetStorableByID(storableId);
                var jsf = storable?.GetFloatJSONParam(floatParamName);
                if (jsf == null) throw new NullReferenceException($"Atom '{_atom.uid}' does not have a param '{storableId}/{floatParamName}'");
                var target = clip.Add(storable, jsf);
                DeserializeCurve(target.Value, morphJSON["Value"]);
            }
        }

        protected override void SerializeClip(JSONStorableFloatAnimationClip clip, JSONClass clipJSON)
        {
            var morphsJSON = new JSONArray();
            clipJSON.Add("Params", morphsJSON);
            foreach (var morph in clip.Targets)
            {
                var morphJSON = new JSONClass
                    {
                        { "Storable", morph.Storable.name },
                        { "FloatParam", morph.FloatParam.name },
                        { "Value", SerializeCurve(morph.Value) },
                    };
                morphsJSON.Add(morphJSON);
            }
        }
    }
}
