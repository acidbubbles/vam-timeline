namespace VamTimeline
{
    public abstract class TargetReduceProcessorBase<T> where T : class, ICurveAnimationTarget
    {
        public readonly T source;
        public readonly ReduceSettings settings;
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

        public virtual ReducerBucket CreateBucket(int from, int to)
        {
            return new ReducerBucket
            {
                @from = from,
                to = to,
                keyWithLargestDelta = -1
            };
        }
    }
}
