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
            return _animation.CreateClip(layerName ?? AtomAnimationClip.DefaultAnimationLayer, clipName ?? _animation.GetUniqueAnimationName(_clip), segmentName ?? _animation.GetUniqueSegmentName(_clip));
        }

        public AtomAnimationClip AddShared(string clipName)
        {
            return _animation.CreateClip(AtomAnimationClip.DefaultAnimationLayer, clipName ?? _animation.GetUniqueAnimationName(_clip), AtomAnimationClip.SharedAnimationSegment, 0);
        }
    }
}
