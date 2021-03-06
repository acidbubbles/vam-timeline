using UnityEngine;
using UnityEngine.EventSystems;

namespace VamTimeline
{
    public class ZoomControl : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        public ZoomStyle style;
        public AtomAnimationEditContext animationEditContext;
        public ZoomControlGraphics graphics;

        private RectTransform _rect;
        private Canvas _canvas;
        private int _mode;
        private float _origClickedX;
        private ScrubberRange _origScrubberRange;
        private bool _lastPointerInside;
        private float _lastPointerDownTime;

        private void Start()
        {
            _rect = GetComponent<RectTransform>();
            _canvas = GetComponentInParent<Canvas>();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _mode = 0;
            graphics.mode = 0;
            graphics.SetVerticesDirty();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (Time.realtimeSinceStartup - _lastPointerDownTime < 0.3f && _lastPointerInside)
            {
                animationEditContext.ResetScrubberRange();
                return;
            }

            _lastPointerDownTime = Time.realtimeSinceStartup;

            eventData.useDragThreshold = false;

            _origScrubberRange = animationEditContext.scrubberRange;

            Vector2 localPosition;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, eventData.position, eventData.pressEventCamera, out localPosition))
                return;
            var actualSize = RectTransformUtility.PixelAdjustRect(GetComponent<RectTransform>(), _canvas);

            _origClickedX = localPosition.x - actualSize.xMin;
            var startX = animationEditContext.scrubberRange.rangeBegin / animationEditContext.current.animationLength * actualSize.width;
            var endX = (animationEditContext.scrubberRange.rangeBegin + animationEditContext.scrubberRange.rangeDuration) / animationEditContext.current.animationLength * actualSize.width;

            _lastPointerInside = _origClickedX > startX && _origClickedX < endX;

            if (_origClickedX < startX - style.ClickablePadding)
            {
                animationEditContext.MoveScrubberRangeBackward();
                return;
            }

            if (_origClickedX > endX + style.ClickablePadding)
            {
                animationEditContext.MoveScrubberRangeForward();
                return;
            }

            var insidePadding = style.ClickablePadding * (endX - startX) / actualSize.width;

            if (_origClickedX < startX + insidePadding)
                _mode = ZoomStateModes.ResizeBeginMode;
            else if (_origClickedX > endX - insidePadding)
                _mode = ZoomStateModes.ResizeEndMode;
            else
                _mode = ZoomStateModes.MoveMode;

            graphics.mode = _mode;
            graphics.SetVerticesDirty();
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
            Dispatch(eventData);
        }

        private void Dispatch(PointerEventData eventData)
        {
            if (_mode == 0) return;
            Vector2 localPosition;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, eventData.position, eventData.pressEventCamera, out localPosition))
                return;
            var actualSize = RectTransformUtility.PixelAdjustRect(_rect, _canvas);
            var clickedX = localPosition.x - actualSize.xMin;
            var clickedTimeDelta = (clickedX - _origClickedX) / actualSize.width * animationEditContext.current.animationLength;
            switch (_mode)
            {
                case ZoomStateModes.MoveMode:
                {
                    animationEditContext.MoveScrubberRange(_origScrubberRange.rangeBegin + clickedTimeDelta);
                    break;
                }
                case ZoomStateModes.ResizeBeginMode:
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
                case ZoomStateModes.ResizeEndMode:
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
            graphics.mode = 0;
            graphics.SetVerticesDirty();

            if (eventData.scrollDelta.y > 0)
                animationEditContext.ZoomScrubberRangeIn();
            else if (eventData.scrollDelta.y < 0)
                animationEditContext.ZoomScrubberRangeOut();
        }
    }
}
