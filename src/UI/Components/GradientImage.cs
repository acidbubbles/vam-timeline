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
    public class GradientImage : MaskableGraphic
    {
        private Color _top;
        private Color _bottom;

        public Color top
        {
            get
            {
                return _top;
            }
            set
            {
                _top = value;
                SetVerticesDirty();
            }
        }
        public Color bottom
        {
            get
            {
                return _bottom;
            }
            set
            {
                _bottom = value;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            vh.AddUIVertexQuad(new[]
            {
                CreateVertex(new Vector2(rectTransform.rect.xMin, rectTransform.rect.yMin), bottom),
                CreateVertex(new Vector2(rectTransform.rect.xMin, rectTransform.rect.yMax), top),
                CreateVertex(new Vector2(rectTransform.rect.xMax, rectTransform.rect.yMax), top),
                CreateVertex(new Vector2(rectTransform.rect.xMax, rectTransform.rect.yMin), bottom),
            });
        }

        private static UIVertex CreateVertex(Vector2 pos, Color color)
        {
            var vert = UIVertex.simpleVert;
            vert.color = color;
            vert.position = pos;
            return vert;
        }
    }
}
