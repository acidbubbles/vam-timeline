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
        private List<JSONStorableFloat> _morphs = new List<JSONStorableFloat>();
        private JSONStorableFloat _selected;

        public string AnimationName { get; }

        public float AnimationLength { get; set; }

        public MorphsAnimationClip(string animatioName)
        {
            AnimationName = animatioName;
        }

        public bool IsEmpty()
        {
            return _morphs.Count == 0;
        }

        public void SelectTargetByName(string name)
        {
            _selected = _morphs.FirstOrDefault(m => m.name == name);
        }

        public IEnumerable<string> GetTargetsNames()
        {
            return _morphs.Select(m => m.name);
        }

        public IEnumerable<JSONStorableFloat> GetAllOrSelectedMorphs()
        {
            if (_selected != null) return new[] { _selected };
            return _morphs;
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
