using UnityEngine;
using UnityEngine.UI;

namespace VamTimeline
{
    public class ZoomControlGraphics : MaskableGraphic
    {
        public ZoomStyle style;
        public AtomAnimationEditContext animationEditContext;
        public int mode;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            if(animationEditContext.current.animationLength == 0) return;

            var rect = rectTransform.rect;
            var padding = style.Padding;
            rect.xMin += padding;
            rect.xMax -= padding;
            var width = rect.width;

            var x1 = (animationEditContext.scrubberRange.rangeBegin / animationEditContext.current.animationLength) * width;
            var x2 = (animationEditContext.scrubberRange.rangeDuration / animationEditContext.current.animationLength) * width;

            vh.AddUIVertexQuad(UIVertexHelper.CreateVBO(style.FullSectionColor,
                new Vector2(rect.xMin, rect.yMin),
                new Vector2(rect.xMax, rect.yMin),
                new Vector2(rect.xMax, rect.yMax),
                new Vector2(rect.xMin, rect.yMax)
            ));

            vh.AddUIVertexQuad(UIVertexHelper.CreateVBO(mode == ZoomStateModes.MoveMode ? style.ZoomedSectionHighlightColor : style.ZoomedSectionColor,
                new Vector2(rect.xMin + x1, rect.yMin),
                new Vector2(rect.xMin + x1 + x2, rect.yMin),
                new Vector2(rect.xMin + x1 + x2, rect.yMax),
                new Vector2(rect.xMin + x1, rect.yMax)
            ));

            if (mode == ZoomStateModes.ResizeBeginMode)
            {
                vh.AddUIVertexQuad(UIVertexHelper.CreateVBO(style.ZoomedSectionHighlightColor,
                    new Vector2(rect.xMin + x1 - style.DragSideWidth, rect.yMin),
                    new Vector2(rect.xMin + x1 + style.DragSideWidth, rect.yMin),
                    new Vector2(rect.xMin + x1 + style.DragSideWidth, rect.yMax),
                    new Vector2(rect.xMin + x1 - style.DragSideWidth, rect.yMax)
                ));
            }
            else if (mode == ZoomStateModes.ResizeEndMode)
            {
                vh.AddUIVertexQuad(UIVertexHelper.CreateVBO(style.ZoomedSectionHighlightColor,
                    new Vector2(rect.xMin + x1 + x2 - style.DragSideWidth, rect.yMin),
                    new Vector2(rect.xMin + x1 + x2 + style.DragSideWidth, rect.yMin),
                    new Vector2(rect.xMin + x1 + x2 + style.DragSideWidth, rect.yMax),
                    new Vector2(rect.xMin + x1 + x2 - style.DragSideWidth, rect.yMax)
                ));
            }
        }
    }
}
