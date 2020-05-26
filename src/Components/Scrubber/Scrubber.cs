using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class Scrubber : MonoBehaviour, IPointerDownHandler, IPointerClickHandler,
    IPointerUpHandler, IPointerExitHandler, IPointerEnterHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private readonly ScrubberStyle _style = new ScrubberStyle();
        private readonly RectTransform _scrubberRect;
        private readonly Text _timeText;

        private float _previousTime = -1f;
        private float _previousMax = -1f;
        private ScrubberMarkers _markers;

        public JSONStorableFloat jsf { get; set; }

        public Scrubber()
        {
            gameObject.AddComponent<Canvas>();
            gameObject.AddComponent<GraphicRaycaster>();

            var image = gameObject.AddComponent<Image>();
            image.raycastTarget = false;

            var mask = gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            CreateBackground(gameObject, _style.BackgroundColor);
            CreateMarkers();
            _scrubberRect = CreateLine(gameObject, _style.ScrubberColor).GetComponent<RectTransform>();
            // TODO: Add second markers
            // TODO: Add keyframe markers (only for filtered targets)
            _timeText = CreateTime();
        }

        private GameObject CreateBackground(GameObject parent, Color color)
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
            if(_previousMax != jsf.max)
            {
                _previousMax = jsf.max;
                _previousTime = -1f;
                _markers.length = jsf.max;
            }

            if (_previousTime == jsf.val) return;

            // TODO: Change animation length won't update. TODO: Animation change events
            _previousTime = jsf.val;
            _previousMax = jsf.max;
            var ratio = Mathf.Clamp(jsf.val / jsf.max, 0, jsf.max);
            _scrubberRect.anchorMin = new Vector2(ratio, 0);
            _scrubberRect.anchorMax = new Vector2(ratio, 1);
            _timeText.text = $"{jsf.val:0.000}s / {jsf.max:0.000}s";
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            UpdateScrubberFromView(eventData);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            UpdateScrubberFromView(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
        }

        public void OnPointerExit(PointerEventData eventData)
        {
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            UpdateScrubberFromView(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            UpdateScrubberFromView(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            UpdateScrubberFromView(eventData);
        }

        private void UpdateScrubberFromView(PointerEventData eventData)
        {
            Vector2 localPosition;
            var rect = GetComponent<RectTransform>();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out localPosition))
                return;
            var ratio = Mathf.Clamp01((localPosition.x + rect.sizeDelta.x / 2f) / rect.sizeDelta.x);
            jsf.val = jsf.max * ratio;
        }
    }
}
