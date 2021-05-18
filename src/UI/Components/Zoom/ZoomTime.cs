using UnityEngine;
using UnityEngine.UI;

namespace VamTimeline
{
    public class ZoomTime : MaskableGraphic
    {
        public ZoomStyle style;
        public float animationLength { get; set; }
        public float time { get; set; }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            if(animationLength == 0) return;

            var rect = rectTransform.rect;
            var width = rect.width;
            var height = rect.height;

            var x1 = (time / animationLength) * width;
            var timeWidth = style.ScrubberWidth / 2f;

            vh.AddUIVertexQuad(UIVertexHelper.CreateVBO(style.ScrubberColor,
                new Vector2(rect.xMin + x1 - timeWidth, rect.yMin),
                new Vector2(rect.xMin + x1 + timeWidth, rect.yMin),
                new Vector2(rect.xMin + x1 + timeWidth, rect.yMax),
                new Vector2(rect.xMin + x1 - timeWidth, rect.yMax)
            ));
        }
    }
}
