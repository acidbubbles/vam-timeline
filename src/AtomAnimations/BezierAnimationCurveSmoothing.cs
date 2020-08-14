using System.Collections.Generic;

namespace VamTimeline
{
    public class BezierAnimationCurveSmoothing
    {
        private float[] _k;
        private float[] _p1;
        private float[] _p2;
        private float[] _r;
        private float[] _a;
        private float[] _b;
        private float[] _c;

        public void AutoComputeControlPoints(List<BezierKeyframe> keys, bool loop)
        {
            // Adapted from Virt-A-Mate's implementation with permission from MeshedVR
            // Original implementation: https://www.particleincell.com/wp-content/uploads/2012/06/bezier-spline.js
            // Based on: https://www.particleincell.com/2012/bezier-splines/
            // Using improvements on near keyframes: http://www.jacos.nl/jacos_html/spline/
            var n = keys.Count - 1;
            if (_k == null || _k.Length < keys.Count)
            {
                _k = new float[keys.Count];
                _p1 = new float[keys.Count];
                _p2 = new float[keys.Count];
                // rhs vector
                _a = new float[n];
                _b = new float[n];
                _c = new float[n];
                _r = new float[n];
            }
            for (var i = 0; i < keys.Count; i++)
            {
                _k[i] = keys[i].value;
            }

            // leftmost segment
            _a[0] = 0f; // outside the matrix
            _b[0] = 2f;
            _c[0] = 1f;
            _r[0] = _k[0] + 2f * _k[1];

            // internal segments
            for (var i = 1; i < n - 1; i++)
            {
                _a[i] = 1f;
                _b[i] = 4f;
                _c[i] = 1f;
                _r[i] = 4f * _k[i] + 2f * _k[i + 1];
            }

            // right segment
            _a[n - 1] = 2f;
            _b[n - 1] = 7f;
            _c[n - 1] = 0f;
            _r[n - 1] = 8f * _k[n - 1] + _k[n];

            // solves Ax=b with the Thomas algorithm
            for (var i = 1; i < n; i++)
            {
                var m = _a[i] / _b[i - 1];
                _b[i] -= m * _c[i - 1];
                _r[i] -= m * _r[i - 1];
            }

            _p1[n - 1] = _r[n - 1] / _b[n - 1];
            for (var i = n - 2; i >= 0; --i)
            {
                _p1[i] = (_r[i] - _c[i] * _p1[i + 1]) / _b[i];
            }

            // we have p1, now compute p2
            for (var i = 0; i < n - 1; i++)
            {
                _p2[i] = 2f * _k[i + 1] - _p1[i + 1];
            }
            _p2[n - 1] = 0.5f * (_k[n] + _p1[n - 1]);

            // Assign the control points to the keyframes
            for (var i = 1; i < n - 1; i++)
            {
                keys[i].controlPointIn = _p2[i - 1];
                keys[i].controlPointOut = _p1[i];
            }
            if (loop)
            {
                var avgIn = (keys[0].controlPointIn + keys[keys.Count - 1].controlPointIn) / 2f;
                keys[0].controlPointIn = keys[keys.Count - 1].controlPointIn = avgIn;
                var avgOut = (keys[0].controlPointOut + keys[keys.Count - 1].controlPointOut) / 2f;
                keys[0].controlPointOut = keys[keys.Count - 1].controlPointOut = avgOut;
            }
        }
    }
}
