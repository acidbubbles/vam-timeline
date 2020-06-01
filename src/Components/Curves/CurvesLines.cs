using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class CurvesLines : MaskableGraphic
    {
        public CurvesStyle style;
        internal Vector2 range;
        private readonly List<KeyValuePair<Color, AnimationCurve>> _curves = new List<KeyValuePair<Color, AnimationCurve>>();

        public void ClearCurves()
        {
            _curves.Clear();
        }

        public void AddCurve(Color color, AnimationCurve curve)
        {
            if (curve.length == 0) return;
            _curves.Add(new KeyValuePair<Color, AnimationCurve>(color, curve));
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (style == null || _curves.Count == 0) return;

            // General
            var margin = 60f;
            var width = rectTransform.rect.width;
            var height = rectTransform.rect.height - margin * 2f;
            var offsetX = -width / 2f;
            var precision = 2f; // Draw at every N pixels
            var minVertexDelta = 1.5f; // How much distance is required to draw a point
            var handleSize = style.HandleSize;

            // X ratio
            var lastCurve = _curves[_curves.Count - 1];
            var maxX = lastCurve.Value[lastCurve.Value.length - 1].time;
            var xRatio = width / maxX;

            // Y ratio
            var minY = range.x;
            var maxY = range.y;
            var yRatio = height / (maxY - minY);
            var offsetY = (-minY - (maxY - minY) / 2f) * yRatio;
            if (float.IsInfinity(yRatio))
            {
                yRatio = 1f;
                offsetY = 0f;
            }

            // Zero line
            var halfWidth = rectTransform.rect.width / 2;
            vh.DrawLine(new[]
            {
                new Vector2(-halfWidth, offsetY),
                new Vector2(halfWidth, offsetY)
            }, style.ZeroLineSize, style.ZeroLineColor);

            // Seconds
            var halfHeight = rectTransform.rect.height / 2;
            for (var t = 0f; t <= maxX; t += 1f)
            {
                var x = offsetX + t * xRatio;
                vh.DrawLine(new[]
                {
                    new Vector2(x, halfHeight),
                    new Vector2(x, -halfHeight)
                }, style.SecondLineSize, style.SecondLineColor);
            }

            // Curves
            foreach (var curveInfo in _curves)
            {
                // Common
                var curve = curveInfo.Value;
                var color = curveInfo.Key;
                var last = curve[curve.length - 1];

                // Draw line
                var step = maxX / width * precision;
                var points = new List<Vector2>(curve.length);
                var previousY = Mathf.Infinity;
                for (var time = 0f; time < maxX; time += step)
                {
                    var value = curve.Evaluate(time);
                    var y = offsetY + value * yRatio;
                    if (Mathf.Abs(y - previousY) < minVertexDelta) continue;
                    var cur = new Vector2(offsetX + time * xRatio, y);
                    previousY = y;
                    points.Add(cur);
                }
                points.Add(new Vector2(offsetX + last.time * xRatio, offsetY + last.value * yRatio));
                vh.DrawLine(points, style.CurveLineSize, color);

                // Draw handles
                for (var i = 0; i < curve.length; i++)
                {
                    var keyframe = curve[i];
                    var center = new Vector2(offsetX + keyframe.time * xRatio, offsetY + keyframe.value * yRatio);
                    vh.AddUIVertexQuad(UIVertexHelper.CreateVBO(color, new[]
                    {
                        center - new Vector2(-handleSize, -handleSize),
                        center - new Vector2(-handleSize, handleSize),
                        center - new Vector2(handleSize, handleSize),
                        center - new Vector2(handleSize, -handleSize)
                    }));
                }
            }
        }
    }
}
