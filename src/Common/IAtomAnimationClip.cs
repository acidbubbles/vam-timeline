using System;
using System.Collections.Generic;
using UnityEngine.Events;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public interface IAtomAnimationClip : IDisposable
    {
        bool loop { get; }
        float animationLength { get; }
        UnityEvent onTargetsSelectionChanged { get; }
        UnityEvent onTargetsListChanged { get; }
        UnityEvent onAnimationKeyframesModified { get; }

        IEnumerable<IAtomAnimationTargetsList> GetTargetGroups();
    }
}
