using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace VamTimeline
{
    public interface ICurveAnimationTarget : IAtomAnimationTarget
    {
        AnimationCurve GetLeadCurve();
        IEnumerable<AnimationCurve> GetCurves();
        void DeleteFrameByKey(int key);
        void AddEdgeFramesIfMissing(float animationLength);
        void ChangeCurve(float time, string curveType, bool loop);
        string GetKeyframeSettings(float time);
    }
}
