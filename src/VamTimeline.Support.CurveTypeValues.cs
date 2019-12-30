using System.Collections.Generic;

namespace AcidBubbles.VamTimeline
{
    public static class CurveTypeValues
    {
        // TODO: Add LinearFlat, FlatLinear (for touching), BounceFlat, FlatBounce (for walking)
        public const string Flat = "Flat";
        public const string Linear = "Linear";
        public const string Smooth = "Smooth";
        public const string Bounce = "Bounce";

        public static readonly List<string> CurveTypes = new List<string> { Flat, Linear, Smooth, Bounce };
    }
}
