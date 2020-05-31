using UnityEngine;

namespace VamTimeline
{
    public class CurvesStyle : StyleBase
    {
        public static ScrubberStyle Default()
        {
            return new ScrubberStyle();
        }

        // Curves
        public float CurveLineSize { get; set; } = 3f;
        public float HandleSize { get; set; } = 8f;

        // Scrubber
        public float ScrubberSize { get; set; } = 2f;
        public Color ScrubberColor { get; set; } = new Color(0.88f, 0.84f, 0.86f);

        // Guides
        public float ZeroLineSize { get; set; } = 4f;
        public float SecondLineSize { get; set; } = 2f;
        public Color ZeroLineColor { get; set; } = new Color(0.4f, 0.4f, 0.45f);
        public Color SecondLineColor { get; set; } = new Color(0.6f, 0.6f, 0.65f);
    }
}
