namespace VamTimeline
{
    public interface ITargetReduceProcessor
    {
        ICurveAnimationTarget target { get; }
        void Branch();
        void Commit();
        ReducerBucket CreateBucket(int from, int to);
        void CopyToBranch(int key);
        void AverageToBranch(float keyTime, int fromKey, int toKey);
    }
}
