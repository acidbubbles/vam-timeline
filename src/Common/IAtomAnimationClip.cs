using System;
using System.Collections.Generic;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public interface IAtomAnimationClip
    {
        bool Loop { get; }
        float AnimationLength { get; }

        IEnumerable<IAtomAnimationTargetsList> GetTargetGroups();
    }
}
