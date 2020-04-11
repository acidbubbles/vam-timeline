using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{

    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class AtomAnimationClip
    {
        public const float DefaultAnimationLength = 2f;
        public const float DefaultBlendDuration = 0.75f;
        private IAnimationTarget _selected;
        private bool _loop = true;
        private string _nextAnimationName;

        public AnimationClip Clip { get; }
        public AnimationPattern AnimationPattern { get; set; }
        public readonly List<FloatParamAnimationTarget> TargetFloatParams = new List<FloatParamAnimationTarget>();
        public readonly List<FreeControllerAnimationTarget> TargetControllers = new List<FreeControllerAnimationTarget>();
        public IEnumerable<IAnimationTarget> AllTargets => TargetControllers.Cast<IAnimationTarget>().Concat(TargetFloatParams.Cast<IAnimationTarget>());
        public bool EnsureQuaternionContinuity { get; set; } = true;
        public string AnimationName { get; set; }
        public float AnimationLength { get; set; } = DefaultAnimationLength;
        public bool AutoPlay { get; set; } = false;
        public bool Loop
        {
            get
            {
                return _loop;
            }
            set
            {
                _loop = value;
                Clip.wrapMode = value ? WrapMode.Loop : WrapMode.Once;
            }
        }
        public float BlendDuration { get; set; } = DefaultBlendDuration;
        public string NextAnimationName
        {
            get
            {
                return _nextAnimationName;
            }
            set
            {
                _nextAnimationName = value == "" ? null : value;
            }
        }

        public float NextAnimationTime { get; set; }

        public AtomAnimationClip(string animationName)
        {
            AnimationName = animationName;
            Clip = new AnimationClip
            {
                legacy = true
            };
        }

        public bool IsEmpty()
        {
            return AllTargets.Count() == 0;
        }

        public void SelectTargetByName(string val)
        {
            _selected = string.IsNullOrEmpty(val)
                ? null
                : AllTargets.FirstOrDefault(c => c.Name == val);
        }

        public IEnumerable<string> GetTargetsNames()
        {
            return AllTargets.Select(c => c.Name).ToList();
        }

        public FreeControllerAnimationTarget Add(FreeControllerV3 controller)
        {
            if (TargetControllers.Any(c => c.Controller == controller)) return null;
            var target = new FreeControllerAnimationTarget(controller);
            TargetControllers.Add(target);
            TargetControllers.Sort(new FreeControllerAnimationTarget.Comparer());
            return target;
        }

        public FloatParamAnimationTarget Add(JSONStorable storable, JSONStorableFloat jsf)
        {
            if (TargetFloatParams.Any(s => s.Storable.name == storable.name && s.Name == jsf.name)) return null;
            var target = new FloatParamAnimationTarget(storable, jsf);
            Add(target);
            return target;
        }

        public void Add(FloatParamAnimationTarget target)
        {
            if (target == null) throw new NullReferenceException(nameof(target));
            TargetFloatParams.Add(target);
            TargetFloatParams.Sort(new FloatParamAnimationTarget.Comparer());
        }

        public void Remove(FreeControllerV3 controller)
        {
            var existing = TargetControllers.FirstOrDefault(c => c.Controller == controller);
            if (existing == null) return;
            TargetControllers.Remove(existing);
        }

        public void ChangeCurve(float time, string curveType)
        {
            foreach (var controller in GetAllOrSelectedControllerTargets())
            {
                controller.ChangeCurve(time, curveType);
            }
        }

        public float GetNextFrame(float time)
        {
            time = time.Snap();
            if (time.IsSameFrame(AnimationLength))
                return 0f;
            var nextTime = AnimationLength;
            foreach (var controller in GetAllOrSelectedTargets())
            {
                // TODO: Use bisect for more efficient navigation
                var leadCurve = controller.GetLeadCurve();
                for (var key = 0; key < leadCurve.length; key++)
                {
                    var potentialNextTime = leadCurve[key].time;
                    if (potentialNextTime <= time) continue;
                    if (potentialNextTime > nextTime) continue;
                    nextTime = potentialNextTime;
                    break;
                }
            }
            if (nextTime.IsSameFrame(AnimationLength) && Loop)
                return 0f;
            else
                return nextTime;
        }

        public float GetPreviousFrame(float time)
        {
            time = time.Snap();
            if (time.IsSameFrame(0))
                return GetAllOrSelectedTargets().Select(t => t.GetLeadCurve()).Select(c => c[c.length - (Loop ? 2 : 1)].time).Max();
            var previousTime = 0f;
            foreach (var controller in GetAllOrSelectedTargets())
            {
                // TODO: Use bisect for more efficient navigation
                var leadCurve = controller.GetLeadCurve();
                for (var key = leadCurve.length - 2; key >= 0; key--)
                {
                    var potentialPreviousTime = leadCurve[key].time;
                    if (potentialPreviousTime >= time) continue;
                    if (potentialPreviousTime < previousTime) continue;
                    previousTime = potentialPreviousTime;
                    break;
                }
            }
            return previousTime;
        }

        public void DeleteFrame(float time)
        {
            time = time.Snap();
            foreach (var target in GetAllOrSelectedTargets())
            {
                target.DeleteFrame(time);
            }
        }

        public IEnumerable<IAnimationTarget> GetAllOrSelectedTargets()
        {
            if (_selected != null) return new IAnimationTarget[] { _selected };
            return AllTargets.Cast<IAnimationTarget>();
        }

        public IEnumerable<FreeControllerAnimationTarget> GetAllOrSelectedControllerTargets()
        {
            if (_selected as FreeControllerAnimationTarget != null) return new[] { (FreeControllerAnimationTarget)_selected };
            return TargetControllers;
        }

        public IEnumerable<FloatParamAnimationTarget> GetAllOrSelectedFloatParamTargets()
        {
            if (_selected as FloatParamAnimationTarget != null) return new[] { (FloatParamAnimationTarget)_selected };
            return TargetFloatParams;
        }

        public void StretchLength(float value)
        {
            if (value == AnimationLength)
                return;
            AnimationLength = value;
            foreach (var target in AllTargets)
            {
                foreach (var curve in target.GetCurves())
                    curve.StretchLength(value);
            }
            UpdateKeyframeSettingsFromBegin();
        }

        public void CropOrExtendLengthEnd(float animationLength)
        {
            if (AnimationLength.IsSameFrame(animationLength))
                return;
            AnimationLength = animationLength;
            foreach (var target in AllTargets)
            {
                foreach (var curve in target.GetCurves())
                    curve.CropOrExtendLengthEnd(animationLength);
            }
            UpdateKeyframeSettingsFromBegin();
        }

        public void CropOrExtendLengthBegin(float animationLength)
        {
            if (AnimationLength.IsSameFrame(animationLength))
                return;
            AnimationLength = animationLength;
            foreach (var target in AllTargets)
            {
                foreach (var curve in target.GetCurves())
                    curve.CropOrExtendLengthBegin(animationLength);
            }
            UpdateKeyframeSettingsFromEnd();
        }

        public void CropOrExtendLengthAtTime(float animationLength, float time)
        {
            if (AnimationLength.IsSameFrame(animationLength))
                return;
            AnimationLength = animationLength;
            foreach (var target in AllTargets)
            {
                foreach (var curve in target.GetCurves())
                    curve.CropOrExtendLengthAtTime(animationLength, time);
            }
            UpdateKeyframeSettingsFromBegin();
        }

        private void UpdateKeyframeSettingsFromBegin()
        {
            foreach (var target in TargetControllers)
            {
                var settings = target.Settings.Values.ToList();
                target.Settings.Clear();
                var leadCurve = target.GetLeadCurve();
                for (var i = 0; i < leadCurve.length; i++)
                {
                    if (i >= settings.Count) break;
                    target.Settings.Add(leadCurve[i].time.ToMilliseconds(), settings[i]);
                }
            }
        }

        private void UpdateKeyframeSettingsFromEnd()
        {
            foreach (var target in TargetControllers)
            {
                var settings = target.Settings.Values.ToList();
                target.Settings.Clear();
                var leadCurve = target.GetLeadCurve();
                for (var i = 0; i < leadCurve.length; i++)
                {
                    if (i >= settings.Count) break;
                    target.Settings.Add(leadCurve[leadCurve.length - i - 1].time.ToMilliseconds(), settings[settings.Count - i - 1]);
                }
            }
        }

        public AtomClipboardEntry Copy(float time, bool all = false)
        {
            var controllers = new List<FreeControllerV3ClipboardEntry>();
            foreach (var target in all ? TargetControllers : GetAllOrSelectedControllerTargets())
            {
                var snapshot = target.GetCurveSnapshot(time);
                if (snapshot == null) continue;
                controllers.Add(new FreeControllerV3ClipboardEntry
                {
                    Controller = target.Controller,
                    Snapshot = snapshot
                });
            }
            var floatParams = new List<FloatParamValClipboardEntry>();
            foreach (var target in all ? TargetFloatParams : GetAllOrSelectedFloatParamTargets())
            {
                int key = target.Value.KeyframeBinarySearch(time);
                if (key == -1) continue;
                floatParams.Add(new FloatParamValClipboardEntry
                {
                    Storable = target.Storable,
                    FloatParam = target.FloatParam,
                    Snapshot = target.Value[key]
                });
            }
            return new AtomClipboardEntry
            {
                Controllers = controllers,
                FloatParams = floatParams
            };
        }

        public void Validate()
        {
            foreach (var target in TargetControllers)
            {
                var leadCurve = target.GetLeadCurve();
                if (leadCurve.length < 2)
                {
                    SuperController.LogError($"Target {target.Name} has {leadCurve.length} frames");
                    return;
                }
                if (target.Settings.Count > leadCurve.length)
                {
                    var curveKeys = leadCurve.keys.Select(k => k.time.ToMilliseconds()).ToList();
                    var extraneousKeys = target.Settings.Keys.Except(curveKeys);
                    SuperController.LogError($"Target {target.Name} has {leadCurve.length} frames but {target.Settings.Count} settings. Attempting auto-repair.");
                    foreach (var extraneousKey in extraneousKeys)
                        target.Settings.Remove(extraneousKey);
                }
                if (target.Settings.Count != leadCurve.length)
                {
                    SuperController.LogError($"Target {target.Name} has {leadCurve.length} frames but {target.Settings.Count} settings");
                    SuperController.LogError($"  Target  : {string.Join(", ", leadCurve.keys.Select(k => k.time.ToString()).ToArray())}");
                    SuperController.LogError($"  Settings: {string.Join(", ", target.Settings.Select(k => (k.Key / 1000f).ToString()).ToArray())}");
                    return;
                }
                var settings = target.Settings.Select(s => s.Key);
                var keys = leadCurve.keys.Select(k => k.time.ToMilliseconds()).ToArray();
                if (!settings.SequenceEqual(keys))
                {
                    SuperController.LogError($"Target {target.Name} has different times for settings and keyframes");
                    SuperController.LogError($"Settings: {string.Join(", ", settings.Select(s => s.ToString()).ToArray())}");
                    SuperController.LogError($"Keyframes: {string.Join(", ", keys.Select(k => k.ToString()).ToArray())}");
                    return;
                }
            }
        }

        public void Paste(float time, AtomClipboardEntry clipboard)
        {
            if (Loop && time >= AnimationLength - float.Epsilon)
                time = 0f;

            time = time.Snap();

            foreach (var entry in clipboard.Controllers)
            {
                var target = TargetControllers.FirstOrDefault(c => c.Controller == entry.Controller);
                if (target == null)
                    target = Add(entry.Controller);
                target.SetCurveSnapshot(time, entry.Snapshot);
            }
            foreach (var entry in clipboard.FloatParams)
            {
                var target = TargetFloatParams.FirstOrDefault(c => c.FloatParam == entry.FloatParam);
                if (target == null)
                    target = Add(entry.Storable, entry.FloatParam);
                target.SetKeyframe(time, entry.Snapshot.value);
            }
        }
    }
}
