using UnityEngine;
using UnityEngine.UI;

namespace VamTimeline
{
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

            var rect = rectTransform.rect;
            vh.AddUIVertexQuad(new[]
            {
                CreateVertex(new Vector2(rect.xMin, rect.yMin), bottom),
                CreateVertex(new Vector2(rect.xMin, rect.yMax), top),
                CreateVertex(new Vector2(rect.xMax, rect.yMax), top),
                CreateVertex(new Vector2(rect.xMax, rect.yMin), bottom)
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
