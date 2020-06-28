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
    public class TriggersAnimationTarget : AnimationTargetBase, IAtomAnimationTarget
    {
        public readonly Dictionary<int, AnimationTimelineTrigger> triggersMap = new Dictionary<int, AnimationTimelineTrigger>();
        private float[] _keyframes = new float[0];
        private readonly List<AnimationTimelineTrigger> _triggers = new List<AnimationTimelineTrigger>();

        public string name => "Triggers";

        public TriggersAnimationTarget()
        {
        }

        public string GetShortName()
        {
            return "Triggers";
        }

        public void Sample(bool isPlaying, float clipTime, float previousClipTime)
        {
            var reverse = !isPlaying && (clipTime < previousClipTime);
            foreach (var trigger in _triggers)
                trigger.Update(reverse, previousClipTime);
        }

        public void Validate()
        {
            foreach (var trigger in _triggers)
            {
                trigger.Validate();
            }
        }

        public void RebuildKeyframes(AnimationTimelineTriggerHandler timelineHandler)
        {
            _keyframes = new float[triggersMap.Count];
            _triggers.Clear();

            var i = 0;
            foreach (var kvp in triggersMap.OrderBy(x => x.Key))
            {
                _keyframes[i++] = kvp.Key / 1000f;
                _triggers.Add(kvp.Value);
            }

            for (i = 0; i < _keyframes.Length; i++)
            {
                var time = _keyframes[i];
                var trigger = _triggers[i];
                trigger.timeLineHandler = timelineHandler;
                trigger.triggerStartTime = time;
                trigger.triggerEndTime = i == _keyframes.Length - 1 ? timelineHandler.GetTotalTime() : _keyframes[i + 1];
            }
        }

        public void SetKeyframe(float time, AnimationTimelineTrigger value)
        {
            SetKeyframe(time.ToMilliseconds(), value);
        }

        public void SetKeyframe(int ms, AnimationTimelineTrigger value)
        {
            if (value == null)
                triggersMap.Remove(ms);
            else
                triggersMap[ms] = value;
            dirty = true;
        }

        public void DeleteFrame(float time)
        {
            triggersMap.Remove(time.ToMilliseconds());
            dirty = true;
        }

        public float[] GetAllKeyframesTime()
        {
            return _keyframes;
        }

        public bool HasKeyframe(float time)
        {
            return triggersMap.ContainsKey(time.ToMilliseconds());
        }

        // TODO: Makes sense?
        public bool TargetsSameAs(IAtomAnimationTarget target)
        {
            return false;
        }

        public class Comparer : IComparer<TriggersAnimationTarget>
        {
            public int Compare(TriggersAnimationTarget t1, TriggersAnimationTarget t2)
            {
                return t1.name.CompareTo(t2.name);

            }
        }
    }
}
