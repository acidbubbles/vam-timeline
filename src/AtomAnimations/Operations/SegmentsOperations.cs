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
            return _animation.CreateClip(layerName, clipName, segmentName);
        }

        public AtomAnimationClip AddShared(string clipName)
        {
            return _animation.CreateClip(AtomAnimationClip.DefaultAnimationLayer, clipName, AtomAnimationClip.SharedAnimationSegment, 0);
        }
    }
}
