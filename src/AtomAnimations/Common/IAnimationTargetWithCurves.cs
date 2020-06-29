using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace VamTimeline
{
    public interface IAnimationTargetWithCurves : IAtomAnimationTarget
    {
        AnimationCurve GetLeadCurve();
        IEnumerable<AnimationCurve> GetCurves();
        void DeleteFrameByKey(int key);
        void AddEdgeFramesIfMissing(float animationLength);
    }
}
