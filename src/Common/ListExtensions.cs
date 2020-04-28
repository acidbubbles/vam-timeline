using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public static class ListExtensions
    {
        public static T AddAndRetreive<T>(this IList<T> list, T value)
        {
            list.Add(value);
            return value;
        }
    }
}
