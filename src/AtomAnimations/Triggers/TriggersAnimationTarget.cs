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
    public class TriggersAnimationTarget : AnimationTargetBase, IAtomAnimationTarget
    {
        public Dictionary<int, AnimationTimelineTrigger> triggersMap { get; } = new Dictionary<int, AnimationTimelineTrigger>();
        public List<float> keyframes = new List<float>();
        public List<AnimationTimelineTrigger> triggers = new List<AnimationTimelineTrigger>();

        public string name => "Triggers";

        public TriggersAnimationTarget()
        {
        }

        public string GetShortName()
        {
            return "Triggers";
        }

        public void Sample(float clipTime, float _weight)
        {
            int i;
            for (i = 0; i < keyframes.Count; i++)
            {
                if (clipTime >= i / 1000f)
                    break;
            }
            var trigger = triggers[i];
            trigger.Update(false, 0f);
        }

        public void Validate()
        {
            foreach (var trigger in triggers)
                trigger.Validate();
        }

        public void RebuildKeyframes(AnimationTimelineTriggerHandler timelineHandler)
        {
            keyframes.Clear();
            triggers.Clear();

            foreach (var kvp in triggersMap.OrderBy(x => x.Key))
            {
                keyframes.Add(kvp.Key / 1000f);
                triggers.Add(kvp.Value);
            }

            for (var i = 0; i < keyframes.Count; i++)
            {
                var time = keyframes[i] * 1000f;
                var trigger = triggers[i];
                trigger.timeLineHandler = timelineHandler;
                trigger.triggerStartTime = time;
                trigger.triggerEndTime = i == keyframes.Count - 1 ? time : (keyframes[i + 1] * 1000f);
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
            // TODO: Optimize memory
            var times = triggersMap.Keys.ToList();
            times.Sort();
            return times.Select(t => (t / 1000f).Snap()).ToArray();
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
