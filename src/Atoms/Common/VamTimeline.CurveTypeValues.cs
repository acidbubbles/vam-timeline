using System.Collections.Generic;

namespace VamTimeline
{
    public static class CurveTypeValues
    {
        public const string LeaveAsIs = "Leave As-Is";
        public const string Flat = "Flat";
        public const string Linear = "Linear";
        public const string Smooth = "Smooth";
        public const string Bounce = "Bounce";
        public const string LinearFlat = "Linear -> Flat";
        public const string FlatLinear = "Flat -> Linear";

        public static readonly List<string> CurveTypes = new List<string> { LeaveAsIs, Flat, Linear, Smooth, Bounce, LinearFlat, FlatLinear };

        public static string FromInt(int v)
        {
            return CurveTypes[v];
        }

        public static int ToInt(string v)
        {
            return CurveTypes.FindIndex(c => c == v);
        }
    }
}
