using System;
using System.Collections.Generic;
using System.Linq;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class MorphsAnimationClip : IAnimationClip
    {
        private float _animationLength = 5f;
        public readonly List<JSONStorableFloatAnimationTarget> Morphs = new List<JSONStorableFloatAnimationTarget>();
        private JSONStorableFloatAnimationTarget _selected;

        public string AnimationName { get; }

        public float AnimationLength
        {
            get
            {
                return _animationLength;
            }
            set
            {
                if (value == _animationLength)
                    return;
                _animationLength = value;
                foreach (var morph in Morphs)
                {
                    morph.SetLength(value);
                }
            }
        }

        public MorphsAnimationClip(string animatioName)
        {
            AnimationName = animatioName;
        }

        public bool IsEmpty()
        {
            return Morphs.Count == 0;
        }

        public void SelectTargetByName(string name)
        {
            _selected = Morphs.FirstOrDefault(m => m.Name == name);
        }

        public IEnumerable<string> GetTargetsNames()
        {
            return Morphs.Select(m => m.Name);
        }

        public IEnumerable<JSONStorableFloatAnimationTarget> GetAllOrSelectedMorphs()
        {
            if (_selected != null) return new[] { _selected };
            return Morphs;
        }

        public IEnumerable<IAnimationTarget> GetAllOrSelectedTargets()
        {
            return GetAllOrSelectedMorphs().Cast<IAnimationTarget>();
        }

        public float GetNextFrame(float time)
        {
            throw new NotImplementedException();
        }

        public float GetPreviousFrame(float time)
        {
            throw new NotImplementedException();
        }

        public void DeleteFrame(float time)
        {
            throw new NotImplementedException();
        }
    }
}
