using UnityEngine;

namespace VamTimeline
{
    public class ZoomStyle : StyleBase
    {

        // Zoom
        public override Color BackgroundColor { get; } = new Color(0.521f, 0.482f, 0.541f);
        public override Color FontColor { get; } = new Color(0.821f, 0.782f, 0.841f);
        public float Padding { get; } = 16f;
        public float VerticalPadding => 4f;
        public Color FullSectionColor { get; } = new Color(0.321f, 0.282f, 0.341f);
        public Color ZoomedSectionColor { get; } = new Color(0.1f, 0.1f, 0.1f);
    }
}
