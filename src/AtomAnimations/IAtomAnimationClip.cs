using System;
using System.Collections.Generic;
using UnityEngine.Events;

namespace VamTimeline
{
    public interface IAtomAnimationClip : IDisposable
    {
        bool loop { get; }
        float animationLength { get; }
        UnityEvent onTargetsSelectionChanged { get; }
        UnityEvent onTargetsListChanged { get; }
        UnityEvent onAnimationKeyframesDirty { get; }
        UnityEvent onAnimationKeyframesRebuilt { get; }

        IEnumerable<IAnimationTargetWithCurves> GetAllCurveTargets();
        IEnumerable<IAtomAnimationTarget> GetAllTargets();
        int GetAllTargetsCount();
        IEnumerable<IAtomAnimationTargetsList> GetTargetGroups();
    }
}
