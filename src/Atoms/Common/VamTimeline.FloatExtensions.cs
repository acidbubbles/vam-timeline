using System;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public static class FloatExtensions
    {
        public static bool IsSameFrame(this float value, float time)
        {
            return value > time - 0.0005f && value < time + 0.0005f;
        }

        public static float Snap(this float value, float range = 0f)
        {
            value = (float)(Math.Round(value * 1000f) / 1000f);

            if (value < 0f)
                value = 0f;

            if (range > 0f)
            {
                var snapDelta = value % range;
                if (snapDelta != 0f)
                {
                    value -= snapDelta;
                    if (snapDelta > range / 2f)
                        value += range;
                }
            }

            return value;
        }

        public static int ToMilliseconds(this float value)
        {
            return (int)(Math.Round(value * 1000f));
        }
    }
}
