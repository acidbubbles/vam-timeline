using UnityEngine;

namespace VamTimeline
{
    public class DopeSheetStyle
    {
        public static DopeSheetStyle Default()
        {
            return new DopeSheetStyle();
        }

        // Global
        public Font Font { get; }
        public Color FontColor { get; set; } = new Color(0, 0, 0);
        public Color BackgroundColor { get; set; } = new Color(0.721f, 0.682f, 0.741f);
        public Color GroupBackgroundColorTop { get; set; } = new Color(0.874f, 0.870f, 0.870f);
        public Color GroupBackgroundColorBottom { get; set; } = new Color(0.704f, 0.700f, 0.700f);
        public Color LabelBackgroundColorTop { get; set; } = new Color(0.974f, 0.970f, 0.970f);
        public Color LabelBackgroundColorBottom { get; set; } = new Color(0.824f, 0.820f, 0.820f);
        public float RowHeight { get; set; } = 30f;
        public float RowSpacing { get; set; } = 5f;
        public float LabelWidth { get; set; } = 150f;

        // Keyframes
        public Color KeyframesRowLineColor { get; set; } = new Color(0.650f, 0.650f, 0.650f);
        public Color KeyframeColor { get; set; } = new Color(0.050f, 0.020f, 0.020f);
        public float KeyframeSize { get; set; } = 7f;
        public float KeyframesRowPadding { get; set; } = 10f;
        public float KeyframesRowLineSize { get; set; } = 1f;

        public DopeSheetStyle()
        {
            Font = (Font)Resources.GetBuiltinResource(typeof(Font), "Arial.ttf");
        }
    }
}
