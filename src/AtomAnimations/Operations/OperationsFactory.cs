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

        public ResizeAnimationOperations Resize()
        {
            return new ResizeAnimationOperations(_clip);
        }

        public TargetsOperations Targets()
        {
            return new TargetsOperations(_animation, _clip);
        }

        public KeyframesOperations Keyframes()
        {
            return new KeyframesOperations(_clip);
        }

        public LayersOperations layers()
        {
            return new LayersOperations(_animation, _clip);
        }

        public ImportOperations import()
        {
            return new ImportOperations(_animation);
        }
    }
}
