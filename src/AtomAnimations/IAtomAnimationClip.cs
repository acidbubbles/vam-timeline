using System;
using System.Collections.Generic;
using UnityEngine.Events;

namespace VamTimeline
{
    public interface IAtomAnimationClip : IDisposable
    {
        string animationLayer { get; }
        string animationName { get; }
        string animationNameQualified { get; }
        bool loop { get; }
        float animationLength { get; }
        bool playbackEnabled { get; }
        float playbackWeight { get; }
        float clipTime { get; }
        float weight { get; }
        UnityEvent onTargetsListChanged { get; }
        UnityEvent onAnimationKeyframesDirty { get; }
        UnityEvent onAnimationKeyframesRebuilt { get; }

        IEnumerable<ICurveAnimationTarget> GetAllCurveTargets();
        IEnumerable<IAtomAnimationTarget> GetAllTargets();
        int GetAllTargetsCount();
        IEnumerable<IAtomAnimationTargetsList> GetTargetGroups();
    }
}
