using System;
using System.Collections.Generic;
using UnityEngine.Events;

namespace VamTimeline
{
    public interface IAtomAnimationClip : IDisposable
    {
        string animationNameQualified { get; }
        bool loop { get; }
        float animationLength { get; }
        bool playbackEnabled { get; }
        float playbackBlendWeight { get; }
        float clipTime { get; }
        float scaledWeight { get; }
        UnityEvent onTargetsListChanged { get; }

        IEnumerable<IAtomAnimationTargetsList> GetTargetGroups();
    }
}
