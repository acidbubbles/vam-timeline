using System.Collections.Generic;

namespace VamTimeline
{
    public static class ListExtensions
    {
        public static T AddAndRetreive<T>(this IList<T> list, T value)
        {
            list.Add(value);
            return value;
        }
    }
}
