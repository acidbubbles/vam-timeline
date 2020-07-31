using System.Collections.Generic;

namespace VamTimeline
{
    public interface ICurveAnimationTarget : IAtomAnimationTarget
    {
        BezierAnimationCurve GetLeadCurve();
        IEnumerable<BezierAnimationCurve> GetCurves();
        void ChangeCurve(float time, int curveType, bool loop);
        int GetKeyframeCurveType(float time);
    }
}
