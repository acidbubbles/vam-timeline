using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VamTimeline
{
    public class DopeSheet : MonoBehaviour
    {
        private readonly List<DopeSheetKeyframes> _keyframesRows = new List<DopeSheetKeyframes>();
        private readonly DopeSheetStyle _style = new DopeSheetStyle();
        private readonly RectTransform _scrubberRect;
        private readonly RectTransform _content;
        private AtomAnimationEditContext _animationEditContext;
        private IAtomAnimationClip _clip;
        private Curves _curves;
        private bool _bound;
        private int _ms;

        public DopeSheet()
        {
            CreateBackground(gameObject, _style.BackgroundColor);
            var editor = CreateEditor();
            _content = VamPrefabFactory.CreateScrollRect(editor.gameObject);
            _content.GetComponent<VerticalLayoutGroup>().spacing = _style.RowSpacing;
            _scrubberRect = CreateScrubber(_content.parent.gameObject, _style.ScrubberColor).GetComponent<RectTransform>();
            CreateLabelsBackground(_content);
            _curves = CreateCurves(editor);
            CreateToolbox();
        }

        private GameObject CreateEditor()
        {
            var go = new GameObject("DopeSheetEditor");
            go.transform.SetParent(transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.StretchParent();
            rect.offsetMax = new Vector2(0, -_style.TimelineHeight);

            return go;
        }

        private void CreateToolbox()
        {
            var go = new GameObject("DopeSheetToolbox");
            go.transform.SetParent(transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.StretchTop();
            rect.sizeDelta = new Vector2(0, _style.TimelineHeight);
            rect.anchoredPosition = new Vector2(0, -_style.TimelineHeight / 2f);

            // TEMP

            var image = go.AddComponent<Image>();
            image.color = Color.red;

            go.AddComponent<Button>().onClick.AddListener(() => { _curves.gameObject.SetActive(!_curves.gameObject.activeSelf); });
        }

        private Curves CreateCurves(GameObject parent)
        {
            var go = new GameObject("Curves");
            go.transform.SetParent(parent.transform, false);
            go.SetActive(false);

            var rect = go.AddComponent<RectTransform>();
            rect.StretchParent();
            rect.anchoredPosition = new Vector2(_style.LabelWidth / 2f, 0);
            rect.sizeDelta = new Vector2(-_style.LabelWidth, 0);

            return go.AddComponent<Curves>();
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

        private static GameObject CreateBackground(GameObject parent, Color color)
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

        private GameObject CreateLabelsBackground(Transform content)
        {
            var go = new GameObject();
            go.transform.SetParent(content.parent, false);
            go.transform.SetSiblingIndex(1);

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
            rect.SetSiblingIndex(0);

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

        public void Bind(AtomAnimationEditContext animationEditContext)
        {
            UnbindAnimation();

            _animationEditContext = animationEditContext;
            _animationEditContext.onTimeChanged.AddListener(OnTimeChanged);
            _animationEditContext.onScrubberRangeChanged.AddListener(OnScrubberRangeChanged);
            _animationEditContext.onCurrentAnimationChanged.AddListener(OnCurrentAnimationChanged);
            _animationEditContext.animation.onAnimationSettingsChanged.AddListener(OnAnimationSettingsChanged);

            BindClip(_animationEditContext.current);
            SetScrubberPosition(_animationEditContext.clipTime, true);
            _curves.Bind(_animationEditContext);
        }

        private void UnbindAnimation()
        {
            if (_animationEditContext == null) return;

            _animationEditContext.onTimeChanged.RemoveListener(OnTimeChanged);
            _animationEditContext.onScrubberRangeChanged.RemoveListener(OnScrubberRangeChanged);
            _animationEditContext.onCurrentAnimationChanged.RemoveListener(OnCurrentAnimationChanged);
            _animationEditContext.animation.onAnimationSettingsChanged.RemoveListener(OnAnimationSettingsChanged);
            _animationEditContext = null;

            UnbindClip();
        }

        public void Update()
        {
            if (_animationEditContext == null) return;
            if (!_animationEditContext.animation.isPlaying) return;
            if (UIPerformance.ShouldSkip()) return;

            SetScrubberPosition(_animationEditContext.clipTime, false);
        }

        private void OnTimeChanged(AtomAnimationEditContext.TimeChangedEventArgs args)
        {
            SetScrubberPosition(args.currentClipTime, true);
        }

        private void OnScrubberRangeChanged(AtomAnimationEditContext.ScrubberRangeChangedEventArgs args)
        {
            foreach (var keyframe in _keyframesRows) keyframe.SetRange(args.scrubberRange.rangeBegin, args.scrubberRange.rangeDuration);
            SetScrubberPosition(_clip.clipTime, true);
        }

        private void OnCurrentAnimationChanged(AtomAnimationEditContext.CurrentAnimationChangedEventArgs args)
        {
            UnbindClip();
            BindClip(args.after);
            SetScrubberPosition(_animationEditContext.clipTime, true);
        }

        private void OnAnimationSettingsChanged()
        {
            UnbindClip();
            BindClip(_animationEditContext.current);
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
            _scrubberRect?.gameObject.SetActive(any);
            _clip.onTargetsListChanged.AddListener(OnTargetsListChanged);
        }

        private void OnTargetsListChanged()
        {
            var clip = _clip;
            UnbindClip();
            BindClip(clip);
        }

        private void UnbindClip()
        {
            _clip.onTargetsListChanged.RemoveListener(OnTargetsListChanged);
            _keyframesRows.Clear();
            _bound = false;
            if (_content == null) return;
            while (_content.childCount > 0)
            {
                var child = _content.GetChild(0);
                child.transform.SetParent(null, false);
                Destroy(child.gameObject);
            }
        }

        private void CreateHeader(IAtomAnimationTargetsList group)
        {
            var go = new GameObject("Header");
            go.transform.SetParent(_content, false);

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
                    var selected = targets.Any(t => _animationEditContext.IsSelected(t));
                    foreach (var target in targets)
                        _animationEditContext.SetSelected(target, !selected);
                });
            }
        }

        private void CreateRow(IAtomAnimationTarget target)
        {
            var go = new GameObject("Row");
            go.transform.SetParent(_content, false);

            var layout = go.AddComponent<LayoutElement>();
            layout.preferredHeight = _style.RowHeight;

            DopeSheetKeyframes keyframes = null;
            GradientImage labelBackgroundImage = null;

            {
                var child = new GameObject();
                child.transform.SetParent(go.transform, false);

                var rect = child.AddComponent<RectTransform>();
                rect.StretchParent();
                rect.anchoredPosition = new Vector2(_style.LabelWidth / 2f, 0);
                rect.sizeDelta = new Vector2(-_style.LabelWidth, 0);

                keyframes = child.AddComponent<DopeSheetKeyframes>();
                _keyframesRows.Add(keyframes);
                keyframes.SetKeyframes(target.GetAllKeyframesTime(), _clip.loop);
                keyframes.SetRange(_animationEditContext.scrubberRange.rangeBegin, _animationEditContext.scrubberRange.rangeDuration);
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
                        keyframes.SetVerticesDirty();
                    }
                );

                var click = go.AddComponent<Clickable>();
                click.onClick.AddListener(eventData => OnClick(target, rect, eventData));
            }

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
                // TODO: Change this for a dictionary and listen once instead of once per row!
                listener.Bind(
                    _animationEditContext.onTargetsSelectionChanged,
                    // ReSharper disable once AccessToModifiedClosure
                    () => UpdateSelected(target, keyframes, labelBackgroundImage)
                );

                var click = child.AddComponent<Clickable>();

                click.onClick.AddListener(_ =>
                {
                    _animationEditContext.SetSelected(target, !_animationEditContext.IsSelected(target));
                });

                click.onRightClick.AddListener(_ =>
                {
                    target.SelectInVam();
                });
            }

            {
                var child = new GameObject();
                child.transform.SetParent(go.transform, false);

                var rect = child.AddComponent<RectTransform>();
                const float padding = 2f;
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

            UpdateSelected(target, keyframes, labelBackgroundImage);
        }

        private void UpdateSelected(IAtomAnimationTarget target, DopeSheetKeyframes keyframes, GradientImage image)
        {
            var selected = _animationEditContext.IsSelected(target);
            if (keyframes.selected == selected) return;

            if (selected)
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
            if (!_animationEditContext.CanEdit() || _animationEditContext.scrubberRange.rangeDuration == 0) return;

            Vector2 localPosition;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out localPosition))
                return;
            var width = rect.rect.width - _style.KeyframesRowPadding * 2f;
            var ratio = Mathf.Clamp01((localPosition.x + width / 2f) / width);
            var clickedTime = (ratio * _animationEditContext.scrubberRange.rangeDuration) + _animationEditContext.scrubberRange.rangeBegin;
            var previousClipTime = _animationEditContext.clipTime;
            _animationEditContext.clipTime = target.GetTimeClosestTo(clickedTime);
            if (!_animationEditContext.IsSelected(target))
            {
                _animationEditContext.SetSelected(target, true);
                if (!Input.GetKey(KeyCode.LeftControl))
                {
                    foreach (var t in _animationEditContext.selectedTargets.Where(x => x != target).ToList())
                        _animationEditContext.SetSelected(t, false);
                }
            }
            else if (previousClipTime == _animationEditContext.clipTime)
            {
                _animationEditContext.SetSelected(target, false);
                if (!Input.GetKey(KeyCode.LeftControl))
                {
                    foreach (var t in _animationEditContext.selectedTargets.Where(x => x != target).ToList())
                        _animationEditContext.SetSelected(t, false);
                }
            }
        }

        public void SetScrubberPosition(float time, bool stopped)
        {
            if (_scrubberRect == null) return;
            if (_animationEditContext.scrubberRange.rangeDuration == 0) return;

            var ratio = (time - _animationEditContext.scrubberRange.rangeBegin) / _animationEditContext.scrubberRange.rangeDuration;

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
