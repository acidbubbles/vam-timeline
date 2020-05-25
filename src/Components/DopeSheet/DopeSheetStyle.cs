using UnityEngine;

namespace VamTimeline
{
    public class DopeSheetStyle : StyleBase
    {
        public static DopeSheetStyle Default()
        {
            return new DopeSheetStyle();
        }

        // Global
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
        public float KeyframesRowPadding { get; set; } = 16f;
        public float KeyframesRowLineSize { get; set; } = 1f;

        // Scrubber
        public Color ScrubberColor { get; set; } = new Color(0.88f, 0.84f, 0.86f);
        public float ScrubberSize { get; set; } = 9f;
    }
}
