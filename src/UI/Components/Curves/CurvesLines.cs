// #define RENDER_BEZIER_CONTROL_POINTS

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VamTimeline
{
    public class CurvesLines : MaskableGraphic
    {
        public CurvesStyle style;
        private readonly List<KeyValuePair<Color, BezierAnimationCurve>> _curves = new List<KeyValuePair<Color, BezierAnimationCurve>>();
        public float rangeBegin;
        public float rangeDuration;

        public void ClearCurves()
        {
            _curves.Clear();
        }

        public void AddCurve(Color color, BezierAnimationCurve curve)
        {
            _curves.Add(new KeyValuePair<Color, BezierAnimationCurve>(color, curve));
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (style == null || _curves.Count == 0 || rangeDuration == 0) return;

            // General
            // TODO: Only recompute range when keyframes changed, not when zooming
            var range = EstimateRange();
            float margin = style.Padding;
            var rect = rectTransform.rect;
            var width = rect.width - margin * 2;
            var height = rect.height - margin * 2f;
            var offsetX = -width / 2f;
            const float precision = 2f; // Draw at every N pixels
            const float maxVertexXDelta = 3f; // Whenever that distance is reached an additional point is drawn for constant curves
            const float minVertexYDelta = 0.8f; // How much distance is required to draw a point
            var handleSize = style.HandleSize;
            var halfWidth = rect.width / 2;
            var halfHeight = rect.height / 2;

            // X ratio
            var lastCurve = _curves[_curves.Count - 1];
            if (lastCurve.Value.length < 2) return;
            var maxX = rangeDuration;
            if (maxX == 0)
            {
                DrawBorder(vh, halfWidth, halfHeight);
                return;
            }
            var xRatio = width / maxX;

            // Y ratio
            var minY = range.x;
            var maxY = range.y;
            var yRatio = height / (maxY - minY);
            var offsetY = (-minY - (maxY - minY) / 2f) * yRatio;
            if (float.IsInfinity(yRatio) || maxY - minY < 0.00001f)
            {
                yRatio = 1f;
                offsetY = 0f;
            }

            // Zero line
            vh.DrawLine(new[]
            {
                new Vector2(-halfWidth, offsetY),
                new Vector2(halfWidth, offsetY)
            }, style.ZeroLineSize, style.ZeroLineColor);

            // Seconds
            var pixelsPerSecond = width / maxX;
            float timespan;
            if (pixelsPerSecond < 20)
                timespan = 100f;
            else if (pixelsPerSecond < 2)
                timespan = 10f;
            else
                timespan = 1f;
            for (var t = 0f; t <= maxX; t += timespan)
            {
                var x = offsetX + (t - rangeBegin) * xRatio;
                if (x < offsetX) continue;
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
                if(curve.length > 1000) continue;

                var curveColor = curveInfo.Key;
                var last = curve.GetKeyframeByKey(curve.length - 1);

                // Draw line
                var step = maxX / width * precision;
                var points = new List<Vector2>(curve.length);
                var previous = new Vector2(Mathf.NegativeInfinity, Mathf.Infinity);
                for (var time = 0f; time < maxX; time += step)
                {
                    var value = curve.Evaluate(time + rangeBegin);
                    var cur = new Vector2(offsetX + time * xRatio, offsetY + value * yRatio);
                    if (Mathf.Abs(cur.y - previous.y) < minVertexYDelta) continue;
                    if (Mathf.Abs(cur.x - previous.x) > maxVertexXDelta)
                        points.Add(new Vector2(cur.x, previous.y));
                    previous = cur;
                    points.Add(cur);
                }
                var curN = new Vector2(offsetX + last.time * xRatio, offsetY + last.value * yRatio);
                if (Mathf.Abs(curN.x - previous.x) > maxVertexXDelta)
                    points.Add(new Vector2(curN.x, previous.y));
                points.Add(curN);
                vh.DrawLine(points, style.CurveLineSize, curveColor);

                // Draw handles
                for (var i = 0; i < curve.length; i++)
                {
                    var keyframe = curve.GetKeyframeByKey(i);
                    if (keyframe.time < rangeBegin) continue;
                    if (keyframe.time > rangeBegin + rangeDuration) break;
                    var handlePos = new Vector2(offsetX + (keyframe.time - rangeBegin) * xRatio, offsetY + keyframe.value * yRatio);
                    // Render bezier control points
#if (RENDER_BEZIER_CONTROL_POINTS)
                    if (i > 0)
                    {
                        var previousKey = curve.GetKeyframeByKey(i - 1);
                        var previousPos = new Vector2(offsetX + (keyframe.time - (keyframe.time - previousKey.time) / 3f) * xRatio, offsetY + keyframe.controlPointIn * yRatio);
                        vh.DrawLine(new[] { handlePos, previousPos }, 1f, Color.white);
                        vh.AddUIVertexQuad(UIVertexHelper.CreateVBO(Color.white, new[]
                        {
                            previousPos - new Vector2(-handleSize / 2f, -handleSize / 2f),
                            previousPos - new Vector2(-handleSize / 2f, handleSize / 2f),
                            previousPos - new Vector2(handleSize / 2f, handleSize / 2f),
                            previousPos - new Vector2(handleSize / 2f, -handleSize / 2f)
                        }));
                    }
                    if (i < curve.length - 1)
                    {
                        var next = curve.GetKeyframeByKey(i + 1);
                        var nextPos = new Vector2(offsetX + (keyframe.time + (next.time - keyframe.time) / 3f) * xRatio, offsetY + keyframe.controlPointOut * yRatio);
                        vh.DrawLine(new[] { handlePos, nextPos }, 1f, Color.white);
                        vh.AddUIVertexQuad(UIVertexHelper.CreateVBO(Color.white, new[]
                        {
                            nextPos - new Vector2(-handleSize / 2f, -handleSize / 2f),
                            nextPos - new Vector2(-handleSize / 2f, handleSize / 2f),
                            nextPos - new Vector2(handleSize / 2f, handleSize / 2f),
                            nextPos - new Vector2(handleSize / 2f, -handleSize / 2f)
                        }));
                    }
#endif
                    // Render keyframe
                    vh.AddUIVertexQuad(UIVertexHelper.CreateVBO(curveColor,
                        handlePos - new Vector2(-handleSize, -handleSize),
                        handlePos - new Vector2(-handleSize, handleSize),
                        handlePos - new Vector2(handleSize, handleSize),
                        handlePos - new Vector2(handleSize, -handleSize)
                    ));
                }
            }

            DrawBorder(vh, halfWidth, halfHeight);
        }

        private void DrawBorder(VertexHelper vh, float halfWidth, float halfHeight)
        {
            var halfBorder = style.BorderSize / 2f;
            var quarterBorder = halfBorder / 2f;
            vh.DrawLine(new[]
            {
                new Vector2(-halfWidth, -halfHeight + quarterBorder),
                new Vector2(halfWidth, -halfHeight + quarterBorder)
            }, style.BorderSize, style.BorderColor);
            vh.DrawLine(new[]
            {
                new Vector2(-halfWidth, halfHeight - quarterBorder),
                new Vector2(halfWidth, halfHeight - quarterBorder)
            }, style.BorderSize, style.BorderColor);
            vh.DrawLine(new[]
            {
                new Vector2(-halfWidth + halfBorder, halfHeight),
                new Vector2(-halfWidth + halfBorder, -halfHeight)
            }, style.BorderSize, style.BorderColor);
            vh.DrawLine(new[]
            {
                new Vector2(halfWidth - halfBorder, halfHeight),
                new Vector2(halfWidth - halfBorder, -halfHeight)
            }, style.BorderSize, style.BorderColor);
        }

        private Vector2 EstimateRange()
        {
            const float boundsEvalPrecision = 20f; // Check how many points to detect highest value
            var minY = float.MaxValue;
            var maxY = float.MinValue;
            var lead = _curves[0].Value;
            var maxX = lead.GetKeyframeByKey(lead.length - 1).time;
            var boundsTestStep = maxX / boundsEvalPrecision;
            foreach (var kvp in _curves)
            {
                var curve = kvp.Value;
                if (curve.length == 0) continue;
                for (var time = 0f; time < maxX; time += boundsTestStep)
                {
                    var value = curve.Evaluate(time);
                    minY = Mathf.Min(minY, value);
                    maxY = Mathf.Max(maxY, value);
                }
            }
            return new Vector2(minY, maxY);
        }
    }
}
