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

        public AtomAnimationClip Add()
        {
            return _animation.CreateClip(AtomAnimationClip.DefaultAnimationLayer, GetNewAnimationName(), GetNewSegmentName());
        }

        public AtomAnimationClip AddShared()
        {
            return _animation.CreateClip(AtomAnimationClip.DefaultAnimationLayer, GetNewAnimationName(), AtomAnimationClip.SharedAnimationSegment, 0);
        }
    }
}
