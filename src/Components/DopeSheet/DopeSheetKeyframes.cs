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
    public class DopeSheetKeyframes : MaskableGraphic
    {
        public IAtomAnimationTarget target;
        public DopeSheetStyle style;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (target == null || style == null) return;
            var width = rectTransform.rect.width;
            var height = rectTransform.rect.height;

            // TODO: Avoid enumerating, we know the array size upfront!
            var keyframes = target.GetAllKeyframesTime().ToList();
            var size = style.KeyframeSize;
            var padding = style.KeyframesRowPadding;
            var ratio = (width - padding * 2f) / keyframes[keyframes.Count - 1];
            var lineHeight = style.KeyframesRowLineSize;
            vh.AddUIVertexQuad(CreateVBO(style.KeyframesRowLineColor, new[]
            {
                new Vector2(-width / 2f + padding, -lineHeight),
                new Vector2(width / 2f - padding, -lineHeight),
                new Vector2(width / 2f - padding, lineHeight),
                new Vector2(-width / 2f + padding, lineHeight)
            }));
            var offsetX = -width / 2f + padding;
            foreach (var keyframe in keyframes)
            {
                // TODO: 0f here should be the y offset based on a predetermined row height
                var center = new Vector2(offsetX + keyframe * ratio, 0);
                vh.AddUIVertexQuad(CreateVBO(style.KeyframeColor, new[]
                {
                    center - new Vector2(0, -size),
                    center - new Vector2(size, 0),
                    center - new Vector2(0, size),
                    center - new Vector2(-size, 0)
                }));
            }
        }

        private static UIVertex[] CreateVBO(Color color, params Vector2[] vertices)
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
    }
}
