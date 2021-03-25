namespace VamTimeline
{
    public struct ReducerBucket
    {
        public int from;
        public int to;
        public int keyWithLargestDelta;
        public float largestDelta;
    }
}
