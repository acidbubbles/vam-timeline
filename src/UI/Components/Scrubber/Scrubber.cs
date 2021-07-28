using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VamTimeline
{
    public class Scrubber : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private readonly ScrubberStyle _style = new ScrubberStyle();
        private readonly RectTransform _scrubberRect;
        private readonly Text _timeText;

        private int _lastScrubberHash;
        private int _lastTextHash;
        private ScrubberMarkers _markers;

        public AtomAnimationEditContext animationEditContext;
        private Canvas _canvas;

        public Scrubber()
        {
            var go = gameObject;

            var image = go.AddComponent<Image>();
            image.raycastTarget = false;

            var mask = go.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            CreateBackground(go, _style.BackgroundColor);
            CreateMarkers();
            _scrubberRect = CreateLine(go, _style.ScrubberColor).GetComponent<RectTransform>();
            _timeText = CreateTime();
        }

        private void Start()
        {
            _canvas = GetComponentInParent<Canvas>();
        }

        private static GameObject CreateBackground(GameObject parent, Color color)
        {
            var go = new GameObject();
            go.transform.SetParent(parent.transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.StretchParent();

            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = true;

            return go;
        }

        private void CreateMarkers()
        {
            var go = new GameObject();
            go.transform.SetParent(transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.StretchParent();

            _markers = go.AddComponent<ScrubberMarkers>();
            _markers.raycastTarget = false;
            _markers.style = _style;
        }

        private GameObject CreateLine(GameObject parent, Color color)
        {
            var go = new GameObject("Scrubber");
            go.transform.SetParent(parent.transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.StretchParent();
            rect.offsetMin = new Vector2(_style.Padding, 0f);
            rect.offsetMax = new Vector2(-_style.Padding, 0f);

            var line = new GameObject("Scrubber Line");
            line.transform.SetParent(go.transform, false);

            var lineRect = line.AddComponent<RectTransform>();
            lineRect.StretchCenter();
            lineRect.sizeDelta = new Vector2(_style.ScrubberSize, 0);

            var image = line.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            return line;
        }

        private Text CreateTime()
        {
            var go = new GameObject();
            go.transform.SetParent(transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.StretchParent();
            rect.anchoredPosition = new Vector2(0, -5f);

            var text = go.AddComponent<Text>();
            text.text = "0.000s / 0.000s";
            text.font = _style.Font;
            text.fontSize = 24;
            text.color = _style.FontColor;
            text.alignment = TextAnchor.UpperCenter;
            text.raycastTarget = false;

            return text;
        }

        public void Update()
        {
            if (UIPerformance.ShouldSkip(UIPerformance.HighFrequency)) return;

            // var currentUpdate = new Vector2(animationEditContext.clipTime, animationEditContext.scrubberRange.rangeDuration);

            if (UIPerformance.ShouldRun(UIPerformance.HighFrequency))
            {
                int currentScrubberHash;
                unchecked
                {
                    currentScrubberHash = animationEditContext.scrubberRange.rangeBegin.GetHashCode() * 23 + animationEditContext.scrubberRange.rangeDuration.GetHashCode();
                }

                if (currentScrubberHash != _lastScrubberHash)
                {
                    _markers.offset = animationEditContext.scrubberRange.rangeBegin;
                    _markers.length = animationEditContext.scrubberRange.rangeDuration;
                    _markers.SetVerticesDirty();
                    _lastScrubberHash = currentScrubberHash;
                }

                var ratio = (animationEditContext.clipTime - animationEditContext.scrubberRange.rangeBegin) / animationEditContext.scrubberRange.rangeDuration;
                _scrubberRect.anchorMin = new Vector2(ratio, 0);
                _scrubberRect.anchorMax = new Vector2(ratio, 1);
            }

            if (UIPerformance.ShouldRun(UIPerformance.LowFrequency))
            {
                int currentTextHash;
                unchecked { currentTextHash = animationEditContext.clipTime.GetHashCode() + 31 * animationEditContext.current.animationLength.GetHashCode(); }

                if (currentTextHash != _lastTextHash)
                {
                    _lastTextHash = currentTextHash;
                    _timeText.text = $"{animationEditContext.clipTime:0.000}s / {animationEditContext.current.animationLength:0.000}s";
                }
            }
        }

        public void OnDisable()
        {
            _scrubberRect.anchorMin = new Vector2(0, 0);
            _scrubberRect.anchorMax = new Vector2(0, 1);
            _timeText.text = "Locked";
        }

        public void OnEnable()
        {
            if (animationEditContext == null) return;

            var ratio = (animationEditContext.clipTime - animationEditContext.scrubberRange.rangeBegin) / animationEditContext.scrubberRange.rangeDuration;
            _scrubberRect.anchorMin = new Vector2(ratio, 0);
            _scrubberRect.anchorMax = new Vector2(ratio, 1);
            _timeText.text = $"{animationEditContext.clipTime:0.000}s / {animationEditContext.current.animationLength:0.000}s";
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            eventData.useDragThreshold = false;
            UpdateScrubberFromView(eventData, true);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            animationEditContext.animation.paused = true;
            UpdateScrubberFromView(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            UpdateScrubberFromView(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            animationEditContext.animation.paused = false;
            UpdateScrubberFromView(eventData, true);
        }

        private void UpdateScrubberFromView(PointerEventData eventData, bool final = false)
        {
            if (animationEditContext == null) return;
            Vector2 localPosition;
            var rect = GetComponent<RectTransform>();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out localPosition))
                return;
            var actualSize = RectTransformUtility.PixelAdjustRect(rect, _canvas);
            var ratio = Mathf.Clamp01((localPosition.x - actualSize.x - _style.Padding) / (actualSize.width - _style.Padding * 2));
            var clickedTime = (ratio * animationEditContext.scrubberRange.rangeDuration) + animationEditContext.scrubberRange.rangeBegin;
            var time = clickedTime.Snap(final ? animationEditContext.snap : 0);
            if (time >= animationEditContext.current.animationLength - 0.001f)
            {
                if (animationEditContext.current.loop)
                    time = 0f;
                else
                    time = animationEditContext.current.animationLength;
            }
            animationEditContext.clipTime = time;
        }
    }
}
