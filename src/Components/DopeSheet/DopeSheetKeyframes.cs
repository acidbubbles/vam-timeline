using System.Collections.Generic;
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
        private int _selectedFrame = -1;
        public DopeSheetStyle style;
        private bool _selected;
        private readonly HashSet<int> _frames = new HashSet<int>();
        private int _animationLength;

        public bool selected
        {
            get
            {
                return _selected;
            }

            set
            {
                _selected = value;
                SetVerticesDirty();
            }
        }

        public void SetKeyframes(float[] keyframes)
        {
            _frames.Clear();
            _animationLength = 0;
            if (keyframes.Length == 0) return;
            for (var i = 0; i < keyframes.Length; i++)
            {
                var v = keyframes[i].ToMilliseconds();
                _frames.Add(v);
                if (v > _animationLength) _animationLength = v;
            }
            SetVerticesDirty();
        }

        public void SetTime(int time)
        {
            if (_frames.Contains(time))
            {
                _selectedFrame = time;
                SetVerticesDirty();
            }
            else if (_selectedFrame != -1)
            {
                _selectedFrame = -1;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (style == null || _animationLength == 0) return;
            var width = rectTransform.rect.width;
            var height = rectTransform.rect.height;

            var padding = style.KeyframesRowPadding;
            var lineHeight = style.KeyframesRowLineSize;
            vh.AddUIVertexQuad(UIVertexHelper.CreateVBO(_selected ? style.KeyframesRowLineColorSelected : style.KeyframesRowLineColor, new[]
            {
                new Vector2(-width / 2f + padding, -lineHeight),
                new Vector2(width / 2f - padding, -lineHeight),
                new Vector2(width / 2f - padding, lineHeight),
                new Vector2(-width / 2f + padding, lineHeight)
            }));

            var ratio = (width - padding * 2f) / _animationLength;
            var size = style.KeyframeSize;
            var offsetX = -width / 2f + padding;
            foreach (var keyframe in _frames)
            {
                if (_selectedFrame == keyframe) continue;
                var center = new Vector2(offsetX + keyframe * ratio, 0);
                vh.AddUIVertexQuad(UIVertexHelper.CreateVBO(style.KeyframeColor, new[]
                {
                    center - new Vector2(0, -size),
                    center - new Vector2(size, 0),
                    center - new Vector2(0, size),
                    center - new Vector2(-size, 0)
                }));
            }

            if (_selectedFrame != -1)
            {
                var center = new Vector2(offsetX + _selectedFrame * ratio, 0);
                size = style.KeyframeSizeSelectedBack;
                vh.AddUIVertexQuad(UIVertexHelper.CreateVBO(style.KeyframeColorSelectedBack, new[]
                {
                    center - new Vector2(0, -size),
                    center - new Vector2(size, 0),
                    center - new Vector2(0, size),
                    center - new Vector2(-size, 0)
                }));
                size = style.KeyframeSizeSelectedFront;
                vh.AddUIVertexQuad(UIVertexHelper.CreateVBO(style.KeyframeColorSelectedFront, new[]
                {
                    center - new Vector2(0, -size),
                    center - new Vector2(size, 0),
                    center - new Vector2(0, size),
                    center - new Vector2(-size, 0)
                }));
            }
        }
    }
}
