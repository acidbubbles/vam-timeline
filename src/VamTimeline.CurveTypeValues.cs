using System.Collections.Generic;

namespace AcidBubbles.VamTimeline
{
    public static class CurveTypeValues
    {
        public const string Flat = "Flat";
        public const string Linear = "Linear";
        public const string Smooth = "Smooth";
        public const string Bounce = "Bounce";

        public static readonly List<string> CurveTypes = new List<string> { Flat, Linear, Smooth, Bounce };
    }
}
