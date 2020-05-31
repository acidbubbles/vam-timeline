using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class AtomAnimationClip : IAtomAnimationClip
    {
        public const float DefaultAnimationLength = 2f;
        public const float DefaultBlendDuration = 0.75f;

        private bool _loop = true;
        private string _nextAnimationName;
        private float _animationLength = DefaultAnimationLength;
        private bool _transition;
        private float _blendDuration = DefaultBlendDuration;
        private float _nextAnimationTime;
        private string _animationName;
        private bool _ensureQuaternionContinuity = true;

        public UnityEvent TargetsSelectionChanged { get; } = new UnityEvent();
        public UnityEvent TargetsListChanged { get; } = new UnityEvent();
        // TODO: We want to deprecate this (this includes everything except changing the targets list)
        public UnityEvent AnimationModified { get; } = new UnityEvent();
        public UnityEvent AnimationSettingsModified { get; } = new UnityEvent();
        public AnimationClip Clip { get; }
        public AnimationPattern AnimationPattern { get; set; }
        public readonly AtomAnimationTargetsList<FreeControllerAnimationTarget> TargetControllers = new AtomAnimationTargetsList<FreeControllerAnimationTarget>() { Label = "Controllers" };
        public readonly AtomAnimationTargetsList<FloatParamAnimationTarget> TargetFloatParams = new AtomAnimationTargetsList<FloatParamAnimationTarget>() { Label = "Float Params" };
        public IEnumerable<IAnimationTargetWithCurves> AllTargets => TargetControllers.Cast<IAnimationTargetWithCurves>().Concat(TargetFloatParams.Cast<IAnimationTargetWithCurves>());
        public bool EnsureQuaternionContinuity
        {
            get
            {
                return _ensureQuaternionContinuity;
            }
            set
            {
                _ensureQuaternionContinuity = value;
                AnimationSettingsModified.Invoke();
            }
        }
        public string AnimationName
        {
            get
            {
                return _animationName;
            }
            set
            {
                _animationName = value;
                AnimationSettingsModified.Invoke();
            }
        }
        public float AnimationLength
        {
            get
            {
                return _animationLength;
            }
            set
            {
                _animationLength = value;
                AnimationSettingsModified.Invoke();
            }
        }
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
                AnimationSettingsModified.Invoke();
            }
        }
        public bool Transition
        {
            get
            {
                return _transition;
            }
            set
            {
                _transition = value;
                AnimationSettingsModified.Invoke();
            }
        }
        public float BlendDuration
        {
            get
            {
                return _blendDuration;
            }
            set
            {
                _blendDuration = value;
                AnimationSettingsModified.Invoke();
            }
        }
        public string NextAnimationName
        {
            get
            {
                return _nextAnimationName;
            }
            set
            {
                _nextAnimationName = value == "" ? null : value;
                AnimationSettingsModified.Invoke();
            }
        }
        public float NextAnimationTime
        {
            get
            {
                return _nextAnimationTime;
            }
            set
            {
                _nextAnimationTime = value;
                AnimationSettingsModified.Invoke();
            }
        }
        public int AllTargetsCount => TargetControllers.Count + TargetFloatParams.Count;

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

        public IEnumerable<string> GetTargetsNames()
        {
            return AllTargets.Select(c => c.Name).ToList();
        }

        public FreeControllerAnimationTarget Add(FreeControllerV3 controller)
        {
            if (TargetControllers.Any(c => c.Controller == controller)) return null;
            var target = new FreeControllerAnimationTarget(controller);
            Add(target);
            return target;
        }

        public void Add(FreeControllerAnimationTarget target)
        {
            TargetControllers.Add(target);
            TargetControllers.Sort(new FreeControllerAnimationTarget.Comparer());
            target.SelectedChanged.AddListener(OnTargetSelectionChanged);
            target.AnimationCurveModified.AddListener(OnAnimationModified);
            TargetsListChanged.Invoke();
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
            target.SelectedChanged.AddListener(OnTargetSelectionChanged);
            target.AnimationCurveModified.AddListener(OnAnimationModified);
            TargetsListChanged.Invoke();
        }

        private void OnTargetSelectionChanged()
        {
            TargetsSelectionChanged.Invoke();
        }

        private void OnAnimationModified()
        {
            AnimationModified.Invoke();
        }

        public void Remove(FreeControllerV3 controller)
        {
            var target = TargetControllers.FirstOrDefault(c => c.Controller == controller);
            if (target == null) return;
            TargetControllers.Remove(target);
            target.Dispose();
            TargetsListChanged.Invoke();
        }

        public void Remove(FloatParamAnimationTarget target)
        {
            TargetFloatParams.Remove(target);
            target.Dispose();
            TargetsListChanged.Invoke();
        }

        public void ChangeCurve(float time, string curveType)
        {
            foreach (var controller in GetAllOrSelectedTargets().OfType<FreeControllerAnimationTarget>())
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
            {
                try
                {
                    return GetAllOrSelectedTargets().Select(t => t.GetLeadCurve()).Select(c => c[c.length - (Loop ? 2 : 1)].time).Max();
                }
                catch (InvalidOperationException)
                {
                    return 0f;
                }
            }
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

        public IEnumerable<IAnimationTargetWithCurves> GetAllOrSelectedTargets()
        {
            var result = AllTargets
                .Where(t => t.Selected)
                .Cast<IAnimationTargetWithCurves>()
                .ToList();
            return result.Count > 0 ? result : AllTargets;
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
                    if (i < settings.Count) target.Settings.Add(leadCurve[i].time.ToMilliseconds(), settings[i]);
                    else target.Settings.Add(leadCurve[i].time.ToMilliseconds(), new KeyframeSettings { CurveType = CurveTypeValues.CopyPrevious });
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
                    int ms = leadCurve[leadCurve.length - i - 1].time.ToMilliseconds();
                    target.Settings.Add(ms, settings[settings.Count - i - 1]);
                }
                if (!target.Settings.ContainsKey(0))
                    target.Settings.Add(0, new KeyframeSettings { CurveType = CurveTypeValues.Smooth });
            }
        }

        public AtomClipboardEntry Copy(float time, bool all = false)
        {
            var controllers = new List<FreeControllerV3ClipboardEntry>();
            foreach (var target in all ? TargetControllers : GetAllOrSelectedTargets().OfType<FreeControllerAnimationTarget>())
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
            foreach (var target in all ? TargetFloatParams : GetAllOrSelectedTargets().OfType<FloatParamAnimationTarget>())
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
                Time = time,
                Controllers = controllers,
                FloatParams = floatParams
            };
        }

        public void Validate()
        {
            foreach (var target in TargetControllers)
            {
                target.Validate();
            }
        }

        public void Paste(float time, AtomClipboardEntry clipboard, bool dirty = true)
        {
            if (Loop && time >= AnimationLength - float.Epsilon)
                time = 0f;

            time = time.Snap();

            foreach (var entry in clipboard.Controllers)
            {
                var target = TargetControllers.FirstOrDefault(c => c.Controller == entry.Controller);
                if (target == null)
                    target = Add(entry.Controller);
                target.SetCurveSnapshot(time, entry.Snapshot, dirty);
            }
            foreach (var entry in clipboard.FloatParams)
            {
                var target = TargetFloatParams.FirstOrDefault(c => c.FloatParam == entry.FloatParam);
                if (target == null)
                    target = Add(entry.Storable, entry.FloatParam);
                target.SetKeyframe(time, entry.Snapshot.value, dirty);
            }
        }

        public void EnsureQuaternionContinuityAndRecalculateSlope()
        {
            foreach (var target in TargetControllers)
            {
                UnitySpecific.EnsureQuaternionContinuityAndRecalculateSlope(
                    target.RotX,
                    target.RotY,
                    target.RotZ,
                    target.RotW);
            }
        }

        public void DirtyAll()
        {
            foreach (var s in AllTargets)
                s.Dirty = true;
        }

        public IEnumerable<IAtomAnimationTargetsList> GetTargetGroups()
        {
            yield return TargetControllers;
            yield return TargetFloatParams;
        }

        public void Dispose()
        {
            TargetsSelectionChanged.RemoveAllListeners();
            AnimationModified.RemoveAllListeners();
            AnimationSettingsModified.RemoveAllListeners();
            foreach (var target in AllTargets)
            {
                target.Dispose();
            }
        }
    }
}
