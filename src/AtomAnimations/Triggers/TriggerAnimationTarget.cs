using System.Collections.Generic;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class ActionParamAnimationTarget : AnimationTargetBase, IAtomAnimationTarget
    {
        public JSONStorable Storable { get; }
        public JSONStorableAction ActionParam { get; }
        public List<int> Keyframes { get; } = new List<int>();

        public string Name => Storable != null ? $"{Storable.name}/{ActionParam.name}" : ActionParam.name;

        public ActionParamAnimationTarget(JSONStorable storable, JSONStorableAction jsa)
        {
            Storable = storable;
            ActionParam = jsa;
        }

        public string GetShortName()
        {
            return ActionParam.name;
        }

        public void SetKeyframe(float time, bool value)
        {
            var ms = time.ToMilliseconds();
            if (value)
            {
                if (!Keyframes.Contains(ms))
                    Keyframes.Add(ms);
            }
            else
            {
                Keyframes.Remove(ms);
            }
        }

        public void DeleteFrame(float time)
        {
            Keyframes.Remove(time.ToMilliseconds());
        }

        public float[] GetAllKeyframesTime()
        {
            var times = new float[Keyframes.Count];
            for (var i = 0; i < Keyframes.Count; i++)
            {
                times[i] = (Keyframes[i] / 1000f).Snap();
            }
            return times;
        }

        public bool HasKeyframe(float time)
        {
            return Keyframes.Contains(time.ToMilliseconds());
        }

        // TODO: Makes sense?
        public bool TargetsSameAs(IAnimationTargetWithCurves target)
        {
            var t = target as ActionParamAnimationTarget;
            if (t == null) return false;
            return t.Storable == Storable && t.ActionParam == ActionParam;
        }

        public class Comparer : IComparer<ActionParamAnimationTarget>
        {
            public int Compare(ActionParamAnimationTarget t1, ActionParamAnimationTarget t2)
            {
                return t1.Name.CompareTo(t2.Name);

            }
        }
    }
}
