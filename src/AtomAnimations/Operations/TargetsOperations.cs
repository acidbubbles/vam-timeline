using System.Linq;

namespace VamTimeline
{
    public class TargetsOperations
    {
        private readonly AtomAnimation _animation;
        private readonly AtomAnimationClip _clip;

        public TargetsOperations(AtomAnimation animation, AtomAnimationClip clip)
        {
            _animation = animation;
            _clip = clip;
        }

        public FreeControllerAnimationTarget Add(FreeControllerV3 fc)
        {
            var target = _clip.targetControllers.FirstOrDefault(t => t.controller == fc);
            if (target != null) return target;
            foreach (var clip in _animation.index.ByLayer(_clip.animationLayer))
            {
                var t = clip.Add(fc);
                if (t == null) continue;
                t.SetKeyframeToCurrentTransform(0f);
                t.SetKeyframeToCurrentTransform(clip.animationLength);
                if (clip == _clip) target = t;
            }
            return target;
        }
    }
}
