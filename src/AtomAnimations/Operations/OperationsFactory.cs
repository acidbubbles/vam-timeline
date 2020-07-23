using System;

namespace VamTimeline
{
    public class OperationsFactory
    {
        private readonly AtomAnimation _animation;
        private readonly AtomAnimationClip _clip;

        public OperationsFactory(AtomAnimation animation, AtomAnimationClip clip)
        {
            if (animation == null) throw new ArgumentNullException(nameof(animation));
            _animation = animation;
            if (clip == null) throw new ArgumentNullException(nameof(clip));
            _clip = clip;
        }

        public ResizeAnimationOperation Resize()
        {
            return new ResizeAnimationOperation(_clip);
        }

        public TargetsOperation Targets()
        {
            return new TargetsOperation(_animation, _clip);
        }

        public KeyframesOperation Keyframes()
        {
            return new KeyframesOperation(_clip);
        }

        public LayersOperation layers()
        {
            return new LayersOperation(_animation, _clip);
        }
    }
}
