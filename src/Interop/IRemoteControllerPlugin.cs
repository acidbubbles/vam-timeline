namespace VamTimeline
{
    public interface IRemoteControllerPlugin : ITimelineListener
    {
        // TODO: Would it be better to use events instead?
        void OnTimelineAnimationParametersChanged(JSONStorable storable);
        void OnTimelineTimeChanged(JSONStorable storable);
    }
}
