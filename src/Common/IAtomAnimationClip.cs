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
        bool Loop { get; }
        float AnimationLength { get; }
        UnityEvent TargetsSelectionChanged { get; }
        UnityEvent TargetsListChanged { get; }
        UnityEvent AnimationModified { get; }

        IEnumerable<IAtomAnimationTargetsList> GetTargetGroups();
    }
}
