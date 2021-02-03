namespace VamTimeline
{
    public interface ITimelineListener
    {
        void OnTimelineAnimationReady(MVRScript storable);
        void OnTimelineAnimationDisabled(MVRScript storable);
    }
}
