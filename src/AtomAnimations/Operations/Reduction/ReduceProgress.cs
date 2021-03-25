namespace VamTimeline
{
    public struct ReduceProgress
    {
        public float startTime;
        public float nowTime;
        public float stepsDone;
        public float stepsTotal;
        public float timeLeft => ((nowTime - startTime) / stepsDone) * (stepsTotal - stepsDone);
    }
}
