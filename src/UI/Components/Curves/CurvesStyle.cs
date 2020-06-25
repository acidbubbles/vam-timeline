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
        public float CurveLineSize { get; set; } = 2f;
        public float HandleSize { get; set; } = 5f;

        // Scrubber
        public float ScrubberSize { get; set; } = 2f;
        public Color ScrubberColor { get; set; } = new Color(0.88f, 0.84f, 0.86f);

        // Guides
        public float ZeroLineSize { get; set; } = 4f;
        public float SecondLineSize { get; set; } = 2f;
        public Color ZeroLineColor { get; set; } = new Color(0.4f, 0.4f, 0.45f);
        public Color SecondLineColor { get; set; } = new Color(0.6f, 0.6f, 0.65f);
        public Color CurveLineColorX { get; set; } = new Color(1.0f, 0.2f, 0.2f);
        public Color CurveLineColorY { get; set; } = new Color(0.2f, 1.0f, 0.2f);
        public Color CurveLineColorZ { get; set; } = new Color(0.2f, 0.2f, 1.0f);
        public Color CurveLineColorFloat { get; set; } = new Color(1f, 1f, 1f);
    }
}
