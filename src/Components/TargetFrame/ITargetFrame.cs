namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public interface ITargetFrame
    {
        UIDynamic Container { get; }
        void SetTime(float time);
        void ToggleKeyframe(bool enable);
    }
}
