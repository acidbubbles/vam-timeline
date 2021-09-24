namespace VamTimeline
{
    public abstract class TargetReduceProcessorBase<T> where T : class, ICurveAnimationTarget
    {
        protected readonly T source;
        protected readonly ReduceSettings settings;
        protected T branch;

        protected TargetReduceProcessorBase(T source, ReduceSettings settings)
        {
            this.source = source;
            this.settings = settings;
        }

        public void Branch()
        {
            branch = source.Clone(false) as T;
        }

        public void Commit()
        {
            source.RestoreFrom(branch);
            branch = null;
        }

        public ReducerBucket CreateBucket(int from, int to)
        {
            var bucket = new ReducerBucket
            {
                from = from,
                to = to,
                keyWithLargestDelta = -1
            };
            for (var i = from; i <= to; i++)
            {
                var delta = GetComparableNormalizedValue(i);
                if (delta > bucket.largestDelta)
                {
                    bucket.largestDelta = delta;
                    bucket.keyWithLargestDelta = i;
                }
            }
            return bucket;
        }

        public abstract float GetComparableNormalizedValue(int key);
    }
}
