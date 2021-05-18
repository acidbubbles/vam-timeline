using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VamTimeline
{
    public class Zoom : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private readonly ZoomStyle _style = new ZoomStyle();
        private readonly ZoomGraphics _graphics;
        private readonly Text _zoomText;

        private Canvas _canvas;
        private AtomAnimationEditContext _animationEditContext;

        public Zoom()
        {
            var image = gameObject.AddComponent<Image>();
            image.raycastTarget = false;

            var mask = gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            CreateBackground();
            _graphics = CreateGraphics();
            _zoomText = CreateZoomText();
        }

        public void Bind(AtomAnimationEditContext animationEditContext)
        {
            _animationEditContext = animationEditContext;
            _animationEditContext.onScrubberRangeChanged.AddListener(OnScrubberRangeChanged);
            OnScrubberRangeChanged(new AtomAnimationEditContext.ScrubberRangeChangedEventArgs {scrubberRange = _animationEditContext.scrubberRange});
        }

        private void Start()
        {
            _canvas = GetComponentInParent<Canvas>();
        }

        private void OnDestroy()
        {
            if (_animationEditContext == null) return;
            _animationEditContext.onScrubberRangeChanged.RemoveListener(OnScrubberRangeChanged);
        }

        private GameObject CreateBackground()
        {
            var go = new GameObject();
            go.transform.SetParent(transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.StretchParent();

            var image = go.AddComponent<Image>();
            image.color = _style.BackgroundColor;
            image.raycastTarget = false;

            return go;
        }

        private ZoomGraphics CreateGraphics()
        {
            var go = new GameObject();
            go.transform.SetParent(transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.StretchParent();
            rect.offsetMin = new Vector2(_style.Padding, _style.VerticalPadding);
            rect.offsetMax = new Vector2(-_style.Padding, -_style.VerticalPadding);

            var graphics = go.AddComponent<ZoomGraphics>();
            graphics.raycastTarget = true;
            graphics.style = _style;
            return graphics;
        }

        private Text CreateZoomText()
        {
            var go = new GameObject();
            go.transform.SetParent(transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.StretchParent();

            var text = go.AddComponent<Text>();
            text.text = "100%";
            text.font = _style.Font;
            text.fontSize = 20;
            text.color = _style.FontColor;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;

            return text;
        }

        private void OnScrubberRangeChanged(AtomAnimationEditContext.ScrubberRangeChangedEventArgs args)
        {
            if (_animationEditContext == null) return;
            _graphics.animationLength = _animationEditContext.current.animationLength;
            _graphics.rangeDuration = args.scrubberRange.rangeDuration;
            _graphics.rangeBegin = args.scrubberRange.rangeBegin;
            _graphics.SetVerticesDirty();
            _zoomText.text = (args.scrubberRange.rangeDuration / _animationEditContext.current.animationLength * 100f).ToString("0.0") + "%";
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            eventData.useDragThreshold = false;
            UpdateZoomFromView(eventData, true);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            UpdateZoomFromView(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            UpdateZoomFromView(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            UpdateZoomFromView(eventData, true);
        }

        private void UpdateZoomFromView(PointerEventData eventData, bool final = false)
        {
            if (_animationEditContext == null) return;
            Vector2 localPosition;
            var rect = GetComponent<RectTransform>();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out localPosition))
                return;
            var actualSize = RectTransformUtility.PixelAdjustRect(rect, _canvas);
            // TODO: Implement this
        }
    }
}
