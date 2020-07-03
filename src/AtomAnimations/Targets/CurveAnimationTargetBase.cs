using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public abstract class CurveAnimationTargetBase : AnimationTargetBase
    {
        public SortedDictionary<int, KeyframeSettings> settings { get; } = new SortedDictionary<int, KeyframeSettings>();
        public abstract string name { get; }

        public abstract AnimationCurve GetLeadCurve();

        protected void Validate(AnimationCurve curve, float animationLength)
        {
            if (curve.length < 2)
            {
                SuperController.LogError($"Target {name} has {curve.length} frames");
                return;
            }
            if (curve[0].time != 0)
            {
                SuperController.LogError($"Target {name} has no start frame");
                return;
            }
            if (curve[curve.length - 1].time != animationLength)
            {
                SuperController.LogError($"Target {name} ends with frame {curve[curve.length - 1].time} instead of expected {animationLength}");
                return;
            }
            if (this.settings.Count > curve.length)
            {
                var curveKeys = curve.keys.Select(k => k.time.ToMilliseconds()).ToList();
                var extraneousKeys = this.settings.Keys.Except(curveKeys);
                SuperController.LogError($"Target {name} has {curve.length} frames but {this.settings.Count} settings. Attempting auto-repair.");
                foreach (var extraneousKey in extraneousKeys)
                    this.settings.Remove(extraneousKey);
            }
            if (this.settings.Count != curve.length)
            {
                SuperController.LogError($"Target {name} has {curve.length} frames but {this.settings.Count} settings");
                SuperController.LogError($"  Target  : {string.Join(", ", curve.keys.Select(k => k.time.ToString()).ToArray())}");
                SuperController.LogError($"  Settings: {string.Join(", ", this.settings.Select(k => (k.Key / 1000f).ToString()).ToArray())}");
                return;
            }
            var settings = this.settings.Select(s => s.Key);
            var keys = curve.keys.Select(k => k.time.ToMilliseconds()).ToArray();
            if (!settings.SequenceEqual(keys))
            {
                SuperController.LogError($"Target {name} has different times for settings and keyframes");
                SuperController.LogError($"Settings: {string.Join(", ", settings.Select(s => s.ToString()).ToArray())}");
                SuperController.LogError($"Keyframes: {string.Join(", ", keys.Select(k => k.ToString()).ToArray())}");
                return;
            }
        }

        protected void ReapplyCurveTypes(AnimationCurve curve, bool loop)
        {
            for (var key = 0; key < curve.length; key++)
            {
                KeyframeSettings setting;
                if (!settings.TryGetValue(curve[key].time.ToMilliseconds(), out setting))
                {
                    continue;
                }
                curve.ApplyCurveType(key, setting.curveType, loop);
            }

            if (loop && settings[0].curveType == CurveTypeValues.Smooth)
            {
                curve.SmoothLoop();
            }
        }

        public void EnsureKeyframeSettings(float time, string defaultCurveTypeValue)
        {
            var ms = time.ToMilliseconds();
            if (!settings.ContainsKey(ms))
                settings[ms] = new KeyframeSettings { curveType = defaultCurveTypeValue };
        }

        protected void AddEdgeKeyframeSettingsIfMissing(float animationLength)
        {
            if (!settings.ContainsKey(0))
                settings.Add(0, new KeyframeSettings { curveType = CurveTypeValues.Smooth });
            if (!settings.ContainsKey(animationLength.ToMilliseconds()))
                settings.Add(animationLength.ToMilliseconds(), new KeyframeSettings { curveType = CurveTypeValues.Smooth });
        }

        public void ChangeCurve(float time, string curveType, bool loop)
        {
            if (string.IsNullOrEmpty(curveType)) return;

            UpdateSetting(time, curveType, false);
            if (loop && time == 0)
            {
                var curve = GetLeadCurve();
                UpdateSetting(curve[curve.length - 1].time, curveType, false);
            }
            dirty = true;
        }

        public string GetKeyframeSettings(float time)
        {
            KeyframeSettings setting;
            return settings.TryGetValue(time.ToMilliseconds(), out setting) ? setting.curveType : null;
        }

        protected void UpdateSetting(float time, string curveType, bool create)
        {
            var ms = time.ToMilliseconds();
            if (settings.ContainsKey(ms))
                settings[ms].curveType = curveType;
            else if (create)
                settings.Add(ms, new KeyframeSettings { curveType = curveType });
        }
    }
}
