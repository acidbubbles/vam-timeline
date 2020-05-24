using System.Collections.Generic;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public interface IAtomAnimationTargetsList
    {
        int Count { get; }
        string Label { get; }

        IEnumerable<IAtomAnimationTarget> GetTargets();
    }
}
