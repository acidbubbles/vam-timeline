using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
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
    public class DopeSheet : MonoBehaviour
    {
        public class SetTimeEvent : UnityEvent<float> { }

        private readonly List<DopeSheetKeyframes> _keyframesRows = new List<DopeSheetKeyframes>();
        private readonly DopeSheetStyle _style = new DopeSheetStyle();
        private readonly RectTransform _scrubberRect;
        private readonly ScrollRect _scrollRect;
        private readonly VerticalLayoutGroup _layout;
        private AtomAnimation _animation;
        private IAtomAnimationClip _clip;

        public DopeSheet()
        {
            gameObject.AddComponent<Canvas>();
            gameObject.AddComponent<GraphicRaycaster>();

            CreateBackground(gameObject, _style.BackgroundColor);

            _scrubberRect = CreateScrubber(gameObject, _style.ScrubberColor).GetComponent<RectTransform>();

            var scrollView = CreateScrollView(gameObject);
            var viewport = CreateViewport(scrollView);
            var content = CreateContent(viewport);
            _scrollRect = scrollView.GetComponent<ScrollRect>();
            _scrollRect.viewport = viewport.GetComponent<RectTransform>();
            _scrollRect.content = content.GetComponent<RectTransform>();
            _layout = content.GetComponent<VerticalLayoutGroup>();
        }

        public void Start()
        {
            _scrollRect.verticalNormalizedPosition = 1f;
        }

        public void OnDestroy()
        {
            UnbindClip();
        }

        private GameObject CreateBackground(GameObject parent, Color color)
        {
            var go = new GameObject();
            go.transform.SetParent(parent.transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.StretchParent();

            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            return go;
        }

        private GameObject CreateScrubber(GameObject parent, Color color)
        {
            var go = new GameObject("Scrubber");
            go.transform.SetParent(parent.transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.StretchParent();
            rect.anchoredPosition = new Vector2(_style.LabelWidth / 2f, 0);
            rect.sizeDelta = new Vector2(-_style.LabelWidth - _style.KeyframesRowPadding * 2f, 0);

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

        private GameObject CreateScrollView(GameObject parent)
        {
            var go = new GameObject("Scroll View");
            go.transform.SetParent(parent.transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.StretchParent();

            var scroll = go.AddComponent<ScrollRect>();
            scroll.horizontal = false;

            return go;
        }

        private GameObject CreateViewport(GameObject scrollView)
        {
            var go = new GameObject("Viewport");
            go.transform.SetParent(scrollView.transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.StretchParent();

            var image = go.AddComponent<Image>();
            image.raycastTarget = true;

            var mask = go.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            return go;
        }

        public GameObject CreateContent(GameObject viewport)
        {
            var go = new GameObject("Content");
            go.transform.SetParent(viewport.transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.StretchTop();
            rect.pivot = new Vector2(0, 1);

            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = _style.RowSpacing;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childAlignment = TextAnchor.UpperLeft;

            var fit = go.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return go;
        }

        public void Bind(AtomAnimation animation)
        {
            UnbindAnimation();

            // TODO: Unbind the events on destroy and re-bind to a new animation (load)
            _animation = animation;
            _animation.TimeChanged.AddListener(OnTimeChanged);
            _animation.CurrentAnimationChanged.AddListener(OnCurrentAnimationChanged);
            BindClip(_animation.Current);
            SetScrubberPosition(_animation.Time, true);
        }

        private void UnbindAnimation()
        {
            if (_animation == null) return;

            _animation.TimeChanged.RemoveListener(OnTimeChanged);
            _animation.CurrentAnimationChanged.RemoveListener(OnCurrentAnimationChanged);
            _animation = null;

            UnbindClip();
        }

        public void Update()
        {
            if (_animation != null && _animation.IsPlaying())
                SetScrubberPosition(_animation.Time, false);
        }

        private void OnTimeChanged(float time)
        {
            SetScrubberPosition(time, true);
        }

        private void OnCurrentAnimationChanged(AtomAnimation.CurrentAnimationChangedEventArgs args)
        {
            // TODO: Instead of destroying children, try updating them (dirty + index)
            // TODO: If the current clip doesn't change, do not rebind
            UnbindClip();

            BindClip(args.After);
        }

        private void BindClip(IAtomAnimationClip clip)
        {
            _clip = clip;
            var any = false;
            foreach (var group in _clip.GetTargetGroups())
            {
                if (group.Count > 0)
                {
                    any = true;
                    CreateHeader(group);

                    foreach (var target in group.GetTargets())
                        CreateRow(target);
                }
            }
            _scrubberRect.gameObject.SetActive(any);
            _clip.TargetsListChanged.AddListener(OnAnimationModified);
            _clip.AnimationModified.AddListener(OnAnimationModified);
        }

        private void OnAnimationModified()
        {
            var clip = _clip;
            UnbindClip();
            BindClip(clip);
        }

        private void UnbindClip()
        {
            _clip.TargetsListChanged.RemoveListener(OnAnimationModified);
            _clip.AnimationModified.RemoveListener(OnAnimationModified);
            _keyframesRows.Clear();
            while (_layout.transform.childCount > 0)
            {
                var child = _layout.transform.GetChild(0);
                child.transform.SetParent(null, false);
                Destroy(child.gameObject);
            }
            _clip = null;
        }

        private void CreateHeader(IAtomAnimationTargetsList group)
        {
            var go = new GameObject("Header");
            go.transform.SetParent(_layout.transform, false);

            var layout = go.AddComponent<LayoutElement>();
            layout.preferredHeight = _style.RowHeight;

            {
                var child = new GameObject();
                child.transform.SetParent(go.transform, false);

                var rect = child.AddComponent<RectTransform>();
                rect.StretchParent();

                var image = child.AddComponent<GradientImage>();
                image.top = _style.GroupBackgroundColorTop;
                image.bottom = _style.GroupBackgroundColorBottom;
                image.raycastTarget = false;
            }

            {
                var child = new GameObject();
                child.transform.SetParent(go.transform, false);

                var rect = child.AddComponent<RectTransform>();
                rect.StretchParent();
                rect.offsetMin = new Vector2(6f, 0);
                rect.offsetMax = new Vector2(-6f, 0);

                var text = child.AddComponent<Text>();
                text.text = group.Label;
                text.font = _style.Font;
                text.fontSize = 24;
                text.color = _style.FontColor;
                text.fontStyle = FontStyle.Bold;
                text.alignment = TextAnchor.MiddleLeft;
                text.raycastTarget = true;

                var click = child.AddComponent<Clickable>();
                click.onClick.AddListener(_ =>
                {
                    var targets = group.GetTargets().ToList();
                    var selected = !targets.Any(t => t.Selected);
                    foreach (var target in targets)
                        target.Selected = selected;
                });
            }
        }

        private void CreateRow(IAtomAnimationTarget target)
        {
            var go = new GameObject("Row");
            go.transform.SetParent(_layout.transform, false);

            var layout = go.AddComponent<LayoutElement>();
            layout.preferredHeight = _style.RowHeight;

            DopeSheetKeyframes keyframes = null;
            GradientImage labelBackgroundImage = null;

            {
                var child = new GameObject();
                child.transform.SetParent(go.transform, false);

                var rect = child.AddComponent<RectTransform>();
                rect.StretchLeft();
                rect.anchoredPosition = new Vector2(_style.LabelWidth / 2f, 0);
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _style.LabelWidth);

                labelBackgroundImage = child.AddComponent<GradientImage>();
                labelBackgroundImage.top = _style.LabelBackgroundColorTop;
                labelBackgroundImage.bottom = _style.LabelBackgroundColorBottom;
                labelBackgroundImage.raycastTarget = true;

                var listener = child.AddComponent<Listener>();
                listener.Bind(
                    target.SelectedChanged,
                    () => UpdateSelected(target, keyframes, labelBackgroundImage)
                );

                var click = child.AddComponent<Clickable>();
                click.onClick.AddListener(_ =>
                {
                    target.Selected = !target.Selected;
                });
            }

            {
                var child = new GameObject();
                child.transform.SetParent(go.transform, false);

                var rect = child.AddComponent<RectTransform>();
                var padding = 2f;
                rect.StretchLeft();
                rect.anchoredPosition = new Vector2(_style.LabelWidth / 2f, 0);
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _style.LabelWidth - padding * 2);

                var text = child.AddComponent<Text>();
                text.text = target.GetShortName();
                text.font = _style.Font;
                text.fontSize = 20;
                text.color = _style.FontColor;
                text.alignment = TextAnchor.MiddleLeft;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.resizeTextForBestFit = false; // Better but ugly if true
                text.raycastTarget = false;
            }

            {
                var child = new GameObject();
                child.transform.SetParent(go.transform, false);

                var rect = child.AddComponent<RectTransform>();
                rect.StretchParent();
                rect.anchoredPosition = new Vector2(_style.LabelWidth / 2f, 0);
                rect.sizeDelta = new Vector2(-_style.LabelWidth, 0);

                keyframes = child.AddComponent<DopeSheetKeyframes>();
                keyframes.SetKeyframes(target.GetAllKeyframesTime(), _clip.Loop);
                keyframes.SetTime(0);
                keyframes.style = _style;
                keyframes.raycastTarget = true;
                _keyframesRows.Add(keyframes);

                var targetWithCurves = target as IAnimationTargetWithCurves;
                if (targetWithCurves != null)
                {
                    var click = go.AddComponent<Clickable>();
                    click.onClick.AddListener(eventData => OnClick(targetWithCurves, rect, eventData));
                }
            }

            UpdateSelected(target, keyframes, labelBackgroundImage);
        }

        private void UpdateSelected(IAtomAnimationTarget target, DopeSheetKeyframes keyframes, GradientImage image)
        {
            if (target.Selected)
            {
                keyframes.selected = true;
                image.top = _style.LabelBackgroundColorTopSelected;
                image.bottom = _style.LabelBackgroundColorBottomSelected;
            }
            else
            {
                keyframes.selected = false;
                image.top = _style.LabelBackgroundColorTop;
                image.bottom = _style.LabelBackgroundColorBottom;
            }
        }

        private void OnClick(IAnimationTargetWithCurves target, RectTransform rect, PointerEventData eventData)
        {
            Vector2 localPosition;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out localPosition))
                return;
            var curve = target.GetLeadCurve();
            var width = rect.rect.width - _style.KeyframesRowPadding * 2f;
            var ratio = Mathf.Clamp01((localPosition.x + width / 2f) / width);
            var closest = curve.KeyframeBinarySearch(ratio * _clip.AnimationLength, true);
            var time = curve[closest].time;
            _animation.Time = time;
        }

        public void SetScrubberPosition(float time, bool stopped)
        {
            if (_clip == null) return; // TODO: Delete this line after events conversion
            var ratio = Mathf.Clamp01(time / _clip.AnimationLength);
            _scrubberRect.anchorMin = new Vector2(ratio, 0);
            _scrubberRect.anchorMax = new Vector2(ratio, 1);
            if (stopped)
            {
                var ms = time.ToMilliseconds();
                foreach (var keyframe in _keyframesRows) keyframe.SetTime(ms);
            }
        }
    }
}
