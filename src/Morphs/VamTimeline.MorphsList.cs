using System;
using System.Collections.Generic;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class MorphsList
    {
        private readonly Atom _atom;
        private GenerateDAZMorphsControlUI _morphControl;

        public MorphsList(Atom atom)
        {
            this._atom = atom;
        }

        public void Refresh()
        {
            var geometry = _atom.GetStorableByID("geometry");
            if (geometry == null) throw new NullReferenceException("geometry");
            var character = geometry as DAZCharacterSelector;
            if (character == null) throw new NullReferenceException("character");
            var morphControl = character.morphsControlUI;
            if (morphControl == null) throw new NullReferenceException("morphControl");
            _morphControl = morphControl;
        }

        public IEnumerable<JSONStorableFloat> GetAnimatableMorphs()
        {
            foreach (var morphDisplayName in _morphControl.GetMorphDisplayNames())
            {
                var morph = _morphControl.GetMorphByDisplayName(morphDisplayName);
                if (morph == null) continue;

                if (morph.animatable)
                {
                    yield return morph.jsonFloat;
                }
            }
        }
    }
}
