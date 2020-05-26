using System.Collections.Generic;

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
        string Name { get; }
        string GetShortName();
        IEnumerable<float> GetAllKeyframesTime();
    }
}
