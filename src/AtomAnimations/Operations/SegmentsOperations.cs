namespace VamTimeline
{
    public class SegmentsOperations : ClipManagementOperationsBase
    {
        private readonly AtomAnimation _animation;
        private readonly AtomAnimationClip _clip;

        public SegmentsOperations(AtomAnimation animation, AtomAnimationClip clip)
            : base(animation, clip)
        {
            _animation = animation;
            _clip = clip;
        }

        public AtomAnimationClip Add()
        {
            return _animation.CreateClip(GetNewLayerName(), GetNewAnimationName(), GetNewSegmentName());
        }
    }
}
