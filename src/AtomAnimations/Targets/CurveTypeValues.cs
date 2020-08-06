using System.Collections.Generic;
using System.Linq;

namespace VamTimeline
{
    public static class CurveTypeValues
    {
        public const int LeaveAsIs = 0;
        public const int Flat = 1;
        public const int FlatLong = 9;
        public const int Linear = 2;
        public const int Smooth = 3;
        public const int Constant = 8;
        public const int Bounce = 4;
        public const int LinearFlat = 5;
        public const int FlatLinear = 6;
        public const int CopyPrevious = 7;
        public const int Auto = 7;

        public static readonly List<KeyValuePair<int, string>> labels = new List<KeyValuePair<int, string>>
        {
            new KeyValuePair<int, string>(Auto, "Auto"),
            new KeyValuePair<int, string>(Smooth, "Smooth"),
            new KeyValuePair<int, string>(Linear, "Linear"),
            new KeyValuePair<int, string>(Constant, "Constant"),
            new KeyValuePair<int, string>(Flat, "Flat"),
            new KeyValuePair<int, string>(FlatLong, "Flat (Long)"),
            new KeyValuePair<int, string>(Bounce, "Bounce"),
            new KeyValuePair<int, string>(LinearFlat, "Linear -> Flat"),
            new KeyValuePair<int, string>(FlatLinear, "Flat -> Linear"),
            new KeyValuePair<int, string>(CopyPrevious, "Copy Previous Keyframe"),
            new KeyValuePair<int, string>(LeaveAsIs, "Leave As-Is"),
        };

        public static string FromInt(int v)
        {
            return labels.FirstOrDefault(l => l.Key == v)?.Value ?? "?";
        }

        public static int ToInt(string v)
        {
            return labels.FirstOrDefault(l => l.Value == v)?.Key ?? Smooth;
        }
    }
}
