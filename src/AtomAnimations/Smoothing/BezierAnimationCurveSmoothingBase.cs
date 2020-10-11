using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace VamTimeline
{
    public abstract class BezierAnimationCurveSmoothingBase
    {
        protected float[] _w; // Weights
        protected float[] _p1; // Out
        protected float[] _p2; // In
        protected float[] _r; // rhs vector
        protected float[] _a;
        protected float[] _b;
        protected float[] _c;
        // protected float _totalTime;
        // protected float _totalDistance;

        [MethodImpl(256)]
        protected float Weighting(BezierKeyframe k1, BezierKeyframe k2)
        {
            return k2.time - k1.time;
            // if (_totalDistance == 0) return 1f;
            // return Vector2.Distance(new Vector2(1f - (k1.time / _totalTime), k1.value / _totalDistance), new Vector2(1f - (k2.time / _totalTime), k2.value / _totalDistance));
        }

        // protected void ComputeTimeAndDistance(List<BezierKeyframe> keys)
        // {
        //     _totalTime = 0f;
        //     _totalDistance = 0f;
        //     for (var i = 1; i < keys.Count; i++)
        //     {
        //         _totalTime += keys[i].time - keys[i - 1].time;
        //         _totalDistance += Mathf.Abs(keys[i].value - keys[i - 1].value);
        //     }
        // }

        [MethodImpl(256)]
        protected void AssignComputedControlPointsToKeyframes(List<BezierKeyframe> keys, int n)
        {
            if (keys[0].curveType != CurveTypeValues.LeaveAsIs)
            {
                keys[0].controlPointOut = _p1[0];
            }
            for (var i = 1; i < n; i++)
            {
                if (keys[i].curveType != CurveTypeValues.LeaveAsIs)
                {
                    keys[i].controlPointIn = _p2[i - 1];
                    keys[i].controlPointOut = _p1[i];
                }
            }
            if (keys[n].curveType != CurveTypeValues.LeaveAsIs)
            {
                keys[n].controlPointIn = _p2[n - 1];
            }
        }
    }
}
