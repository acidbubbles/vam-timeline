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
        private List<KeyValuePair<Color, AnimationCurve>> _curves;

        public List<KeyValuePair<Color, AnimationCurve>> curves
        {
            get
            {
                return _curves;
            }

            set
            {
                _curves = value;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (style == null || _curves == null) return;
            var width = rectTransform.rect.width;
            var height = rectTransform.rect.height;
            var offsetX = -width / 2f;
            var offsetY = -height / 2f;

            foreach (var curveInfo in _curves)
            {
                var curve = curveInfo.Value;
                var color = curveInfo.Key;
                var length = curve[curve.length - 1].time;
                var xRatio = width / length;
                var yRatio = 1f;
                for (var i = 1; i < curve.length; i++)
                {
                    var prev1 = curve[i - 1];
                    var cur1 = curve[i];
                    var prev = new Vector2(offsetX + prev1.time * xRatio, offsetY + prev1.value * height * yRatio);
                    var cur = new Vector2(offsetX + cur1.time * xRatio, offsetY + cur1.value * height * yRatio);

                    var angle = Mathf.Atan2(cur.y - prev.y, cur.x - prev.x) * 180f / Mathf.PI;

                    var v1 = prev + new Vector2(0, -style.LineSize / 2);
                    var v2 = prev + new Vector2(0, +style.LineSize / 2);
                    var v3 = cur + new Vector2(0, +style.LineSize / 2);
                    var v4 = cur + new Vector2(0, -style.LineSize / 2);

                    v1 = RotatePointAroundPivot(v1, prev, angle);
                    v2 = RotatePointAroundPivot(v2, prev, angle);
                    v3 = RotatePointAroundPivot(v3, cur, angle);
                    v4 = RotatePointAroundPivot(v4, cur, angle);

                    vh.AddUIVertexQuad(UIVertexHelper.CreateVBO(color, new[] { v1, v2, v3, v4 }));
                }
            }
        }

        // TODO: Move to utility class
        public static Vector3 RotatePointAroundPivot(Vector3 point, Vector3 pivot, Vector3 angles)
            => Quaternion.Euler(angles) * (point - pivot) + pivot;
        public static Vector2 RotatePointAroundPivot(Vector2 point, Vector2 pivot, float angle)
            => RotatePointAroundPivot(point, pivot, angle * Vector3.forward);
    }
}
