using System.Collections.Generic;

namespace VamTimeline
{
    public interface ICurveAnimationTarget : IAtomAnimationTarget
    {
        BezierAnimationCurve GetLeadCurve();
        IEnumerable<BezierAnimationCurve> GetCurves();
        // TODO: Do not work with strings anymore!
        void ChangeCurve(float time, int curveType, bool loop);
        string GetKeyframeSettings(float time);
    }
}
