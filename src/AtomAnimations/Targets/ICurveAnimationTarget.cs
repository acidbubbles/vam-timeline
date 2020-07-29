using System.Collections.Generic;
using UnityEngine;

namespace VamTimeline
{
    public interface ICurveAnimationTarget : IAtomAnimationTarget
    {
        SortedDictionary<int, KeyframeSettings> settings { get; }

        VamAnimationCurve GetLeadCurve();
        IEnumerable<VamAnimationCurve> GetCurves();
        void ChangeCurve(float time, string curveType, bool loop);
        void EnsureKeyframeSettings(float time, string defaultCurveTypeValue);
        string GetKeyframeSettings(float time);
    }
}
