using System.Linq;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class JSONStorableFloatAnimationClip : AnimationClipBase<JSONStorableFloatAnimationTarget>, IAnimationClip<JSONStorableFloatAnimationTarget>
    {
        public JSONStorableFloatAnimationClip(string animationName)
            : base(animationName)
        {
        }

        public JSONStorableFloatAnimationTarget Add(JSONStorableFloat jsf)
        {
            if (Targets.Any(s => s.Name == jsf.name)) return null;
            var target = new JSONStorableFloatAnimationTarget(jsf, AnimationLength);
            Add(target);
            return target;
        }

        public void Add(JSONStorableFloatAnimationTarget target)
        {
            Targets.Add(target);
        }
    }
}
