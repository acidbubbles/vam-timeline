using System.Collections.Generic;

namespace VamTimeline
{
    public interface ICurveAnimationTarget : IAtomAnimationTarget
    {
        BezierAnimationCurve GetLeadCurve();
        IEnumerable<BezierAnimationCurve> GetCurves();
        void ChangeCurve(float time, int curveType, bool dirty = true);
        int GetKeyframeCurveType(float time);
    }
}
