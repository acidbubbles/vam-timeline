namespace VamTimeline
{
    public abstract class TargetReduceProcessorBase<T> where T : class, ICurveAnimationTarget
    {
        public readonly T target;
        public readonly ReduceSettings settings;
        protected T branch;

        protected TargetReduceProcessorBase(T target, ReduceSettings settings)
        {
            this.target = target;
            this.settings = settings;
        }

        public void Branch()
        {
            branch = target.Clone(false) as T;
        }

        public void Commit()
        {
            target.RestoreFrom(branch);
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
