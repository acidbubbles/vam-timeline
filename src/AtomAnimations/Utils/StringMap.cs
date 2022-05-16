using System.Collections.Generic;

namespace VamTimeline
{
    public static class StringMap
    {
        private static int _nextId = 1;
        private static readonly Dictionary<string, int> _ids = new Dictionary<string, int>();

        public static int ToId(this string name)
        {
            if (name == null)
            {
                return -1;
            }

            int id;
            if (_ids.TryGetValue(name, out id))
                return id;

            id = _nextId;
            _nextId++;
            _ids.Add(name, id);
            return id;
        }
    }
}
