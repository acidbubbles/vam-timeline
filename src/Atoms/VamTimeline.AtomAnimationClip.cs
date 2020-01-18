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

        public void SmoothAllFrames()
        {
            foreach (var controller in TargetControllers)
            {
                controller.SmoothAllFrames();
            }
        }

        public float GetNextFrame(float time)
        {
            if (time == AnimationLength)
                return 0f;
            var nextTime = AnimationLength;
            foreach (var controller in GetAllOrSelectedTargets())
            {
                var targetNextTime = controller.GetCurves().First().keys.FirstOrDefault(k => k.time > time).time;
                if (targetNextTime != 0 && targetNextTime < nextTime) nextTime = targetNextTime;
            }
            if (nextTime == AnimationLength && Loop)
                return 0f;
            else
                return nextTime;
        }

        public float GetPreviousFrame(float time)
        {
            if (time == 0f)
                return GetAllOrSelectedTargets().SelectMany(c => c.GetCurves()).SelectMany(c => c.keys).Select(c => c.time).Where(t => !Loop || t != AnimationLength).Max();
            var previousTime = 0f;
            foreach (var controller in GetAllOrSelectedTargets())
            {
                var targetPreviousTime = controller.GetCurves().First().keys.LastOrDefault(k => k.time < time).time;
                if (targetPreviousTime != 0 && targetPreviousTime > previousTime) previousTime = targetPreviousTime;
            }
            return previousTime;
        }

        public void DeleteFrame(float time)
        {
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
            if (animationLength == AnimationLength)
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
            if (animationLength == AnimationLength)
                return;
            AnimationLength = animationLength;
            foreach (var target in AllTargets)
            {
                foreach (var curve in target.GetCurves())
                    curve.CropOrExtendLengthBegin(animationLength);
            }
            UpdateKeyframeSettingsFromEnd();
        }

        private void UpdateKeyframeSettingsFromBegin()
        {
            foreach (var target in TargetControllers)
            {
                var settings = target.Settings.Values.ToList();
                target.Settings.Clear();
                for (var i = 0; i < target.X.keys.Length; i++)
                {
                    if (i >= settings.Count) break;
                    target.Settings.Add(target.X.keys[i].time, settings[i]);
                }
            }
        }

        private void UpdateKeyframeSettingsFromEnd()
        {
            foreach (var target in TargetControllers)
            {
                var settings = target.Settings.Values.ToList();
                target.Settings.Clear();
                for (var i = 0; i < target.X.keys.Length; i++)
                {
                    if (i >= settings.Count) break;
                    target.Settings.Add(target.X.keys[target.X.keys.Length - i - 1].time, settings[settings.Count - i - 1]);
                }
            }
        }

        public AtomClipboardEntry Copy(float time)
        {
            var controllers = new List<FreeControllerV3ClipboardEntry>();
            foreach (var target in GetAllOrSelectedControllerTargets())
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
            foreach (var target in GetAllOrSelectedFloatParamTargets())
            {
                if (!target.Value.keys.Any(k => k.time == time)) continue;
                floatParams.Add(new FloatParamValClipboardEntry
                {
                    Storable = target.Storable,
                    FloatParam = target.FloatParam,
                    Snapshot = target.Value.keys.First(k => k.time == time)
                });
            }
            return new AtomClipboardEntry
            {
                Controllers = controllers,
                FloatParams = floatParams
            };
        }

        public void Paste(float time, AtomClipboardEntry clipboard)
        {
            if (Loop && time >= AnimationLength)
                time = 0f;
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
