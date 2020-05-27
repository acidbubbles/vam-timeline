namespace VamTimeline
{
    public class CurvesStyle : StyleBase
    {
        public static ScrubberStyle Default()
        {
            return new ScrubberStyle();
        }

        public float LineSize { get; set; } = 4f;
    }
}
