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
        private int _rangeBegin;
        private int _rangeDuration;

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

        public void SetRange(float rangeBegin, float rangeDuration)
        {
            _rangeBegin = rangeBegin.ToMilliseconds();
            _rangeDuration = rangeDuration.ToMilliseconds();
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (style == null || _animationLength == 0 || _rangeDuration == 0) return;
            var width = rectTransform.rect.width;

            var padding = style.KeyframesRowPadding;
            var lineHeight = style.KeyframesRowLineSize;
            var lineColor = _selected ? style.KeyframesRowLineColorSelected : style.KeyframesRowLineColor;
            vh.AddUIVertexQuad(UIVertexHelper.CreateVBO(lineColor,
                new Vector2(-width / 2f + padding, -lineHeight),
                new Vector2(width / 2f - padding, -lineHeight),
                new Vector2(width / 2f - padding, lineHeight),
                new Vector2(-width / 2f + padding, lineHeight)
            ));

            var ratio = (width - padding * 2f) / _rangeDuration;
            var size = style.KeyframeSize;
            var minX = -width / 2f - style.KeyframeSizeSelectedBack;
            var maxX = width / 2f + style.KeyframeSizeSelectedBack;
            var offsetX = -width / 2f + padding;
            var lastCenter = float.NegativeInfinity;
            foreach (var keyframe in _frames)
            {
                if (_currentFrame == keyframe) continue;
                if (_loop && keyframe == _animationLength) continue;
                var center = new Vector2(offsetX + (keyframe - _rangeBegin) * ratio, 0);
                if (center.x < minX) continue;
                if (center.x > maxX) break;
                if (center.x - lastCenter < 2.5f) continue;
                lastCenter = center.x;
                vh.AddUIVertexQuad(UIVertexHelper.CreateVBO(style.KeyframeColor,
                    center - new Vector2(0, -size),
                    center - new Vector2(size, 0),
                    center - new Vector2(0, size),
                    center - new Vector2(-size, 0)
                ));
            }

            if (_loop)
            {
                var center = new Vector2(offsetX + (_animationLength - _rangeBegin) * ratio, 0);
                if (center.x >= minX && center.x <= maxX)
                {
                    vh.AddUIVertexQuad(UIVertexHelper.CreateVBO(style.KeyframeColor,
                        center - new Vector2(-2, -size),
                        center - new Vector2(-2, size),
                        center - new Vector2(2, size),
                        center - new Vector2(2, -size)
                    ));
                }
            }

            if (_currentFrame != -1)
            {
                var center = new Vector2(offsetX + (_currentFrame - _rangeBegin) * ratio, 0);
                if (center.x >= minX && center.x <= maxX)
                {
                    size = style.KeyframeSizeSelectedBack;
                    vh.AddUIVertexQuad(UIVertexHelper.CreateVBO(selected ? style.KeyframeColorSelectedBack : style.KeyframeColorCurrentBack,
                        center - new Vector2(0, -size),
                        center - new Vector2(size, 0),
                        center - new Vector2(0, size),
                        center - new Vector2(-size, 0)
                    ));
                    size = style.KeyframeSizeSelectedFront;
                    vh.AddUIVertexQuad(UIVertexHelper.CreateVBO(selected ? style.KeyframeColorSelectedFront : style.KeyframeColorCurrentFront,
                        center - new Vector2(0, -size),
                        center - new Vector2(size, 0),
                        center - new Vector2(0, size),
                        center - new Vector2(-size, 0)
                    ));
                }
            }
        }
    }
}
