
using Leap.Unity.Swizzle;
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
    public class TargetRow
    {
        private readonly IAnimationTarget _target;
        private readonly float _height;
        private readonly float _yOffset;

        public TargetRow(IAnimationTarget target, float height, float yOffset)
        {
            _target = target;
            _height = height;
            _yOffset = yOffset;
        }

        public void PopulateMesh(VertexHelper vh, Matrix4x4 viewMatrix, Rect viewBounds)
        {
            // TODO: Avoid enumerating, we know the array size upfront!
            var keyframes = _target.GetAllKeyframesTime().ToList();
            var ratio = viewBounds.max.x / keyframes[keyframes.Count - 1];
            SuperController.LogMessage(_yOffset.ToString());
            foreach (var keyframe in keyframes)
            {
                var size = _height / 2f;
                // TODO: 0f here should be the y offset based on a predetermined row height
                var center = new Vector2(keyframe * ratio, viewBounds.max.y - _yOffset - _height / 2f);
                vh.AddUIVertexQuad(CreateVBO(Color.white, viewMatrix, new[]
                {
                    center - new Vector2(-size, -size),
                    center - new Vector2(size, -size),
                    center - new Vector2(size, size),
                    center - new Vector2(-size, size)
                }));
            }
        }

        private static UIVertex[] CreateVBO(Color color, Matrix4x4 viewMatrix, params Vector2[] vertices)
        {
            var vbo = new UIVertex[vertices.Length];
            for (var i = 0; i < vertices.Length; i++)
            {
                var vert = UIVertex.simpleVert;
                vert.color = color;
                vert.position = viewMatrix.MultiplyPoint3x4(vertices[i]).xy();
                vbo[i] = vert;
            }

            return vbo;
        }
    }
}
