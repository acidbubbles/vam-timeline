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
        public Color LabelsBackgroundColor { get; } = new Color(0.600f, 0.580f, 0.620f);
        public Color GroupBackgroundColorTop { get; } = new Color(0.874f, 0.870f, 0.870f);
        public Color GroupBackgroundColorBottom { get; } = new Color(0.704f, 0.700f, 0.700f);
        public Color LabelBackgroundColorTop { get; } = new Color(0.924f, 0.920f, 0.920f);
        public Color LabelBackgroundColorBottom { get; } = new Color(0.724f, 0.720f, 0.720f);
        public Color LabelBackgroundColorTopSelected { get; } = new Color(0.924f, 0.920f, 0.920f);
        public Color LabelBackgroundColorBottomSelected { get; } = new Color(1, 1, 1);
        public float RowHeight { get; } = 30f;
        public float RowSpacing { get; } = 5f;
        public float LabelWidth { get; } = 150f;
        public float LabelHorizontalPadding { get; } = 6f;
        public float GroupToggleWidth { get; } = 30f;

        // Timeline
        public float ZoomHeight { get; } = 30f;
        public float TimelineHeight { get; } = 60f;
        public float ToolbarHeight => ZoomHeight + TimelineHeight;

        // Keyframes
        public Color KeyframesRowLineColor { get; } = new Color(0.650f, 0.650f, 0.650f);
        public Color KeyframesRowLineColorSelected { get; } = new Color(0.750f, 0.750f, 0.750f);
        public Color KeyframeColor { get; } = new Color(0.050f, 0.020f, 0.020f);
        public Color KeyframeColorCurrentBack { get; } = new Color(0.050f, 0.020f, 0.020f);
        public Color KeyframeColorCurrentFront { get; } = new Color(0.350f, 0.320f, 0.320f);
        public Color KeyframeColorSelectedBack { get; } = new Color(0.050f, 0.020f, 0.020f);
        public Color KeyframeColorSelectedFront { get; } = new Color(0.950f, 0.820f, 0.920f);
        public float KeyframeSize { get; } = 6f;
        public float KeyframeSizeSelectedBack { get; } = 11f;
        public float KeyframeSizeSelectedFront { get; } = 5f;
        public float KeyframesRowPadding { get; } = 16f;
        public float KeyframesRowLineSize { get; } = 1f;

        // Scrubber
        public Color ScrubberColor { get; } = new Color(0.88f, 0.84f, 0.86f);
        public float ScrubberSize { get; } = 9f;
    }
}
