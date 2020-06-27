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
        public Dictionary<int, Trigger> keyframes { get; } = new Dictionary<int, Trigger>();

        public string name => "Triggers";

        public TriggersAnimationTarget()
        {
        }

        public string GetShortName()
        {
            return "Triggers";
        }

        public void SetKeyframe(float time, Trigger value)
        {
            SetKeyframe(time.ToMilliseconds(), value);
        }

        public void SetKeyframe(int ms, Trigger value)
        {
            if (value == null)
                keyframes.Remove(ms);
            else
                keyframes[ms] = value;
        }

        public void DeleteFrame(float time)
        {
            keyframes.Remove(time.ToMilliseconds());
        }

        public float[] GetAllKeyframesTime()
        {
            // TODO: Optimize memory
            var times = keyframes.Keys.ToList();
            times.Sort();
            return times.Select(t => (t / 1000f).Snap()).ToArray();
        }

        public bool HasKeyframe(float time)
        {
            return keyframes.ContainsKey(time.ToMilliseconds());
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
