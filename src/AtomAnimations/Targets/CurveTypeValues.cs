using System.Collections.Generic;

namespace VamTimeline
{
    public static class CurveTypeValues
    {
        public const string LeaveAsIs = "Leave As-Is";
        public const string Flat = "Flat";
        public const string FlatLong = "Flat (Long)";
        public const string Linear = "Linear";
        public const string Smooth = "Smooth";
        public const string Constant = "Constant";
        public const string Bounce = "Bounce";
        public const string LinearFlat = "Linear -> Flat";
        public const string FlatLinear = "Flat -> Linear";
        public const string CopyPrevious = "Copy Previous Keyframe";

        // Used for serialization. Do not reorder.
        private static readonly List<string> _indexedCurveTypes = new List<string> { LeaveAsIs, Flat, Linear, Smooth, Bounce, LinearFlat, FlatLinear, CopyPrevious, Constant, FlatLong };

        // Used for display. Reorder as needed.
        public static readonly List<string> DisplayCurveTypes = new List<string> { LeaveAsIs, Flat, FlatLong, Linear, Smooth, Constant, Bounce, LinearFlat, FlatLinear, CopyPrevious };

        public static string FromInt(int v)
        {
            return _indexedCurveTypes[v];
        }

        public static int ToInt(string v)
        {
            return _indexedCurveTypes.IndexOf(v);
        }
    }
}
