using UnityEngine;
using UnityEngine.UI;

namespace VamTimeline
{
    public class ScrubberMarkers : MaskableGraphic
    {
        private float _length;
        public ScrubberStyle style;

        public float length
        {
            get { return _length; }
            set { _length = value; SetVerticesDirty(); }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_length == 0 || style == null) return;
            var width = rectTransform.rect.width;
            var height = rectTransform.rect.height;
            var yMin = -height / 2f;
            var yMax = -2f;
            var yMaxSmall = -8f;

            var offsetX = -width / 2f;
            var ratio = width / _length;

            for (var s = 0f; s <= _length; s += 1f)
            {
                DrawLine(vh, yMin, yMax, offsetX, ratio, s, style.SecondsSize, style.SecondsColor);

                if (s == _length) break;
                DrawLine(vh, yMin, yMaxSmall, offsetX, ratio, s + 0.25f, style.SecondFractionsSize, style.SecondFractionsColor);
                DrawLine(vh, yMin, yMaxSmall, offsetX, ratio, s + 0.50f, style.SecondFractionsSize, style.SecondFractionsColor);
                DrawLine(vh, yMin, yMaxSmall, offsetX, ratio, s + 0.75f, style.SecondFractionsSize, style.SecondFractionsColor);
            }
        }

        private void DrawLine(VertexHelper vh, float yMin, float yMax, float offsetX, float ratio, float time, float size, Color color)
        {
            var xMin = offsetX + time * ratio - size / 2f;
            var xMax = xMin + size;
            vh.AddUIVertexQuad(UIVertexHelper.CreateVBO(color, new[]
            {
                    new Vector2(xMin, yMin),
                    new Vector2(xMax, yMin),
                    new Vector2(xMax, yMax),
                    new Vector2(xMin, yMax)
                }));
        }
    }
}
