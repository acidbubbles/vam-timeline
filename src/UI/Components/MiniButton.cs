using UnityEngine;
using UnityEngine.UI;

namespace VamTimeline
{
    public class MiniButtonStyle : StyleBase
    {
        public static MiniButtonStyle Default()
        {
            return new MiniButtonStyle();
        }

        public Color LabelBackgroundColorTop { get; } = new Color(0.924f, 0.920f, 0.920f);
        public Color LabelBackgroundColorBottom { get; } = new Color(0.724f, 0.720f, 0.720f);
        public Color LabelBackgroundColorTopSelected { get; } = new Color(0.924f, 0.920f, 0.920f);
        public Color LabelBackgroundColorBottomSelected { get; } = new Color(1, 1, 1);
    }

    public class MiniButton : MonoBehaviour
    {
        private static readonly MiniButtonStyle _style = MiniButtonStyle.Default();

        public static MiniButton Create(GameObject parent, string label)
        {
            var go = new GameObject("MiniButton");
            go.transform.SetParent(parent.transform, false);

            var rect = go.AddComponent<RectTransform>();

            var miniButton = go.AddComponent<MiniButton>();
            miniButton.rectTransform = rect;

            miniButton.text.text = label;

            return miniButton;
        }

        public MiniButton()
        {
            CreateBackground();
            CreateLabel();
        }

        private void CreateBackground()
        {
            var child = new GameObject();
            child.transform.SetParent(transform, false);

            var rect = child.AddComponent<RectTransform>();
            rect.StretchParent();

            image = child.AddComponent<GradientImage>();
            image.top = _style.LabelBackgroundColorTop;
            image.bottom = _style.LabelBackgroundColorBottom;
            image.raycastTarget = true;

            clickable = child.AddComponent<Clickable>();
        }

        private void CreateLabel()
        {
            var child = new GameObject();
            child.transform.SetParent(transform, false);

            var rect = child.AddComponent<RectTransform>();
            const float padding = 2f;
            rect.StretchParent();

            text = child.AddComponent<Text>();
            text.text = "Button";
            text.font = _style.Font;
            text.fontSize = 20;
            text.color = _style.FontColor;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.resizeTextForBestFit = false; // Better but ugly if true
            text.raycastTarget = false;
        }

        public RectTransform rectTransform { get; set; }
        public Clickable clickable { get; set; }
        public GradientImage image { get; set; }
        public Text text;

        private bool _selected;

        public bool selected
        {
            get
            {
                return _selected;
            }
            set
            {
                if(_selected == value) return;
                _selected = value;
                SyncSelected();
            }
        }

        private void SyncSelected()
        {
            if (_selected)
            {
                image.top = _style.LabelBackgroundColorTopSelected;
                image.bottom = _style.LabelBackgroundColorBottomSelected;
            }
            else
            {
                image.top = _style.LabelBackgroundColorTop;
                image.bottom = _style.LabelBackgroundColorBottom;
            }
        }
    }
}
