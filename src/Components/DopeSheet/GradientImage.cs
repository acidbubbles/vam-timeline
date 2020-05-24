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
        private bool _dirty;
        private Color _top;
        private Color _bottom;

        public Color top { get { return _top; } set { _top = value; _dirty = true; } }
        public Color bottom { get { return _bottom; } set { _bottom = value; _dirty = true; } }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            if (!_dirty) return;
            _dirty = false;
            vh.Clear();

            vh.AddUIVertexQuad(new[]
            {
                CreateVertex(new Vector2(rectTransform.rect.xMin, rectTransform.rect.yMin), _bottom),
                CreateVertex(new Vector2(rectTransform.rect.xMin, rectTransform.rect.yMax), _top),
                CreateVertex(new Vector2(rectTransform.rect.xMax, rectTransform.rect.yMax), _top),
                CreateVertex(new Vector2(rectTransform.rect.xMax, rectTransform.rect.yMin), _bottom),
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
