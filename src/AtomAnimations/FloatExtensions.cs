using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace VamTimeline
{
    public static class FloatExtensions
    {
        [MethodImpl(256)]
        public static bool IsSameFrame(this float value, float time)
        {
            return value > time - 0.0005f && value < time + 0.0005f;
        }

        [MethodImpl(256)]
        public static float Snap(this float value, float range = 0f)
        {
            value = (float)(Math.Round(value * 1000f) / 1000f);

            if (value < 0f)
                value = 0f;

            if (range > 0f)
            {
                var snapDelta = Mathf.Repeat(value, range);
                if (snapDelta != 0f)
                {
                    value -= snapDelta;
                    if (snapDelta > range / 2f)
                        value += range;
                }
            }

            return value;
        }

        [MethodImpl(256)]
        public static int ToMilliseconds(this float value)
        {
            return (int)(Math.Round(value * 1000f));
        }

        [MethodImpl(256)]
        public static float ExponentialScale(this float value, float midValue, float maxValue)
        {
            var m = maxValue / midValue;
            var c = Mathf.Log(Mathf.Pow(m - 1, 2));
            var b = maxValue / (Mathf.Exp(c) - 1);
            var a = -1 * b;
            return a + b * Mathf.Exp(c * value);
        }
    }
}
