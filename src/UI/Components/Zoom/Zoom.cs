using UnityEngine;
using UnityEngine.UI;

namespace VamTimeline
{
    public class Zoom : MonoBehaviour
    {
        private readonly ZoomStyle _style = new ZoomStyle();
        private readonly ZoomControl _zoomControl;
        private readonly ZoomControlGraphics _zoomGraphics;
        private readonly ZoomTime _time;
        private readonly Text _zoomText;

        private AtomAnimationEditContext _animationEditContext;

        public Zoom()
        {
            var image = gameObject.AddComponent<Image>();
            image.raycastTarget = false;

            var mask = gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            CreateBackground();
            _zoomControl = CreateZoomControl();
            _zoomGraphics = CreateZoomGraphics(_zoomControl.gameObject);
            _zoomControl.graphics = _zoomGraphics;
            _time = CreateTime();
            _zoomText = CreateZoomText();
        }

        public void Bind(AtomAnimationEditContext animationEditContext)
        {
            _animationEditContext = animationEditContext;
            _animationEditContext.onScrubberRangeChanged.AddListener(OnScrubberRangeChanged);
            _zoomControl.animationEditContext = _animationEditContext;
            _zoomGraphics.animationEditContext = _animationEditContext;
            OnScrubberRangeChanged(new AtomAnimationEditContext.ScrubberRangeChangedEventArgs {scrubberRange = _animationEditContext.scrubberRange});
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

        private ZoomControl CreateZoomControl()
        {
            var go = new GameObject();
            go.transform.SetParent(transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.StretchParent();
            rect.offsetMin = new Vector2(_style.Padding, _style.VerticalPadding);
            rect.offsetMax = new Vector2(-_style.Padding, -_style.VerticalPadding);

            var control = go.AddComponent<ZoomControl>();
            control.style = _style;

            return control;
        }

        private ZoomControlGraphics CreateZoomGraphics(GameObject go)
        {
            var graphics = go.AddComponent<ZoomControlGraphics>();
            graphics.raycastTarget = true;
            graphics.style = _style;
            return graphics;
        }

        private ZoomTime CreateTime()
        {
            var go = new GameObject();
            go.transform.SetParent(transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.StretchParent();
            rect.offsetMin = new Vector2(_style.Padding, _style.VerticalPadding);
            rect.offsetMax = new Vector2(-_style.Padding, -_style.VerticalPadding);

            var graphics = go.AddComponent<ZoomTime>();
            graphics.raycastTarget = false;
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
            _zoomGraphics.SetVerticesDirty();
            _time.animationLength = _animationEditContext.current.animationLength;
            _time.time = _animationEditContext.clipTime;
            _time.SetVerticesDirty();
            _zoomText.text = args.scrubberRange.rangeDuration == _animationEditContext.current.animationLength ? "100%" : $"{args.scrubberRange.rangeBegin:0.0}s - {args.scrubberRange.rangeBegin + args.scrubberRange.rangeDuration:0.0}s";
        }

        public void Update()
        {
            if (!UIPerformance.ShouldRun()) return;
            if (_time.time == _animationEditContext.clipTime) return;
            _time.time = _animationEditContext.clipTime;
            _time.SetVerticesDirty();
        }
    }
}
