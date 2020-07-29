using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public abstract class CurveAnimationTargetBase : AnimationTargetBase
    {
        public SortedDictionary<int, KeyframeSettings> settings { get; } = new SortedDictionary<int, KeyframeSettings>();
        public abstract string name { get; }

        public abstract BezierAnimationCurve GetLeadCurve();
        public abstract IEnumerable<BezierAnimationCurve> GetCurves();

        protected void Validate(BezierAnimationCurve curve, float animationLength)
        {
            if (animationLength <= 0)
            {
                SuperController.LogError($"Target {name} has an invalid animation length of {animationLength}");
                return;
            }
            if (curve.length < 2)
            {
                SuperController.LogError($"Target {name} has {curve.length} frames");
                return;
            }
            if (curve.GetKeyframe(0).time != 0)
            {
                SuperController.LogError($"Target {name} has no start frame. Frames: {string.Join(", ", curve.keys.Select(k => k.time.ToString()).ToArray())}");
                return;
            }
            if (curve.GetKeyframe(curve.length - 1).time > animationLength)
            {
                var curveKeys = curve.keys.Select(k => k.time.ToMilliseconds()).ToList();
                var extraneousKeys = this.settings.Keys.Except(curveKeys).ToList();
                SuperController.LogError($"Target {name} has  duration of {curve.GetKeyframe(curve.length - 1).time} but the animation should be {animationLength}. Auto-repairing extraneous keys.");
                foreach (var c in GetCurves())
                    while (c.GetKeyframe(c.length - 1).time > animationLength && c.length > 2)
                        c.RemoveKey(c.length - 1);
            }
            if (this.settings.Count > curve.length)
            {
                var curveKeys = curve.keys.Select(k => k.time.ToMilliseconds()).ToList();
                var extraneousKeys = this.settings.Keys.Except(curveKeys).ToList();
                SuperController.LogError($"Target {name} has {curve.length} frames but {this.settings.Count} settings. Auto-repairing extraneous keys.");
                SuperController.LogError($"  Target  : {string.Join(", ", curve.keys.Select(k => k.time.ToString()).ToArray())}");
                SuperController.LogError($"  Settings: {string.Join(", ", this.settings.Select(k => (k.Key / 1000f).ToString()).ToArray())}");
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
            if (curve.GetKeyframe(curve.length - 1).time != animationLength)
            {
                SuperController.LogError($"Target {name} ends with frame {curve.GetKeyframe(curve.length - 1).time} instead of expected {animationLength}. Auto-repairing last frame.");
                var lastTime = curve.GetKeyframe(curve.length - 1).time;
                foreach (var c in GetCurves())
                {
                    var keyframe = c.GetKeyframe(c.length - 1);
                    if (keyframe.time == animationLength) continue;
                    keyframe.time = animationLength;
                    c.MoveKey(c.length - 1, keyframe);
                }
                if (!this.settings.ContainsKey(animationLength.ToMilliseconds()))
                {
                    var kvp = this.settings.Last();
                    this.settings.Remove(kvp.Key);
                    this.settings.Add(animationLength.ToMilliseconds(), kvp.Value);
                }
            }
        }

        protected void ReapplyCurveTypes(BezierAnimationCurve curve, bool loop)
        {
            curve.AutoComputeControlPoints();
            for (var key = 0; key < curve.length; key++)
            {
                KeyframeSettings setting;
                if (!settings.TryGetValue(curve.GetKeyframe(key).time.ToMilliseconds(), out setting))
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
            KeyframeSettings ks;
            if (!settings.TryGetValue(ms, out ks))
                settings.Add(ms, new KeyframeSettings { curveType = defaultCurveTypeValue });
            else if (ks.curveType == CurveTypeValues.CopyPrevious)
                ks.curveType = defaultCurveTypeValue;
        }

        protected void AddEdgeKeyframeSettingsIfMissing(float animationLength)
        {
            if (!settings.ContainsKey(0))
                settings.Add(0, new KeyframeSettings { curveType = CurveTypeValues.Smooth });
            var ms = animationLength.ToMilliseconds();
            if (!settings.ContainsKey(ms))
            {
                if (settings.Count == 2)
                {
                    var last = settings.Last();
                    settings.Remove(last.Key);
                    settings.Add(ms, last.Value);
                }
                else
                {
                    settings.Add(ms, new KeyframeSettings { curveType = CurveTypeValues.Smooth });
                }
            }
        }

        public void ChangeCurve(float time, string curveType, bool loop)
        {
            if (string.IsNullOrEmpty(curveType)) return;

            UpdateSetting(time, curveType, false);
            if (loop && time == 0)
            {
                var curve = GetLeadCurve();
                UpdateSetting(curve.GetKeyframe(curve.length - 1).time, curveType, false);
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
