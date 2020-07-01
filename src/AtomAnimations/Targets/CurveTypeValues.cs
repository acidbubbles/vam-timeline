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
        public const string CopyPrevious = "Copy Previous (Flat)";

        private static readonly List<string> _indexedCurveTypes = new List<string> { LeaveAsIs, Flat, Linear, Smooth, Bounce, LinearFlat, FlatLinear, CopyPrevious };
        public static readonly List<string> DisplayCurveTypes = new List<string> { LeaveAsIs, Flat, Linear, Smooth, Bounce, LinearFlat, FlatLinear, CopyPrevious };

        public static string FromInt(int v)
        {
            return _indexedCurveTypes[v];
        }

        public static int ToInt(string v)
        {
            return _indexedCurveTypes.FindIndex(c => c == v);
        }
    }
}
