using UnityEngine;
using UnityEngine.EventSystems;

namespace VamTimeline
{
    public class ScrubberInput : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, IScrollHandler
    {
        private const float _clickablePadding = 32f;
        private const int _resizeBeginMode = 1;
        private const int _resizeEndMode = 2;
        private const int _moveMode = 3;

        public AtomAnimationEditContext animationEditContext;

        private Canvas _canvas;
        private int _mode;
        private float _origClickedX;
        private ScrubberRange _origScrubberRange;

        private void Start()
        {
            _canvas = GetComponentInParent<Canvas>();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.clickCount >= 2)
            {
                animationEditContext.ResetScrubberRange();
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            eventData.useDragThreshold = false;

            _origScrubberRange = animationEditContext.scrubberRange;

            Vector2 localPosition;
            var rect = GetComponent<RectTransform>();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out localPosition))
                return;
            var actualSize = RectTransformUtility.PixelAdjustRect(GetComponent<RectTransform>(), _canvas);

            _origClickedX = localPosition.x - actualSize.xMin;
            var startX = animationEditContext.scrubberRange.rangeBegin / animationEditContext.current.animationLength * actualSize.width;
            var endX = (animationEditContext.scrubberRange.rangeBegin + animationEditContext.scrubberRange.rangeDuration) / animationEditContext.current.animationLength * actualSize.width;

            if (_origClickedX < startX - _clickablePadding || _origClickedX > endX + _clickablePadding) return;

            var insidePadding = _clickablePadding * (endX - startX) / actualSize.width;

            if (_origClickedX < startX + insidePadding)
                _mode = _resizeBeginMode;
            else if (_origClickedX > endX - insidePadding)
                _mode = _resizeEndMode;
            else
                _mode = _moveMode;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            Dispatch(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            Dispatch(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            Dispatch(eventData, true);
            _mode = 0;
        }

        private void Dispatch(PointerEventData eventData, bool final = false)
        {
            if (_mode == 0) return;
            Vector2 localPosition;
            var rect = GetComponent<RectTransform>();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out localPosition))
                return;
            var actualSize = RectTransformUtility.PixelAdjustRect(rect, _canvas);
            var clickedX = localPosition.x - actualSize.xMin;
            var clickedTimeDelta = (clickedX - _origClickedX) / actualSize.width * animationEditContext.current.animationLength;
            switch (_mode)
            {
                case _moveMode:
                {
                    var rangeBegin = Mathf.Clamp(
                        _origScrubberRange.rangeBegin + clickedTimeDelta,
                        0,
                        animationEditContext.current.animationLength - _origScrubberRange.rangeDuration);
                    animationEditContext.scrubberRange = new ScrubberRange
                    {
                        rangeBegin = rangeBegin,
                        rangeDuration = _origScrubberRange.rangeDuration
                    };
                    break;
                }
                case _resizeBeginMode:
                {
                    var rangeBegin = Mathf.Clamp(
                        _origScrubberRange.rangeBegin + clickedTimeDelta,
                        0,
                        _origScrubberRange.rangeBegin + _origScrubberRange.rangeDuration);
                    animationEditContext.scrubberRange = new ScrubberRange
                    {
                        rangeBegin = rangeBegin,
                        rangeDuration = (_origScrubberRange.rangeBegin - rangeBegin) + _origScrubberRange.rangeDuration
                    };
                    break;
                }
                case _resizeEndMode:
                {
                    var rangeDuration = Mathf.Clamp(
                        _origScrubberRange.rangeDuration + clickedTimeDelta,
                        0,
                        animationEditContext.current.animationLength - _origScrubberRange.rangeBegin);
                    animationEditContext.scrubberRange = new ScrubberRange
                    {
                        rangeBegin = _origScrubberRange.rangeBegin,
                        rangeDuration = rangeDuration
                    };
                    break;
                }
            }
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (!eventData.IsScrolling()) return;
            _mode = 0;
            var rangeDuration = animationEditContext.scrubberRange.rangeDuration;
            if (eventData.scrollDelta.y > 0)
                rangeDuration *= 0.8f;
            else if (eventData.scrollDelta.y < 0)
                rangeDuration *= 1.2f;
            var rangeBegin = Mathf.Max(0f, animationEditContext.scrubberRange.rangeBegin - (rangeDuration - animationEditContext.scrubberRange.rangeDuration) / 2f);
            rangeDuration = Mathf.Min(animationEditContext.current.animationLength - rangeBegin, rangeDuration);
            animationEditContext.scrubberRange = new ScrubberRange
            {
                rangeBegin = rangeBegin,
                rangeDuration = rangeDuration
            };
        }
    }
}
