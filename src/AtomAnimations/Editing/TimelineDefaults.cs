using System;
using MVR.FileManagementSecure;
using SimpleJSON;
using UnityEngine.Events;

namespace VamTimeline
{
    public class TimelineDefaults
    {
        private const string TimelineDirectory = "Saves\\PluginData\\Timeline";
        public const string DefaultsPath = "Saves\\PluginData\\Timeline\\settings.json";

        public static readonly TimelineDefaults singleton = new TimelineDefaults();

        public bool Exists()
        {
            return FileManagerSecure.FileExists(DefaultsPath);
        }

        public void Save()
        {
            FileManagerSecure.CreateDirectory(TimelineDirectory);
            SuperController.singleton.SaveJSON(GetJSON(), DefaultsPath);
        }

        public JSONClass GetJSON()
        {
            var json = new JSONClass();
            var screens = new JSONClass();
            json["Screens"] = screens;

            screens[RecordScreen.ScreenName] = GetJSON(RecordScreenSettings.singleton);
            screens[ReduceScreen.ScreenName] = GetJSON(ReduceScreenSettings.singleton);

            return json;
        }

        private static JSONClass GetJSON(TimelineSettings settings)
        {
            var obj = new JSONClass();
            settings.Save(obj);
            return obj;
        }

        public void Load()
        {
            if (!Exists()) return;
            var json = SuperController.singleton.LoadJSON(DefaultsPath).AsObject;
            if (json == null) return;

            if (json.HasKey("Screens"))
            {
                var screens = json["Screens"];
                Load(RecordScreenSettings.singleton, screens[RecordScreen.ScreenName]);
                Load(ReduceScreenSettings.singleton, screens[ReduceScreen.ScreenName]);
            }
        }

        private static void Load(TimelineSettings settings, JSONNode node)
        {
            var obj = node.AsObject;
            if (obj == null) return;
            settings.Load(obj);
        }

        public void Delete()
        {
            FileManagerSecure.DeleteFile(DefaultsPath);
        }
    }

    public abstract class TimelineSettings
    {
        public abstract void Load(JSONClass json);
        public abstract void Save(JSONClass json);
    }

    public class TimelineSetting<T> where T : struct
    {
        public string key { get; }
        public T defaultValue { get; }
        public T value { get; set; }

        public TimelineSetting(string key, T defaultValue)
        {
            this.key = key;
            this.defaultValue = defaultValue;
            value = defaultValue;
        }

        public void Load(JSONClass json)
        {
            if (!json.HasKey(key)) return;
            var entry = json[key];
            if (typeof(T) == typeof(int))
                value = (T)(object)entry.AsInt;
            else if (typeof(T) == typeof(bool))
                value = (T)(object)entry.AsBool;
            else if (typeof(T) == typeof(float))
                value = (T)(object)entry.AsFloat;
            else
                throw new NotSupportedException($"Type {value.GetType()} is not supported");
        }

        public void Save(JSONClass json)
        {
            if (typeof(T) == typeof(int))
                json[key].AsInt = (int)(object)value;
            else if (typeof(T) == typeof(bool))
                json[key].AsBool = (bool)(object)value;
            else if (typeof(T) == typeof(float))
                json[key].AsFloat = (float)(object)value;
            else
                throw new NotSupportedException($"Type {value.GetType()} is not supported");
        }
    }
}
