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
        public float LineSize { get; set; } = 3f;
        public float HandleSize { get; set; } = 8f;

        // Scrubber
        public float ScrubberSize { get; set; } = 2f;
        public Color ScrubberColor { get; set; } = new Color(0.88f, 0.84f, 0.86f);
    }
}
