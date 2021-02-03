
using System;
using System.Collections.Generic;

namespace VamTimeline
{
    public class SyncProxy : IDisposable
    {
        public static SyncProxy Wrap(Dictionary<string, object> dict)
        {
            return new SyncProxy(dict);
        }

        public readonly Dictionary<string, object> dict;

        public bool connected
        {
            get { return Get<bool>(nameof(connected)); }
            set { Set(nameof(connected), value); }
        }

        public MVRScript storable
        {
            get { return Get<MVRScript>(nameof(storable)); }
            set { Set(nameof(storable), value); }
        }

        public string label
        {
            get
            {
                var s = storable;
                var customLabel = s.pluginLabelJSON.val;
                return !string.IsNullOrEmpty(customLabel)
                    ? $"{s.containingAtom.name}: {customLabel}"
                    : s.containingAtom.name;
            }
        }

        // TODO: Instead, get from storable and cache
        public JSONStorableStringChooser animation
        {
            get { return Get<JSONStorableStringChooser>(nameof(animation)); }
            set { Set(nameof(animation), value); }
        }

        public JSONStorableFloat time
        {
            get { return Get<JSONStorableFloat>(nameof(time)); }
            set { Set(nameof(time), value); }
        }

        public JSONStorableBool isPlaying
        {
            get { return Get<JSONStorableBool>(nameof(isPlaying)); }
            set { Set(nameof(isPlaying), value); }
        }

        public JSONStorableAction play
        {
            get { return Get<JSONStorableAction>(nameof(play)); }
            set { Set(nameof(play), value); }
        }

        public JSONStorableAction playIfNotPlaying
        {
            get { return Get<JSONStorableAction>(nameof(playIfNotPlaying)); }
            set { Set(nameof(playIfNotPlaying), value); }
        }

        public JSONStorableAction stop
        {
            get { return Get<JSONStorableAction>(nameof(stop)); }
            set { Set(nameof(stop), value); }
        }

        public JSONStorableAction stopAndReset
        {
            get { return Get<JSONStorableAction>(nameof(stopAndReset)); }
            set { Set(nameof(stopAndReset), value); }
        }

        public JSONStorableAction nextFrame
        {
            get { return Get<JSONStorableAction>(nameof(nextFrame)); }
            set { Set(nameof(nextFrame), value); }
        }

        public JSONStorableAction previousFrame
        {
            get { return Get<JSONStorableAction>(nameof(previousFrame)); }
            set { Set(nameof(previousFrame), value); }
        }

        private SyncProxy(Dictionary<string, object> dict)
        {
            this.dict = dict;
        }

        public SyncProxy()
            : this(new Dictionary<string, object>())
        {
        }

        private T Get<T>(string key)
        {
            object val;
            return dict.TryGetValue(key, out val) ? (T)val : default(T);
        }

        private void Set<T>(string key, T value)
        {
            dict[key] = value;
        }

        public void Dispose()
        {
        }
    }
}
