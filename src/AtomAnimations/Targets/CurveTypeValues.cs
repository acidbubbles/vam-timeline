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

        public static readonly int LeaveAsIs_ = _indexedCurveTypes.IndexOf(LeaveAsIs);
        public static readonly int Flat_ = _indexedCurveTypes.IndexOf(Flat);
        public static readonly int FlatLong_ = _indexedCurveTypes.IndexOf(FlatLong);
        public static readonly int Linear_ = _indexedCurveTypes.IndexOf(Linear);
        public static readonly int Smooth_ = _indexedCurveTypes.IndexOf(Smooth);
        public static readonly int Constant_ = _indexedCurveTypes.IndexOf(Constant);
        public static readonly int Bounce_ = _indexedCurveTypes.IndexOf(Bounce);
        public static readonly int LinearFlat_ = _indexedCurveTypes.IndexOf(LinearFlat);
        public static readonly int FlatLinear_ = _indexedCurveTypes.IndexOf(FlatLinear);
        public static readonly int CopyPrevious_ = _indexedCurveTypes.IndexOf(CopyPrevious);

        public static string FromInt(int v)
        {
            if (v < 0 || v >= _indexedCurveTypes.Count) return Smooth;
            return _indexedCurveTypes[v];
        }

        public static int ToInt(string v)
        {
            var idx = _indexedCurveTypes.IndexOf(v);
            if (idx == -1) return _indexedCurveTypes.IndexOf(Smooth);
            return idx;
        }
    }
}
