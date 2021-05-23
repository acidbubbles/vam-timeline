using UnityEngine;
using UnityEngine.UI;

namespace VamTimeline
{
    public class ZoomControlGraphics : MaskableGraphic
    {
        public ZoomStyle style;
        public AtomAnimationEditContext animationEditContext;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            if(animationEditContext.current.animationLength == 0) return;

            var rect = rectTransform.rect;
            var width = rect.width;
            var height = rect.height;

            var x1 = (animationEditContext.scrubberRange.rangeBegin / animationEditContext.current.animationLength) * width;
            var x2 = (animationEditContext.scrubberRange.rangeDuration / animationEditContext.current.animationLength) * width;

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
