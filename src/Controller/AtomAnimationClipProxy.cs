
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
    public class AtomAnimationClipProxy : IAtomAnimationClip
    {
        private readonly List<AtomAnimationTargetsListProxy> _targetLists;

        public AtomAnimationClipProxy(List<KeyValuePair<string, List<KeyValuePair<string, List<float>>>>> targets)
        {
            _targetLists = targets.Select(t => new AtomAnimationTargetsListProxy(t.Key, t.Value)).ToList();
        }

        public IEnumerable<IAtomAnimationTargetsList> GetTargetGroups()
        {
            return _targetLists.Cast<IAtomAnimationTargetsList>();
        }
    }

    public class AtomAnimationTargetsListProxy : IAtomAnimationTargetsList
    {
        public int Count => _targets.Count;
        public string Label { get; private set; }

        private readonly List<AnimationTargetProxy> _targets;

        public AtomAnimationTargetsListProxy(string key, List<KeyValuePair<string, List<float>>> value)
        {
            Label = key;
            _targets = value.Select(x => new AnimationTargetProxy(x.Key, x.Value)).ToList();
        }

        public IEnumerable<IAtomAnimationTarget> GetTargets()
        {
            return _targets.Cast<IAtomAnimationTarget>();
        }
    }

    public class AnimationTargetProxy : IAtomAnimationTarget
    {
        private readonly string _shortName;
        private readonly List<float> _keyframeTimes;

        public AnimationTargetProxy(string shortName, List<float> keyframeTimes)
        {
            _shortName = shortName;
            _keyframeTimes = keyframeTimes;
        }

        public IEnumerable<float> GetAllKeyframesTime()
        {
            return _keyframeTimes;
        }

        public string GetShortName()
        {
            return _shortName;
        }
    }
}
