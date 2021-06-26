using UnityEngine;
using UnityEngine.UI;

namespace VamTimeline
{
    public class ScrubberMarkers : MaskableGraphic
    {
        public ScrubberStyle style;

        public float length;
        public float offset;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (length == 0 || style == null) return;
            var width = rectTransform.rect.width - style.Padding * 2;
            var pixelsPerSecond = width / length;
            float timespan;
            if (pixelsPerSecond < 20)
                timespan = 100f;
            else if (pixelsPerSecond < 2)
                timespan = 10f;
            else
                timespan = 1f;
            var height = rectTransform.rect.height;
            var yMin = -height / 2f;
            const float yMax = -2f;
            const float yMaxSmall = -8f;
            var timespan25 = timespan * 0.25f;
            var timespan50 = timespan * 0.50f;
            var timespan75 = timespan * 0.75f;

            var offsetX = -width / 2f;
            var ratio = width / length;

            for (var s = -offset; s <= length; s += timespan)
            {
                DrawLine(vh, yMin, yMax, offsetX, ratio, s, style.SecondsSize, style.SecondsColor);

                if (s >= length) break;
                DrawLine(vh, yMin, yMaxSmall, offsetX, ratio, s + timespan25, style.SecondFractionsSize, style.SecondFractionsColor);
                DrawLine(vh, yMin, yMaxSmall, offsetX, ratio, s + timespan50, style.SecondFractionsSize, style.SecondFractionsColor);
                DrawLine(vh, yMin, yMaxSmall, offsetX, ratio, s + timespan75, style.SecondFractionsSize, style.SecondFractionsColor);
            }

            vh.AddUIVertexQuad(UIVertexHelper.CreateVBO(style.SecondsColor,
                new Vector2(offsetX, yMin),
                new Vector2(-offsetX, yMin),
                new Vector2(-offsetX, yMin + style.SecondsSize),
                new Vector2(offsetX, yMin + style.SecondsSize)
            ));
        }

        private static void DrawLine(VertexHelper vh, float yMin, float yMax, float offsetX, float ratio, float time, float size, Color color)
        {
            var xMin = offsetX + time * ratio - size / 2f;
            var xMax = xMin + size;
            vh.AddUIVertexQuad(UIVertexHelper.CreateVBO(color,
                new Vector2(xMin, yMin),
                new Vector2(xMax, yMin),
                new Vector2(xMax, yMax),
                new Vector2(xMin, yMax)
            ));
        }
    }
}
