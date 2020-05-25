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
    public class DopeSheet
    {
        private readonly float _width;
        private readonly float _height;
        private readonly DopeSheetStyle _style;
        private readonly RectTransform _scrubberRect;
        private readonly VerticalLayoutGroup _layout;
        private float _scrubberMax;

        public DopeSheet(UIDynamic container, float width, float height, DopeSheetStyle style)
        {
            _width = width;
            _height = height;
            _style = style;

            var go = new GameObject("Dope Sheet");
            go.transform.SetParent(container.transform, false);

            CreateBackground(go, _style.BackgroundColor);

            _scrubberRect = CreateScrubber(go, _style.ScrubberColor).GetComponent<RectTransform>();

            var scrollView = CreateScrollView(go);
            var viewport = CreateViewport(scrollView);
            var content = CreateContent(viewport);
            var scrollRect = scrollView.GetComponent<ScrollRect>();
            scrollRect.viewport = viewport.GetComponent<RectTransform>();
            scrollRect.content = content.GetComponent<RectTransform>();
            _layout = content.GetComponent<VerticalLayoutGroup>();
        }

        private GameObject CreateBackground(GameObject parent, Color color)
        {
            var go = new GameObject();
            go.transform.SetParent(parent.transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.StretchParent(_width, _height);

            var image = go.AddComponent<Image>();
            image.color = color;

            return go;
        }

        private GameObject CreateScrubber(GameObject parent, Color color)
        {
            var go = new GameObject();
            go.transform.SetParent(parent.transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(0.5f, 1);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _style.ScrubberSize);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _height);

            var image = go.AddComponent<Image>();
            image.color = color;

            return go;
        }

        private GameObject CreateScrollView(GameObject parent)
        {
            var go = new GameObject("Scroll View");
            go.transform.SetParent(parent.transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.StretchParent(_width, _height);

            var scroll = go.AddComponent<ScrollRect>();
            scroll.horizontal = false;

            return go;
        }

        private GameObject CreateViewport(GameObject scrollView)
        {
            var go = new GameObject("Viewport");
            go.transform.SetParent(scrollView.transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.StretchParent(_width, _height);

            var image = go.AddComponent<Image>();

            var mask = go.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            return go;
        }

        public GameObject CreateContent(GameObject viewport)
        {
            var go = new GameObject("Content");
            go.transform.SetParent(viewport.transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.StretchTop(_width);

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

        public void Bind(IAtomAnimationClip clip)
        {
            _scrubberMax = clip.AnimationLength;
            // TODO: Instead of destroying children, try updating them (dirty + index)
            while (_layout.transform.childCount > 0)
            {
                var child = _layout.transform.GetChild(0);
                child.transform.parent = null;
                Object.Destroy(child);
            }
            foreach (var group in clip.GetTargetGroups())
            {
                if (group.Count > 0)
                {
                    CreateHeader(group.Label);

                    foreach (var target in group.GetTargets())
                        CreateRow(target);
                }
            }
        }

        private void CreateHeader(string title)
        {
            var go = new GameObject("Header");
            go.transform.SetParent(_layout.transform, false);

            var layout = go.AddComponent<LayoutElement>();
            layout.preferredHeight = _style.RowHeight;

            {
                var child = new GameObject();
                child.transform.SetParent(go.transform, false);

                var rect = child.AddComponent<RectTransform>();
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _width);
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _style.RowHeight);

                var image = child.AddComponent<GradientImage>();
                image.top = _style.GroupBackgroundColorTop;
                image.bottom = _style.GroupBackgroundColorBottom;
            }

            {
                var child = new GameObject();
                child.transform.SetParent(go.transform, false);

                var rect = child.AddComponent<RectTransform>();
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _width - 12f);
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _style.RowHeight);

                var text = child.AddComponent<Text>();
                text.text = title;
                text.font = _style.Font;
                text.fontSize = 24;
                text.color = _style.FontColor;
                text.fontStyle = FontStyle.Bold;
                text.alignment = TextAnchor.MiddleLeft;
            }
        }

        private void CreateRow(IAtomAnimationTarget target)
        {
            var go = new GameObject("Row");
            go.transform.SetParent(_layout.transform, false);

            var layout = go.AddComponent<LayoutElement>();
            layout.preferredHeight = _style.RowHeight;

            {
                var child = new GameObject();
                child.transform.SetParent(go.transform, false);

                var rect = child.AddComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(-_width / 2f + _style.LabelWidth / 2f, 0);
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _style.LabelWidth);
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _style.RowHeight);

                var image = child.AddComponent<GradientImage>();
                image.top = _style.LabelBackgroundColorTop;
                image.bottom = _style.LabelBackgroundColorBottom;
            }

            {
                var child = new GameObject();
                child.transform.SetParent(go.transform, false);

                var rect = child.AddComponent<RectTransform>();
                var padding = 2f;
                rect.anchoredPosition = new Vector2(-_width / 2f + _style.LabelWidth / 2f + padding, 0);
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _style.LabelWidth - padding * 2f);
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _style.RowHeight);

                var text = child.AddComponent<Text>();
                text.text = target.GetShortName();
                text.font = _style.Font;
                text.fontSize = 20;
                text.color = _style.FontColor;
                text.alignment = TextAnchor.MiddleLeft;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.resizeTextForBestFit = false; // Better but ugly if true

                var click = child.AddComponent<ClickAction>();
                click.onClick.AddListener(() =>
                {
                    SuperController.LogMessage("Select target " + target.GetShortName());
                });
            }

            {
                var child = new GameObject();
                child.transform.SetParent(go.transform, false);

                var keyframes = child.AddComponent<DopeSheetKeyframes>();
                keyframes.target = target;
                keyframes.style = _style;
                keyframes.rectTransform.anchoredPosition = new Vector2(_style.LabelWidth / 2f, 0);
                keyframes.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _width - _style.LabelWidth);
                keyframes.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _style.RowHeight);
            }
        }

        public void SetScrubberPosition(float val)
        {
            // TODO: Precalculate this
            var width = _width - _style.LabelWidth - _style.KeyframesRowPadding * 2f;
            var offsetX = _style.LabelWidth / 2f - width / 2f;// + _style.KeyframesRowPadding - _style.ScrubberSize / 2f;
            _scrubberRect.anchoredPosition = new Vector2(Mathf.Clamp(val / _scrubberMax, 0, _scrubberMax) * width + offsetX, _scrubberRect.anchoredPosition.y);
            // TODO: Implement
        }
    }
    public class ClickAction : MonoBehaviour, IPointerClickHandler
    {
        public UnityEvent onClick = new UnityEvent();

        public void OnPointerClick(PointerEventData eventData)
        {
            onClick.Invoke();
        }
    }
}
