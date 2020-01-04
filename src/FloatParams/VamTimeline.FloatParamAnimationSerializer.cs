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
    public class FloatParamsAnimationSerializer : AnimationSerializerBase<FloatParamAnimation, FloatParamAnimationClip, FloatParamAnimationTarget>
    {
        private readonly Atom _atom;

        public FloatParamsAnimationSerializer(Atom atom)
        {
            _atom = atom;
        }

        public override FloatParamAnimation CreateDefaultAnimation()
        {
            return new FloatParamAnimation();
        }

        protected override FloatParamAnimationClip CreateDefaultAnimationClip(string animationName)
        {
            return new FloatParamAnimationClip(animationName);
        }

        protected override void DeserializeClip(FloatParamAnimationClip clip, JSONClass clipJSON)
        {
            JSONArray paramsJSON = clipJSON["Params"].AsArray;
            if (paramsJSON == null) throw new NullReferenceException("Saved state does not have params");
            foreach (JSONClass paramJSON in paramsJSON)
            {
                var storableId = paramJSON["Storable"].Value;
                var floatParamName = paramJSON["FloatParam"].Value;
                JSONStorable storable = _atom.containingAtom.GetStorableByID(storableId);
                var jsf = storable?.GetFloatJSONParam(floatParamName);
                if (jsf == null) throw new NullReferenceException($"Atom '{_atom.uid}' does not have a param '{storableId}/{floatParamName}'");
                var target = clip.Add(storable, jsf);
                DeserializeCurve(target.Value, paramJSON["Value"]);
            }
        }

        protected override void SerializeClip(FloatParamAnimationClip clip, JSONClass clipJSON)
        {
            var paramsJSON = new JSONArray();
            clipJSON.Add("Params", paramsJSON);
            foreach (var target in clip.Targets)
            {
                var paramJSON = new JSONClass
                    {
                        { "Storable", target.Storable.name },
                        { "FloatParam", target.FloatParam.name },
                        { "Value", SerializeCurve(target.Value) },
                    };
                paramsJSON.Add(paramJSON);
            }
        }
    }
}
