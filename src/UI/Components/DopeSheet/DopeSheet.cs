using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VamTimeline
{
    public class DopeSheet : MonoBehaviour
    {
        public class SetTimeEvent : UnityEvent<float> { }

        private readonly List<DopeSheetKeyframes> _keyframesRows = new List<DopeSheetKeyframes>();
        private readonly DopeSheetStyle _style = new DopeSheetStyle();
        private readonly RectTransform _scrubberRect;
        private readonly RectTransform _content;
        private AtomAnimation _animation;
        private IAtomAnimationClip _clip;
        private bool _bound;
        private int _ms;
        private bool _locked;

        public bool locked
        {
            get
            {
                return _locked;
            }
            set
            {
                _locked = value;
                _content.gameObject.SetActive(!value);
                _scrubberRect.gameObject.SetActive(!value);
            }
        }

        public DopeSheet()
        {
            CreateBackground(gameObject, _style.BackgroundColor);
            CreateLabelsBackground();

            _scrubberRect = CreateScrubber(gameObject, _style.ScrubberColor).GetComponent<RectTransform>();

            _content = VamPrefabFactory.CreateScrollRect(gameObject);
            _content.GetComponent<VerticalLayoutGroup>().spacing = _style.RowSpacing;
        }

        public void OnDisable()
        {
            if (_bound)
                UnbindClip();
        }

        public void OnEnable()
        {
            if (!_bound && _clip != null)
                BindClip(_clip);
        }

        public void OnDestroy()
        {
            if (_bound)
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

        private GameObject CreateLabelsBackground()
        {
            var go = new GameObject();
            go.transform.SetParent(transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = Vector2.zero;
            rect.sizeDelta = new Vector2(_style.LabelWidth, 0);

            var image = go.AddComponent<Image>();
            image.color = _style.LabelsBackgroundColor;
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

        public void Bind(AtomAnimation animation)
        {
            UnbindAnimation();

            _animation = animation;
            _animation.onTimeChanged.AddListener(OnTimeChanged);
            _animation.onCurrentAnimationChanged.AddListener(OnCurrentAnimationChanged);
            _animation.onAnimationSettingsChanged.AddListener(OnAnimationSettingsChanged);
            BindClip(_animation.current);
            SetScrubberPosition(_animation.clipTime, true);
        }

        private void UnbindAnimation()
        {
            if (_animation == null) return;

            _animation.onTimeChanged.RemoveListener(OnTimeChanged);
            _animation.onCurrentAnimationChanged.RemoveListener(OnCurrentAnimationChanged);
            _animation = null;

            UnbindClip();
        }

        public void Update()
        {
            if (_animation == null) return;
            if (!_animation.isPlaying) return;
            if (UIPerformance.ShouldSkip()) return;

            SetScrubberPosition(_animation.clipTime, false);
        }

        private void OnTimeChanged(AtomAnimation.TimeChangedEventArgs args)
        {
            SetScrubberPosition(args.currentClipTime, true);
        }

        private void OnCurrentAnimationChanged(AtomAnimation.CurrentAnimationChangedEventArgs args)
        {
            UnbindClip();
            BindClip(args.after);
        }

        private void OnAnimationSettingsChanged()
        {
            UnbindClip();
            BindClip(_animation.current);
        }

        private void BindClip(IAtomAnimationClip clip)
        {
            _clip = clip;
            _bound = true;
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
            _clip.onTargetsListChanged.AddListener(OnTargetsListChanged);
            _clip.onAnimationKeyframesRebuilt.AddListener(OnAnimationKeyframesRebuilt);
        }

        private void OnTargetsListChanged()
        {
            var clip = _clip;
            UnbindClip();
            BindClip(clip);
        }

        private void OnAnimationKeyframesRebuilt()
        {
            foreach (var keyframe in _keyframesRows)
            {
                keyframe.SetVerticesDirty();
            }
        }

        private void UnbindClip()
        {
            _clip.onTargetsListChanged.RemoveListener(OnTargetsListChanged);
            _clip.onAnimationKeyframesRebuilt.RemoveListener(OnAnimationKeyframesRebuilt);
            _keyframesRows.Clear();
            while (_content.transform.childCount > 0)
            {
                var child = _content.transform.GetChild(0);
                child.transform.SetParent(null, false);
                Destroy(child.gameObject);
            }
            _bound = false;
        }

        private void CreateHeader(IAtomAnimationTargetsList group)
        {
            var go = new GameObject("Header");
            go.transform.SetParent(_content.transform, false);

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
                text.text = group.label;
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
                    var selected = !targets.Any(t => t.selected);
                    foreach (var target in targets)
                        target.selected = selected;
                });
            }
        }

        private void CreateRow(IAtomAnimationTarget target)
        {
            var go = new GameObject("Row");
            go.transform.SetParent(_content.transform, false);

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
                    target.onSelectedChanged,
                    () => UpdateSelected(target, keyframes, labelBackgroundImage)
                );

                var click = child.AddComponent<Clickable>();
                click.onClick.AddListener(_ =>
                {
                    target.selected = !target.selected;
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
                _keyframesRows.Add(keyframes);
                // TODO: We could optimize here by using the AnimationCurve directly, avoiding a copy.
                keyframes.SetKeyframes(target.GetAllKeyframesTime(), _clip.loop);
                keyframes.SetTime(_ms);
                keyframes.style = _style;
                keyframes.raycastTarget = true;

                var listener = go.AddComponent<Listener>();
                listener.Bind(
                    target.onAnimationKeyframesRebuilt,
                    () =>
                    {
                        keyframes.SetKeyframes(target.GetAllKeyframesTime(), _clip.loop);
                        keyframes.SetTime(_ms);
                    }
                );

                var click = go.AddComponent<Clickable>();
                click.onClick.AddListener(eventData => OnClick(target, rect, eventData));
            }

            UpdateSelected(target, keyframes, labelBackgroundImage);
        }

        private void UpdateSelected(IAtomAnimationTarget target, DopeSheetKeyframes keyframes, GradientImage image)
        {
            if (target.selected)
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

        private void OnClick(IAtomAnimationTarget target, RectTransform rect, PointerEventData eventData)
        {
            if (_locked) return;

            Vector2 localPosition;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out localPosition))
                return;
            var width = rect.rect.width - _style.KeyframesRowPadding * 2f;
            var ratio = Mathf.Clamp01((localPosition.x + width / 2f) / width);
            var clickedTime = ratio * _clip.animationLength;
            _animation.clipTime = target.GetTimeClosestTo(clickedTime);
        }

        public void SetScrubberPosition(float time, bool stopped)
        {
            if (_locked) return;

            var ratio = Mathf.Clamp01(time / _clip.animationLength);
            _scrubberRect.anchorMin = new Vector2(ratio, 0);
            _scrubberRect.anchorMax = new Vector2(ratio, 1);
            if (stopped)
            {
                _ms = time.ToMilliseconds();
                foreach (var keyframe in _keyframesRows) keyframe.SetTime(_ms);
            }
        }
    }
}
