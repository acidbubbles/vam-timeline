using UnityEngine;
using UnityEngine.UI;

namespace VamTimeline
{
    public class ZoomGraphics : MaskableGraphic
    {
        public ZoomStyle style;
        public float animationLength { get; set; }
        public float rangeDuration { get; set; }
        public float rangeBegin { get; set; }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            if(animationLength == 0 || rangeDuration == 0) return;

            var rect = rectTransform.rect;
            var width = rect.width;
            var height = rect.height;

            var x1 = (rangeBegin / animationLength) * width;
            var x2 = (rangeDuration / animationLength) * width;

            vh.AddUIVertexQuad(UIVertexHelper.CreateVBO(style.FullSectionColor,
                new Vector2(rect.xMin, rect.yMin),
                new Vector2(rect.xMax, rect.yMin),
                new Vector2(rect.xMax, rect.yMax),
                new Vector2(rect.xMin, rect.yMax)
            ));

            vh.AddUIVertexQuad(UIVertexHelper.CreateVBO(style.ZoomedSectionColor,
                new Vector2(rect.xMin + x1, rect.yMin),
                new Vector2(rect.xMin + x1 + x2, rect.yMin),
                new Vector2(rect.xMin + x1 + x2, rect.yMax),
                new Vector2(rect.xMin + x1, rect.yMax)
            ));
        }
    }
}
