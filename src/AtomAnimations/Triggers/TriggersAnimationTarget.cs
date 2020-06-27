using System.Collections.Generic;

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
        public List<int> keyframes { get; } = new List<int>();

        public string name => "Triggers";

        public TriggersAnimationTarget()
        {
        }

        public string GetShortName()
        {
            return "Triggers";
        }

        public void SetKeyframe(float time, bool value)
        {
            var ms = time.ToMilliseconds();
            if (value)
            {
                if (!keyframes.Contains(ms))
                    keyframes.Add(ms);
            }
            else
            {
                keyframes.Remove(ms);
            }
        }

        public void DeleteFrame(float time)
        {
            keyframes.Remove(time.ToMilliseconds());
        }

        public float[] GetAllKeyframesTime()
        {
            var times = new float[keyframes.Count];
            for (var i = 0; i < keyframes.Count; i++)
            {
                times[i] = (keyframes[i] / 1000f).Snap();
            }
            return times;
        }

        public bool HasKeyframe(float time)
        {
            return keyframes.Contains(time.ToMilliseconds());
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
