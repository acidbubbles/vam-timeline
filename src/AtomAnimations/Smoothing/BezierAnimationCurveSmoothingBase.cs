using System.Collections.Generic;
using UnityEngine;

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

        protected static float Weighting(BezierKeyframe k1, BezierKeyframe k2)
        {
            // return _w[i] = keys[i + 1].time - keys[i].time;
            return Vector2.Distance(new Vector2(k1.time, k1.value), new Vector2(k2.time, k2.value));
        }

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
