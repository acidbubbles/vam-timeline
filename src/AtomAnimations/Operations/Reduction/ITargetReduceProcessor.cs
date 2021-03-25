namespace VamTimeline
{
    public interface ITargetReduceProcessor
    {
        ICurveAnimationTarget target { get; }
        void Branch();
        void Commit();
        ReducerBucket CreateBucket(int from, int to);
        void CopyToBranch(int sourceKey, int curveType = CurveTypeValues.Undefined);
        void AverageToBranch(float keyTime, int fromKey, int toKey);
        bool IsStable(int key1, int key2);
    }
}
