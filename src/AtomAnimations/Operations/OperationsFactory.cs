using System;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
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
