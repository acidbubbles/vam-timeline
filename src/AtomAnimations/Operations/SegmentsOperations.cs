namespace VamTimeline
{
    public class SegmentsOperations : ClipManagementOperationsBase
    {
        private readonly AtomAnimation _animation;
        private readonly AtomAnimationClip _clip;

        public SegmentsOperations(AtomAnimation animation, AtomAnimationClip clip)
            : base(animation)
        {
            _animation = animation;
            _clip = clip;
        }

        public AtomAnimationClip Add(string clipName, string layerName, string segmentName)
        {
            return _animation.CreateClip(layerName ?? AtomAnimationClip.DefaultAnimationLayer, clipName ?? GetNewAnimationName(), segmentName ?? GetNewSegmentName());
        }

        public AtomAnimationClip AddShared(string clipName)
        {
            return _animation.CreateClip(AtomAnimationClip.DefaultAnimationLayer, clipName ?? GetNewAnimationName(), AtomAnimationClip.SharedAnimationSegment, 0);
        }
    }
}
