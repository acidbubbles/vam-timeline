using System.Collections.Generic;

namespace VamTimeline
{
    public static class CurveTypeValues
    {
        public const string Flat = "Flat";
        public const string Linear = "Linear";
        public const string Smooth = "Smooth";
        public const string Bounce = "Bounce";
        public const string LinearFlat = "LinearFlat";
        public const string FlatLinear = "FlatLinear";

        public static readonly List<string> CurveTypes = new List<string> { Flat, Linear, Smooth, Bounce, LinearFlat, FlatLinear };
    }
}
