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
    public class JSONStorableFloatAnimationClip : IAnimationClip
    {
        private float _animationLength = 5f;
        public readonly List<JSONStorableFloatAnimationTarget> Storables = new List<JSONStorableFloatAnimationTarget>();
        private JSONStorableFloatAnimationTarget _selected;

        public string AnimationName { get; }
        public float Speed { get; set; }

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
                foreach (var morph in Storables)
                {
                    morph.SetLength(value);
                }
            }
        }

        public JSONStorableFloatAnimationClip(string animatioName)
        {
            AnimationName = animatioName;
        }

        public JSONStorableFloatAnimationTarget Add(JSONStorableFloat jsf)
        {
            if (Storables.Any(s => s.Name == jsf.name)) return null;
            var target = new JSONStorableFloatAnimationTarget(jsf, AnimationLength);
            Add(target);
            return target;
        }

        public void Add(JSONStorableFloatAnimationTarget target)
        {
            Storables.Add(target);
        }

        public bool IsEmpty()
        {
            return Storables.Count == 0;
        }

        public void SelectTargetByName(string name)
        {
            _selected = Storables.FirstOrDefault(m => m.Name == name);
        }

        public IEnumerable<string> GetTargetsNames()
        {
            return Storables.Select(m => m.Name);
        }

        public IEnumerable<JSONStorableFloatAnimationTarget> GetAllOrSelectedMorphs()
        {
            if (_selected != null) return new[] { _selected };
            return Storables;
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
