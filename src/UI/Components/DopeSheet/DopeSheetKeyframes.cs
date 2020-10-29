using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VamTimeline
{
    public class DopeSheetKeyframes : MaskableGraphic
    {
        private int _currentFrame = -1;
        public DopeSheetStyle style;
        private bool _selected;
        private readonly HashSet<int> _frames = new HashSet<int>();
        private int _animationLength;
        private bool _loop;

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

        public void SetKeyframes(float[] keyframes, bool loop)
        {
            _frames.Clear();
            _animationLength = 0;
            _loop = loop;
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
                _currentFrame = time;
                SetVerticesDirty();
            }
            else if (_currentFrame != -1)
            {
                _currentFrame = -1;
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
            var pixelsPerSecond = width / _frames.Count;
            var tooManyKeyframes = pixelsPerSecond < 2f;
            var lineColor = _selected ? style.KeyframesRowLineColorSelected : (tooManyKeyframes ? Color.red : style.KeyframesRowLineColor);
            vh.AddUIVertexQuad(UIVertexHelper.CreateVBO(lineColor, new[]
            {
                new Vector2(-width / 2f + padding, -lineHeight),
                new Vector2(width / 2f - padding, -lineHeight),
                new Vector2(width / 2f - padding, lineHeight),
                new Vector2(-width / 2f + padding, lineHeight)
            }));

            if(tooManyKeyframes) return;

            var ratio = (width - padding * 2f) / _animationLength;
            var size = style.KeyframeSize;
            var offsetX = -width / 2f + padding;
            foreach (var keyframe in _frames)
            {
                if (_currentFrame == keyframe) continue;
                if (_loop && keyframe == _animationLength) continue;
                var center = new Vector2(offsetX + keyframe * ratio, 0);
                vh.AddUIVertexQuad(UIVertexHelper.CreateVBO(style.KeyframeColor, new[]
                {
                    center - new Vector2(0, -size),
                    center - new Vector2(size, 0),
                    center - new Vector2(0, size),
                    center - new Vector2(-size, 0)
                }));
            }

            if (_loop)
            {
                var center = new Vector2(offsetX + _animationLength * ratio, 0);
                vh.AddUIVertexQuad(UIVertexHelper.CreateVBO(style.KeyframeColor, new[]
                {
                    center - new Vector2(-2, -size),
                    center - new Vector2(-2, size),
                    center - new Vector2(2, size),
                    center - new Vector2(2, -size)
                }));
            }

            if (_currentFrame != -1)
            {
                var center = new Vector2(offsetX + _currentFrame * ratio, 0);
                size = style.KeyframeSizeSelectedBack;
                vh.AddUIVertexQuad(UIVertexHelper.CreateVBO(selected ? style.KeyframeColorSelectedBack : style.KeyframeColorCurrentBack, new[]
                {
                    center - new Vector2(0, -size),
                    center - new Vector2(size, 0),
                    center - new Vector2(0, size),
                    center - new Vector2(-size, 0)
                }));
                size = style.KeyframeSizeSelectedFront;
                vh.AddUIVertexQuad(UIVertexHelper.CreateVBO(selected ? style.KeyframeColorSelectedFront : style.KeyframeColorCurrentFront, new[]
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
