using System.Linq;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class FloatParamAnimationClip : AnimationClipBase<FloatParamAnimationTarget>, IAnimationClip<FloatParamAnimationTarget>
    {
        public FloatParamAnimationClip(string animationName)
            : base(animationName)
        {
        }

        public FloatParamAnimationTarget Add(JSONStorable storable, JSONStorableFloat jsf)
        {
            if (Targets.Any(s => s.Name == jsf.name)) return null;
            var target = new FloatParamAnimationTarget(storable, jsf, AnimationLength);
            Add(target);
            return target;
        }

        public void Add(FloatParamAnimationTarget target)
        {
            Targets.Add(target);
        }
    }
}
