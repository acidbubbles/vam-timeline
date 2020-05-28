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
    public static class UIVertexHelper
    {
        public static void DrawLine(this VertexHelper vh, IList<Vector2> points, float thickness, Color color)
        {
            for (var i = 1; i < points.Count; i++)
            {
                var prev = points[i - 1];
                var curr = points[i];
                var angle = Mathf.Atan2(curr.y - prev.y, curr.x - prev.x) * Mathf.Rad2Deg;

                var v1 = prev + new Vector2(0, -thickness / 2);
                var v2 = prev + new Vector2(0, +thickness / 2);
                var v3 = curr + new Vector2(0, +thickness / 2);
                var v4 = curr + new Vector2(0, -thickness / 2);

                v1 = RotatePointAroundPivot(v1, prev, angle);
                v2 = RotatePointAroundPivot(v2, prev, angle);
                v3 = RotatePointAroundPivot(v3, curr, angle);
                v4 = RotatePointAroundPivot(v4, curr, angle);

                vh.AddUIVertexQuad(CreateVBO(color, new[] { v1, v2, v3, v4 }));
                // vh.AddUIVertexQuad(CreateVBO(new Color(Random.Range(0, 1), Random.Range(0, 1f), Random.Range(0, 1)), new[] { v1, v2, v3, v4 }));
            }
        }

        public static UIVertex[] CreateVBO(Color color, params Vector2[] vertices)
        {
            var vbo = new UIVertex[vertices.Length];
            for (var i = 0; i < vertices.Length; i++)
            {
                var vert = UIVertex.simpleVert;
                vert.color = color;
                vert.position = vertices[i];
                vbo[i] = vert;
            }

            return vbo;
        }

        private static Vector3 RotatePointAroundPivot(Vector3 point, Vector3 pivot, Vector3 angles)
            => Quaternion.Euler(angles) * (point - pivot) + pivot;
        private static Vector2 RotatePointAroundPivot(Vector2 point, Vector2 pivot, float angle)
            => RotatePointAroundPivot(point, pivot, angle * Vector3.forward);
    }
}
