using System;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public interface IAtomAnimationTarget
    {
        bool Selected { get; set; }
        event EventHandler SelectedChanged;
        string Name { get; }
        string GetShortName();
        float[] GetAllKeyframesTime();
    }
}
