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
        private readonly List<KeyValuePair<Color, AnimationCurve>> _curves = new List<KeyValuePair<Color, AnimationCurve>>();

        public void ClearCurves()
        {
            _curves.Clear();
            SetVerticesDirty();
        }

        public void AddCurve(Color color, AnimationCurve curve)
        {
            _curves.Add(new KeyValuePair<Color, AnimationCurve>(color, curve));
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (style == null) return;
            var margin = 20f;
            var width = rectTransform.rect.width;
            var height = rectTransform.rect.height - margin * 2f;
            var offsetX = -width / 2f;
            var precision = 2f; // Draw at every N pixels
            var boundsEvalPrecision = 20f; // Check how many points to detect highest value
            var minVertexDelta = 1.5f; // How much distance is required to draw a point
            var handleSize = style.HandleSize;

            foreach (var curveInfo in _curves)
            {
                // Common
                var curve = curveInfo.Value;
                var color = curveInfo.Key;
                var last = curve[curve.length - 1];
                var maxX = last.time;
                var xRatio = width / maxX;

                // Detect y bounds, offset and ratio
                var minY = float.MaxValue;
                var maxY = float.MinValue;
                var boundsTestStep = maxX / boundsEvalPrecision;
                for (var time = 0f; time < maxX; time += boundsTestStep)
                {
                    var value = curve.Evaluate(time);
                    minY = Mathf.Min(minY, value);
                    maxY = Mathf.Max(maxY, value);
                }
                var yRatio = height / (maxY - minY);
                var offsetY = (-minY - (maxY - minY) / 2f) * yRatio;
                if (float.IsInfinity(yRatio))
                {
                    yRatio = 1f;
                    offsetY = 0f;
                }

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
                vh.DrawLine(points, style.LineSize, color);

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
