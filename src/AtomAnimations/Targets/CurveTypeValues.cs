using System.Collections.Generic;
using System.Linq;

namespace VamTimeline
{
    public static class CurveTypeValues
    {
        public const int Undefined = -1;
        public const int LeaveAsIs = 0;
        public const int Flat = 1;
        public const int FlatLong = 9;
        public const int Linear = 2;
        public const int SmoothLocal = 3;
        public const int Constant = 8;
        public const int Bounce = 4;
        public const int LinearFlat = 5;
        public const int FlatLinear = 6;
        public const int CopyPrevious = 7;
        public const int SmoothGlobal = 10;

        private static readonly Dictionary<int, string> _labelMap = new Dictionary<int, string>
        {
            {SmoothLocal, "Smooth (Local)"},
            {SmoothGlobal, "Smooth (Global)"},
            {Linear, "Linear"},
            {Constant, "Constant"},
            {Flat, "Flat"},
            {FlatLong, "Flat (Long)"},
            {Bounce, "Bounce"},
            {LinearFlat, "Linear -> Flat"},
            {FlatLinear, "Flat -> Linear"},
            {CopyPrevious, "Copy Previous Keyframe"},
            {LeaveAsIs, "Leave As-Is"},
        };

        public static readonly List<string> labels2 = new List<string>
        {
            FromInt(SmoothGlobal),
            FromInt(SmoothLocal),
            FromInt(Linear),
            FromInt(Constant),
            FromInt(Flat),
            FromInt(Bounce),
            FromInt(LinearFlat),
            FromInt(FlatLinear),
        };

        public static string FromInt(int v)
        {
            string r;
            if (_labelMap.TryGetValue(v, out r)) return r;
            return "?";
        }

        public static int ToInt(string v)
        {
            return _labelMap.FirstOrDefault(l => l.Value == v).Key;
        }
    }
}
