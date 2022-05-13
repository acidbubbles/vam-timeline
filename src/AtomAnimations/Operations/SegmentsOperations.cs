namespace VamTimeline
{
    public class SegmentsOperations
    {
        private readonly AtomAnimation _animation;
        private readonly AtomAnimationClip _clip;

        public SegmentsOperations(AtomAnimation animation, AtomAnimationClip clip)
        {
            _animation = animation;
            _clip = clip;
        }

        public AtomAnimationClip Add(string clipName, string layerName, string segmentName)
        {
            var clip = _animation.CreateClip(clipName, layerName, segmentName);
            return clip;
        }

        public AtomAnimationClip AddShared(string clipName)
        {
            var clip = _animation.CreateClip(clipName, AtomAnimationClip.DefaultAnimationLayer, AtomAnimationClip.SharedAnimationSegment, 0);
            return clip;
        }
    }
}
