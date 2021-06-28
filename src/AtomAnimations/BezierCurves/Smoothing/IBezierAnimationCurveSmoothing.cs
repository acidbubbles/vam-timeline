using System.Collections.Generic;

namespace VamTimeline
{
    public interface IBezierAnimationCurveSmoothing
    {
        bool looping { get; }
        void AutoComputeControlPoints(List<BezierKeyframe> keys);
    }
}
