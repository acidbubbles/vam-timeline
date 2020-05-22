using UnityEngine;
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
        private readonly GameObject _gameObject;
        private readonly VerticalLayoutGroup _gridLayout;

        public DopeSheet(UIDynamic container, float width, float height, DopeSheetStyle style)
        {
            _width = width;
            _height = height;
            _style = style;

            _gameObject = new GameObject();
            _gameObject.transform.SetParent(container.transform, false);

            var mask = _gameObject.AddComponent<RectMask2D>();
            mask.rectTransform.anchoredPosition = new Vector2(0, 0);
            mask.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            mask.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

            var backgroundContent = new GameObject();
            backgroundContent.transform.SetParent(_gameObject.transform, false);

            var backgroundImage = backgroundContent.AddComponent<Image>();
            backgroundImage.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            backgroundImage.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            backgroundImage.color = _style.BackgroundColor;

            var sheetContainer = new GameObject();
            sheetContainer.transform.SetParent(container.transform, false);

            var rectTransform = sheetContainer.AddComponent<RectTransform>();
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

            _gridLayout = sheetContainer.AddComponent<VerticalLayoutGroup>();
            _gridLayout.spacing = _style.RowSpacing;
            _gridLayout.childForceExpandHeight = false;
            _gridLayout.childControlHeight = true;
            _gridLayout.childAlignment = TextAnchor.UpperLeft;
        }

        public void Draw(AtomAnimationClip clip)
        {
            if (clip.TargetControllers.Count > 0)
            {
                CreateHeader("Controllers");

                foreach (var target in clip.TargetControllers)
                    CreateRow(target);
            }

            if (clip.TargetFloatParams.Count > 0)
            {
                CreateHeader("Params");

                foreach (var target in clip.TargetFloatParams)
                    CreateRow(target);
            }
        }

        private void CreateHeader(string title)
        {
            var rowContainer = new GameObject();
            rowContainer.transform.SetParent(_gridLayout.transform, false);

            var layout = rowContainer.AddComponent<LayoutElement>();
            layout.preferredHeight = _style.RowHeight;

            var backgroundContent = new GameObject();
            backgroundContent.transform.SetParent(rowContainer.transform, false);

            var backgroundImage = backgroundContent.AddComponent<Image>();
            backgroundImage.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _width);
            backgroundImage.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _style.RowHeight);
            backgroundImage.color = _style.GroupBackgroundColor;

            var textContent = new GameObject();
            textContent.transform.SetParent(rowContainer.transform, false);

            var text = textContent.AddComponent<Text>();
            text.text = title;
            text.font = _style.Font;
            text.fontSize = 13;
            text.color = _style.FontColor;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleLeft;
            text.material = _style.Font.material;
        }

        private void CreateRow(IAnimationTarget target)
        {
            var rowContainer = new GameObject();
            rowContainer.transform.SetParent(_gridLayout.transform, false);

            var layout = rowContainer.AddComponent<LayoutElement>();
            layout.preferredHeight = _style.RowHeight;

            var backgroundContent = new GameObject();
            backgroundContent.transform.SetParent(rowContainer.transform, false);

            var backgroundImage = backgroundContent.AddComponent<Image>();
            backgroundImage.rectTransform.anchoredPosition = new Vector2(-_width / 2f + _style.LabelWidth / 2f, 0);
            backgroundImage.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _style.LabelWidth);
            backgroundImage.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _style.RowHeight);
            backgroundImage.color = _style.LabelBackgroundColor;

            var textContent = new GameObject();
            textContent.transform.SetParent(rowContainer.transform, false);

            var text = textContent.AddComponent<Text>();
            text.rectTransform.anchoredPosition = new Vector2(-_width / 2f + _style.LabelWidth / 2f, 0);
            text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _style.LabelWidth);
            text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _style.RowHeight);
            text.text = target.Name;
            text.font = _style.Font;
            text.fontSize = 10;
            text.color = _style.FontColor;
            text.alignment = TextAnchor.MiddleLeft;
            text.material = _style.Font.material;

            var keyframesContent = new GameObject();
            keyframesContent.transform.SetParent(rowContainer.transform, false);

            var keyframes = keyframesContent.AddComponent<DopeSheetKeyframes>();
            keyframes.target = target;
            keyframes.style = _style;
            keyframes.rectTransform.anchoredPosition = new Vector2(_style.LabelWidth / 2f, 0);
            keyframes.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _width - _style.LabelWidth);
            keyframes.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _style.RowHeight);
        }
    }
}
