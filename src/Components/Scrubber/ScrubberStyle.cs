using UnityEngine;

namespace VamTimeline
{
    public class ScrubberStyle : StyleBase
    {
        public static ScrubberStyle Default()
        {
            return new ScrubberStyle();
        }

        // Scrubber
        public Color ScrubberColor { get; set; } = new Color(0.88f, 0.84f, 0.86f);
        public float ScrubberSize { get; set; } = 9f;
    }
}
