using System.Collections.Generic;
using System.Linq;

namespace VamTimeline
{
    public static class DictionaryExtensions
    {
        public static T Get<T>(this Dictionary<string, object> @this, string key)
        {
            object value;
            if (!@this.TryGetValue(key, out value))
                throw new KeyNotFoundException($"Key {key} was not defined in message. First 10 keys: {string.Join(", ", @this.Keys.Take(10).ToArray())}");
            return (T)value;
        }
    }
}
