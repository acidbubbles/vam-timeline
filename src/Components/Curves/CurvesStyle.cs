namespace VamTimeline
{
    public class CurvesStyle : StyleBase
    {
        public static ScrubberStyle Default()
        {
            return new ScrubberStyle();
        }

        public float LineSize { get; set; } = 3f;
        public float HandleSize { get; set; } = 8f;
    }
}
