using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace VamTimeline
{
    public class BezierAnimationCurveSmoothingNonLooping : BezierAnimationCurveSmoothingBase, IBezierAnimationCurveSmoothing
    {
        public bool looping => false;

        private float[] _p;
        private float[] _d;

        public void AutoComputeControlPoints(List<BezierKeyframe> keys)
        {
            // Original implementation: https://www.particleincell.com/wp-content/uploads/2012/06/bezier-spline.js
            // Based on: https://www.particleincell.com/2012/bezier-splines/
            // Using improvements on near keyframes: http://www.jacos.nl/jacos_html/spline/
            var n = keys.Count - 1;
            // ComputeTimeAndDistance(keys);
            InitializeArrays(n);
            Weighting(keys, n);
            InternalSegments(keys, n);
            ThomasAlgorithm();
            Rearrange(n);
            AssignComputedControlPointsToKeyframes(keys, n);
        }

        [MethodImpl(256)]
        private void InitializeArrays(int n)
        {
            if (_w == null || _w.Length < n + 1)
            {
                _w = new float[n + 1];
                _p1 = new float[n + 1];
                _p2 = new float[n];
                // rhs vector
                // TODO: *2 only for non-looping?
                _a = new float[n * 2];
                _b = new float[n * 2];
                _c = new float[n * 2];
                _d = new float[n * 2];
                _r = new float[n * 2];
                _p = new float[n * 2];
            }
        }

        [MethodImpl(256)]
        private void Weighting(List<BezierKeyframe> keys, int n)
        {
            for (var i = 0; i < n; i++)
            {
                _w[i] = Weighting(keys[i+1], keys[i]);
            }
            _w[n] = _w[n - 1];
        }

        [MethodImpl(256)]
        private void InternalSegments(List<BezierKeyframe> keys, int n)
        {
            // left most segment
            _a[0] = 0; // outside the matrix
            _b[0] = 2;
            _c[0] = -1;
            _d[0] = 0;
            _r[0] = keys[0].value + 0;// add curvature at K0

            // internal segments
            for (var i = 1; i < n; i++)
            {
                var idx = 2 * i - 1;
                _a[2 * i - 1] = 1 * _w[i] * _w[i];
                _b[2 * i - 1] = -2 * _w[i] * _w[i];
                _c[2 * i - 1] = 2 * _w[i - 1] * _w[i - 1];
                _d[2 * i - 1] = -1 * _w[i - 1] * _w[i - 1];
                _r[2 * i - 1] = keys[i].value * (-_w[i] * _w[i] + _w[i - 1] * _w[i - 1]);

                _a[2 * i] = _w[i];
                _b[2 * i] = _w[i - 1];
                _c[2 * i] = 0;
                _d[2 * i] = 0; // note: d[2n-2] is already outside the matrix
                _r[2 * i] = (_w[i - 1] + _w[i]) * keys[i].value;

            }

            // right segment
            _a[2 * n - 1] = -1;
            _b[2 * n - 1] = 2;
            _r[2 * n - 1] = keys[n].value; // curvature at last point
            _c[2 * n - 1] = 0; // outside the matrix
            _d[2 * n - 2] = 0; // outside the matrix
            _d[2 * n - 1] = 0; // outside the matrix
        }

        [MethodImpl(256)]
        private void ThomasAlgorithm()
        {
            var n = _r.Length;

            // the following array elements are not in the original matrix, so they should not have an effect
            _a[0] = 0; // outside the matrix
            _c[n - 1] = 0; // outside the matrix
            _d[n - 2] = 0; // outside the matrix
            _d[n - 1] = 0; // outside the matrix

            /* solves Ax=b with the Thomas algorithm (from Wikipedia) */
            /* adapted for a 4-diagonal matrix. only the a[i] are under the diagonal, so the Gaussian elimination is very similar */
            for (var i = 1; i < n; i++)
            {
                var m = _a[i] / _b[i - 1];
                _b[i] = _b[i] - m * _c[i - 1];
                _c[i] = _c[i] - m * _d[i - 1];
                _r[i] = _r[i] - m * _r[i - 1];
            }

            _p[n - 1] = _r[n - 1] / _b[n - 1];
            _p[n - 2] = (_r[n - 2] - _c[n - 2] * _p[n - 1]) / _b[n - 2];
            for (var i = n - 3; i >= 0; --i)
            {
                _p[i] = (_r[i] - _c[i] * _p[i + 1] - _d[i] * _p[i + 2]) / _b[i];
            }
        }

        [MethodImpl(256)]
        private void Rearrange(int n)
        {
            for (var i = 0; i < n; i++)
            {
                _p1[i] = _p[2 * i];
                _p2[i] = _p[2 * i + 1];
            }
        }
    }
}
