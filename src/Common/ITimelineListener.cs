namespace VamTimeline
{
    public interface ITimelineListener
    {
        void OnTimelineAnimationReady(JSONStorable storable);
        void OnTimelineAnimationDisabled(JSONStorable storable);
    }
}
