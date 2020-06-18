namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public interface IRemoteControllerPlugin
    {
        void OnTimelineAnimationReady(JSONStorable storable);
        void OnTimelineAnimationDisabled(JSONStorable storable);

        // TODO: Would it be better to use events instead?
        void OnTimelineAnimationParametersChanged(JSONStorable storable);
        void OnTimelineTimeChanged(JSONStorable storable);
    }
}
