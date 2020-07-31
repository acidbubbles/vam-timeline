namespace VamTimeline
{
    public class BezierKeyframe
    {
        public float time;
        public float value;
        public float controlPointIn;
        public float controlPointOut;
        public int curveType;

        public BezierKeyframe()
        {
        }

        public BezierKeyframe(float time, float value, int curveType)
            : this(time, value, curveType, value, value)
        {

        }

        public BezierKeyframe(float time, float value, int curveType, float controlPointIn, float controlPointOut)
        {
            this.time = time;
            this.value = value;
            this.curveType = curveType;
            this.controlPointIn = controlPointIn;
            this.controlPointOut = controlPointOut;
        }

        public BezierKeyframe Clone()
        {
            // TODO: Untangle AnimationCurve struct references
            return new BezierKeyframe
            {
                time = time,
                value = value,
                curveType = curveType,
                controlPointIn = controlPointIn,
                controlPointOut = controlPointOut
            };
        }
    }
}
