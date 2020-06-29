using System;

namespace VamTimeline
{
    public class OperationsFactory
    {
        private readonly AtomAnimationClip _clip;

        public OperationsFactory(AtomAnimationClip clip)
        {
            if (clip == null) throw new ArgumentNullException(nameof(clip));
            _clip = clip;
        }

        public ResizeAnimationOperation Resize()
        {
            return new ResizeAnimationOperation(_clip);
        }
    }
}
