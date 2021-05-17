using UnityEngine;

namespace VamTimeline
{
    public class ScrubberStyle : StyleBase
    {
        // Scrubber
        public float Padding { get; } = 16f;
        public Color ScrubberColor { get; } = new Color(0.88f, 0.84f, 0.86f);
        public float ScrubberSize { get; } = 2f;
        public Color SecondsColor { get; } = new Color(0.50f, 0.48f, 0.48f);
        public float SecondsSize { get; } = 4f;
        public Color SecondFractionsColor { get; } = new Color(0.65f, 0.63f, 0.63f);
        public float SecondFractionsSize { get; } = 2.5f;
    }
}
