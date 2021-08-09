using System.Collections.Generic;

namespace VamTimeline
{
    public interface ICurveAnimationTarget : IAtomAnimationTarget
    {
        bool recording { get; set; }
        BezierAnimationCurve GetLeadCurve();
        IEnumerable<BezierAnimationCurve> GetCurves();
        void ChangeCurveByTime(float time, int curveType, bool dirty = true);
        int GetKeyframeCurveTypeByTime(float time);
        ICurveAnimationTarget Clone(bool copyKeyframes);
        void RestoreFrom(ICurveAnimationTarget backup);
    }
}
