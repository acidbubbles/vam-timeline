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
        public Color GroupBackgroundColor { get; set; } = new Color(0.774f, 0.770f, 0.770f);
        public Color LabelBackgroundColor { get; set; } = new Color(0.521f, 0.482f, 0.541f);
        public float RowHeight { get; set; } = 20f;
        public float RowSpacing { get; set; } = 5f;
        public float LabelWidth { get; set; } = 150f;

        // Keyframes
        public Color KeyframesRowLineColor { get; set; } = new Color(0.650f, 0.650f, 0.650f);
        public Color KeyframeColor { get; set; } = new Color(0.050f, 0.020f, 0.020f);
        public float KeyframeSize { get; set; } = 6f;
        public float KeyframesRowPadding { get; set; } = 10f;
        public float KeyframesRowLineSize { get; set; } = 1f;

        public DopeSheetStyle()
        {
            Font = (Font)Resources.GetBuiltinResource(typeof(Font), "Arial.ttf");
        }
    }
}
