using System.Runtime.CompilerServices;

namespace VamTimeline
{
    public struct BezierKeyframe
    {
        public const int NullKeyframeCurveType = -1;
        public static readonly BezierKeyframe NullKeyframe = new BezierKeyframe(0, 0, -1);

        public float time;
        public float value;
        public float controlPointIn;
        public float controlPointOut;
        public int curveType;

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

        [MethodImpl(256)]
        public bool IsNull()
        {
            return curveType == NullKeyframeCurveType;
        }

        [MethodImpl(256)]
        public bool HasValue()
        {
            return curveType != NullKeyframeCurveType;
        }

        public override string ToString()
        {
            return $"{time: 0.000}: {value:0.000} ({CurveTypeValues.FromInt(curveType)}, {controlPointIn:0.0}/{controlPointOut:0.0})";
        }
    }
}
