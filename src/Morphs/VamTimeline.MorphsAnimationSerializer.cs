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
    public class MorphsAnimationSerializer : AnimationSerializerBase<JSONStorableFloatAnimation, JSONStorableFloatAnimationClip>
    {
        private readonly Atom _atom;

        public MorphsAnimationSerializer(Atom atom)
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
            var morphsList = new MorphsList(_atom);
            morphsList.Refresh();
            var allMorphs = morphsList.GetAnimatableMorphs().ToList();

            JSONArray morphsJSON = clipJSON["Morphs"].AsArray;
            if (morphsJSON == null) throw new NullReferenceException("Saved state does not have morphs");
            foreach (JSONClass morphJSON in morphsJSON)
            {
                var morphName = morphJSON["Morph"].Value;
                var morph = allMorphs.Single(fc => fc.name == morphName);
                if (morph == null) throw new NullReferenceException($"Atom '{_atom.uid}' does not have a morph '{morphName}'");
                var target = clip.Add(morph);
                DeserializeCurve(target.Value, morphJSON["Value"]);
            }
        }

        protected override void SerializeClip(JSONStorableFloatAnimationClip clip, JSONClass clipJSON)
        {
            var morphsJSON = new JSONArray();
            clipJSON.Add("Morphs", morphsJSON);
            foreach (var morph in clip.Storables)
            {
                var morphJSON = new JSONClass
                    {
                        { "Morph", morph.Storable.name },
                        { "Value", SerializeCurve(morph.Value) },
                    };
                morphsJSON.Add(morphJSON);
            }
        }
    }
}
