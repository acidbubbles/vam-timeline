using System;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class MorphsPlugin : MVRScript
    {
        public override void Init()
        {
            try
            {
                if (containingAtom.type != "Person")
                {
                    SuperController.LogError("VamTimeline.MorphsAnimation can only be applied on a Person atom.");
                    return;
                }

                InitMorphsList();
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.MorphsPlugin.Init: " + exc);
            }
        }

        private void InitMorphsList()
        {
            var geometry = containingAtom.GetStorableByID("geometry");
            if (geometry == null) throw new NullReferenceException("geometry");
            var character = geometry as DAZCharacterSelector;
            if (character == null) throw new NullReferenceException("character");
            var morphControl = character.morphsControlUI;
            if (morphControl == null) throw new NullReferenceException("morphControl");
            foreach (var morphDisplayName in morphControl.GetMorphDisplayNames())
            {
                var morph = morphControl.GetMorphByDisplayName(morphDisplayName);
                if (morph == null) continue;

                if (morph.animatable)
                {
                    var morphJSON = new JSONStorableFloat($"Morph:{morphDisplayName}", morph.jsonFloat.defaultVal, (float val) => UpdateMorph(morph, val), morph.jsonFloat.min, morph.jsonFloat.max, morph.jsonFloat.constrained, true);
                    CreateSlider(morphJSON, true);
                }
            }
        }

        private void UpdateMorph(DAZMorph morph, float val)
        {
            morph.jsonFloat.val = val;
        }

        public void Update()
        {
        }
    }
}
