using System;
using System.Collections.Generic;
using UnityEngine.Events;

namespace VamTimeline
{
    public interface IAtomAnimationClip : IDisposable
    {
        string animationNameQualified { get; }
        bool loop { get; }
        bool playbackEnabled { get; }
        float playbackBlendWeight { get; }
        float playbackBlendWeightSmoothed { get; }
        bool temporarilyEnabled { get; }
        float clipTime { get; }
        float scaledWeight { get; }
        UnityEvent onTargetsListChanged { get; }
        float blendInDuration { get; }
        float animationLength { get; }

        bool preserveLoopLastFrame { get; }
        float loopSelfBlendDuration { get; }

        IEnumerable<IAtomAnimationTargetsList> GetTargetGroups();
    }
}
