using System.Collections.Generic;
using UnityEngine;

namespace VamTimeline
{
    public interface ICurveAnimationTarget : IAtomAnimationTarget
    {
        SortedDictionary<int, KeyframeSettings> settings { get; }

        AnimationCurve GetLeadCurve();
        IEnumerable<AnimationCurve> GetCurves();
        void AddEdgeFramesIfMissing(float animationLength);
        void ChangeCurve(float time, string curveType, bool loop);
        void EnsureKeyframeSettings(float time, string defaultCurveTypeValue);
        string GetKeyframeSettings(float time);
    }
}
