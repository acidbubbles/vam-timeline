namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public interface IAnimationController
    {
        void OnTimelineAnimationParametersChanged(string uid);
        void OnTimelineTimeChanged(string uid);
        void OnTimelineAnimationReady(string uid);
    }
}
