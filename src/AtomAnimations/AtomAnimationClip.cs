using System;
using System.Collections.Generic;
using System.Linq;
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
        public class AnimationSettingModifiedEvent : UnityEvent<string> { }

        public const float DefaultAnimationLength = 2f;
        public const float DefaultBlendDuration = 0.75f;
        public const string DefaultAnimationLayer = "Layer 1";

        private bool _loop = true;
        private string _nextAnimationName;
        private float _animationLength = DefaultAnimationLength;
        private bool _transition;
        private float _blendDuration = DefaultBlendDuration;
        private float _nextAnimationTime;
        private string _animationName;
        private string _animationLayer;
        private bool _ensureQuaternionContinuity = true;
        private bool _skipNextAnimationSettingsModified;
        private AnimationPattern _animationPattern;

        public UnityEvent onTargetsSelectionChanged { get; } = new UnityEvent();
        public UnityEvent onTargetsListChanged { get; } = new UnityEvent();
        public UnityEvent onAnimationKeyframesModified { get; } = new UnityEvent();
        public AnimationSettingModifiedEvent onAnimationSettingsModified { get; } = new AnimationSettingModifiedEvent();
        public AnimationPattern animationPattern
        {
            get
            {
                return _animationPattern;
            }
            set
            {
                _animationPattern = value;
                onAnimationSettingsModified.Invoke(nameof(animationPattern));
            }
        }
        public readonly AtomAnimationTargetsList<TriggersAnimationTarget> targetTriggers = new AtomAnimationTargetsList<TriggersAnimationTarget>() { label = "Triggers" };
        public readonly AtomAnimationTargetsList<FreeControllerAnimationTarget> targetControllers = new AtomAnimationTargetsList<FreeControllerAnimationTarget>() { label = "Controllers" };
        public readonly AtomAnimationTargetsList<FloatParamAnimationTarget> targetFloatParams = new AtomAnimationTargetsList<FloatParamAnimationTarget>() { label = "Float Params" };
        // TODO: This and selection is bound to curves. Make two separate methods or just filter when using?
        public IEnumerable<IAnimationTargetWithCurves> allCurveTargets => targetControllers.Cast<IAnimationTargetWithCurves>().Concat(targetFloatParams.Cast<IAnimationTargetWithCurves>());
        public string animationLayer
        {
            get
            {
                return _animationLayer;
            }
            set
            {
                if (_animationLayer == value) return;
                _animationLayer = value;
                onAnimationSettingsModified.Invoke(nameof(animationLayer));
            }
        }
        public bool ensureQuaternionContinuity
        {
            get
            {
                return _ensureQuaternionContinuity;
            }
            set
            {
                if (_ensureQuaternionContinuity == value) return;
                _ensureQuaternionContinuity = value;
                onAnimationSettingsModified.Invoke(nameof(ensureQuaternionContinuity));
            }
        }
        public string animationName
        {
            get
            {
                return _animationName;
            }
            set
            {
                if (_animationName == value) return;
                _animationName = value;
                onAnimationSettingsModified.Invoke(nameof(animationName));
            }
        }
        public float animationLength
        {
            get
            {
                return _animationLength;
            }
            set
            {
                if (_animationLength == value) return;
                _animationLength = value;
                UpdateForcedNextAnimationTime();
                onAnimationSettingsModified.Invoke(nameof(animationLength));
            }
        }
        public bool autoPlay { get; set; } = false;
        public bool loop
        {
            get
            {
                return _loop;
            }
            set
            {
                if (_loop == value) return;
                _loop = value;
                _skipNextAnimationSettingsModified = true;
                try
                {
                    if (value)
                    {
                        foreach (var target in targetControllers)
                        {
                            if (target.settings.Count == 2)
                                target.settings[animationLength.ToMilliseconds()].curveType = CurveTypeValues.LeaveAsIs;
                        }
                        transition = false;
                    }
                    else
                    {
                        foreach (var target in targetControllers)
                        {
                            if (target.settings.Count == 2)
                                target.settings[animationLength.ToMilliseconds()].curveType = CurveTypeValues.CopyPrevious;
                        }
                    }
                }
                finally
                {
                    _skipNextAnimationSettingsModified = false;
                }
                UpdateForcedNextAnimationTime();
                if (!_skipNextAnimationSettingsModified) onAnimationSettingsModified.Invoke(nameof(loop));
                DirtyAll();
            }
        }
        public bool transition
        {
            get
            {
                return _transition;
            }
            set
            {
                if (_transition == value) return;
                _transition = value;
                _skipNextAnimationSettingsModified = true;
                try
                {
                    if (loop) loop = false;
                }
                finally
                {
                    _skipNextAnimationSettingsModified = false;
                }
                if (!_skipNextAnimationSettingsModified) onAnimationSettingsModified.Invoke(nameof(transition));
                DirtyAll();
            }
        }
        public float blendDuration
        {
            get
            {
                return _blendDuration;
            }
            set
            {
                if (_blendDuration == value) return;
                _blendDuration = value;
                UpdateForcedNextAnimationTime();
                onAnimationSettingsModified.Invoke(nameof(blendDuration));
            }
        }
        public string nextAnimationName
        {
            get
            {
                return _nextAnimationName;
            }
            set
            {
                if (_nextAnimationName == value) return;
                _nextAnimationName = value == "" ? null : value;
                UpdateForcedNextAnimationTime();
                onAnimationSettingsModified.Invoke(nameof(nextAnimationName));
            }
        }
        public float nextAnimationTime
        {
            get
            {
                return _nextAnimationTime;
            }
            set
            {
                if (_nextAnimationTime == value) return;
                _nextAnimationTime = value;
                if (!_skipNextAnimationSettingsModified) onAnimationSettingsModified.Invoke(nameof(nextAnimationTime));
            }
        }
        public int allTargetsCount => targetControllers.Count + targetFloatParams.Count;

        public AtomAnimationClip(string animationName, string animationLayer)
        {
            this.animationName = animationName;
            this.animationLayer = animationLayer;
        }

        public bool IsEmpty()
        {
            return allCurveTargets.Count() == 0;
        }

        public IEnumerable<string> GetTargetsNames()
        {
            return allCurveTargets.Select(c => c.name).ToList();
        }

        public FreeControllerAnimationTarget Add(FreeControllerV3 controller)
        {
            if (targetControllers.Any(c => c.controller == controller)) return null;
            return Add(new FreeControllerAnimationTarget(controller));
        }

        public FreeControllerAnimationTarget Add(FreeControllerAnimationTarget target)
        {
            targetControllers.Add(target);
            targetControllers.Sort(new FreeControllerAnimationTarget.Comparer());
            target.onSelectedChanged.AddListener(OnTargetSelectionChanged);
            target.onAnimationKeyframesModified.AddListener(OnAnimationModified);
            onTargetsListChanged.Invoke();
            return target;
        }

        public FloatParamAnimationTarget Add(JSONStorable storable, JSONStorableFloat jsf)
        {
            if (targetFloatParams.Any(s => s.storable.name == storable.name && s.name == jsf.name)) return null;
            return Add(new FloatParamAnimationTarget(storable, jsf));
        }

        public FloatParamAnimationTarget Add(FloatParamAnimationTarget target)
        {
            if (target == null) throw new NullReferenceException(nameof(target));
            targetFloatParams.Add(target);
            targetFloatParams.Sort(new FloatParamAnimationTarget.Comparer());
            target.onSelectedChanged.AddListener(OnTargetSelectionChanged);
            target.onAnimationKeyframesModified.AddListener(OnAnimationModified);
            onTargetsListChanged.Invoke();
            return target;
        }

        public TriggersAnimationTarget Add(TriggersAnimationTarget target)
        {
            if (target == null) throw new NullReferenceException(nameof(target));
            targetTriggers.Add(target);
            targetTriggers.Sort(new TriggersAnimationTarget.Comparer());
            target.onSelectedChanged.AddListener(OnTargetSelectionChanged);
            target.onAnimationKeyframesModified.AddListener(OnAnimationModified);
            onTargetsListChanged.Invoke();
            return target;
        }

        private void OnTargetSelectionChanged()
        {
            onTargetsSelectionChanged.Invoke();
        }

        private void OnAnimationModified()
        {
            onAnimationKeyframesModified.Invoke();
        }

        public void Remove(FreeControllerV3 controller)
        {
            var target = targetControllers.FirstOrDefault(c => c.controller == controller);
            if (target == null) return;
            targetControllers.Remove(target);
            target.Dispose();
            onTargetsListChanged.Invoke();
        }

        public void Remove(JSONStorable storable, JSONStorableFloat jsf)
        {
            var target = targetFloatParams.FirstOrDefault(c => c.storable == storable && c.floatParam == jsf);
            if (target == null) return;
            targetFloatParams.Remove(target);
            target.Dispose();
            onTargetsListChanged.Invoke();
        }

        public void Remove(TriggersAnimationTarget target)
        {
            if (target == null) return;
            targetTriggers.Remove(target);
            target.Dispose();
            onTargetsListChanged.Invoke();
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
            if (time.IsSameFrame(animationLength))
                return 0f;
            var nextTime = animationLength;
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
            if (nextTime.IsSameFrame(animationLength) && loop)
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
                    return GetAllOrSelectedTargets().Select(t => t.GetLeadCurve()).Select(c => c[c.length - (loop ? 2 : 1)].time).Max();
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

        public bool IsDirty()
        {
            return allCurveTargets.Any(t => t.dirty);
        }

        public void Validate()
        {
            foreach (var target in targetControllers)
            {
                if (!target.dirty) continue;
                target.Validate();
            }
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
            var result = allCurveTargets
                .Where(t => t.selected)
                .Cast<IAnimationTargetWithCurves>()
                .ToList();
            return result.Count > 0 ? result : allCurveTargets;
        }

        public IEnumerable<IAnimationTargetWithCurves> GetSelectedTargets()
        {
            return allCurveTargets
                .Where(t => t.selected)
                .Cast<IAnimationTargetWithCurves>()
                .ToList();
        }

        public void StretchLength(float value)
        {
            if (value == animationLength)
                return;
            animationLength = value;
            foreach (var target in allCurveTargets)
            {
                foreach (var curve in target.GetCurves())
                    curve.StretchLength(value);
            }
            UpdateKeyframeSettingsFromBegin();
        }

        public void CropOrExtendLengthEnd(float animationLength)
        {
            if (this.animationLength.IsSameFrame(animationLength))
                return;
            this.animationLength = animationLength;
            foreach (var target in allCurveTargets)
            {
                foreach (var curve in target.GetCurves())
                    curve.CropOrExtendLengthEnd(animationLength);
            }
            UpdateKeyframeSettingsFromBegin();
        }

        public void CropOrExtendLengthBegin(float animationLength)
        {
            if (this.animationLength.IsSameFrame(animationLength))
                return;
            this.animationLength = animationLength;
            foreach (var target in allCurveTargets)
            {
                foreach (var curve in target.GetCurves())
                    curve.CropOrExtendLengthBegin(animationLength);
            }
            UpdateKeyframeSettingsFromEnd();
        }

        public void CropOrExtendLengthAtTime(float animationLength, float time)
        {
            if (this.animationLength.IsSameFrame(animationLength))
                return;
            this.animationLength = animationLength;
            foreach (var target in allCurveTargets)
            {
                foreach (var curve in target.GetCurves())
                    curve.CropOrExtendLengthAtTime(animationLength, time);
            }
            UpdateKeyframeSettingsFromBegin();
        }

        private void UpdateKeyframeSettingsFromBegin()
        {
            foreach (var target in targetControllers)
            {
                var settings = target.settings.Values.ToList();
                target.settings.Clear();
                var leadCurve = target.GetLeadCurve();
                for (var i = 0; i < leadCurve.length; i++)
                {
                    if (i < settings.Count) target.settings.Add(leadCurve[i].time.ToMilliseconds(), settings[i]);
                    else target.settings.Add(leadCurve[i].time.ToMilliseconds(), new KeyframeSettings { curveType = CurveTypeValues.CopyPrevious });
                }
            }
        }

        private void UpdateKeyframeSettingsFromEnd()
        {
            foreach (var target in targetControllers)
            {
                var settings = target.settings.Values.ToList();
                target.settings.Clear();
                var leadCurve = target.GetLeadCurve();
                for (var i = 0; i < leadCurve.length; i++)
                {
                    if (i >= settings.Count) break;
                    int ms = leadCurve[leadCurve.length - i - 1].time.ToMilliseconds();
                    target.settings.Add(ms, settings[settings.Count - i - 1]);
                }
                if (!target.settings.ContainsKey(0))
                    target.settings.Add(0, new KeyframeSettings { curveType = CurveTypeValues.Smooth });
            }
        }

        public AtomClipboardEntry Copy(float time, bool all = false)
        {
            var controllers = new List<FreeControllerV3ClipboardEntry>();
            foreach (var target in all ? targetControllers : GetAllOrSelectedTargets().OfType<FreeControllerAnimationTarget>())
            {
                var snapshot = target.GetCurveSnapshot(time);
                if (snapshot == null) continue;
                controllers.Add(new FreeControllerV3ClipboardEntry
                {
                    controller = target.controller,
                    snapshot = snapshot
                });
            }
            var floatParams = new List<FloatParamValClipboardEntry>();
            foreach (var target in all ? targetFloatParams : GetAllOrSelectedTargets().OfType<FloatParamAnimationTarget>())
            {
                int key = target.value.KeyframeBinarySearch(time);
                if (key == -1) continue;
                floatParams.Add(new FloatParamValClipboardEntry
                {
                    storable = target.storable,
                    floatParam = target.floatParam,
                    snapshot = target.value[key]
                });
            }
            var triggers = new List<TriggersClipboardEntry>();
            foreach (var target in all ? targetTriggers : GetAllOrSelectedTargets().OfType<TriggersAnimationTarget>())
            {
                // TODO: Put something in this!
                triggers.Add(new TriggersClipboardEntry());
            }
            return new AtomClipboardEntry
            {
                time = time,
                controllers = controllers,
                floatParams = floatParams,
                triggers = triggers
            };
        }

        public void Paste(float time, AtomClipboardEntry clipboard, bool dirty = true)
        {
            if (loop && time >= animationLength - float.Epsilon)
                time = 0f;

            time = time.Snap();

            foreach (var entry in clipboard.controllers)
            {
                var target = targetControllers.FirstOrDefault(c => c.controller == entry.controller);
                if (target == null)
                    target = Add(entry.controller);
                target.SetCurveSnapshot(time, entry.snapshot, dirty);
            }
            foreach (var entry in clipboard.floatParams)
            {
                var target = targetFloatParams.FirstOrDefault(c => c.floatParam == entry.floatParam);
                if (target == null)
                    target = Add(entry.storable, entry.floatParam);
                target.SetKeyframe(time, entry.snapshot.value, dirty);
            }
            foreach (var entry in clipboard.triggers)
            {
                // TODO: Always paste in the first? Makes sense as long as we only support a single triggers track
                var target = targetTriggers.FirstOrDefault();
                if (target == null)
                    target = Add(new TriggersAnimationTarget());
                // TODO: Actually paste something
                target.SetKeyframe(time, true);
            }
        }

        public void DirtyAll()
        {
            foreach (var s in allCurveTargets)
                s.dirty = true;
        }

        public IEnumerable<IAtomAnimationTargetsList> GetTargetGroups()
        {
            yield return targetTriggers;
            yield return targetControllers;
            yield return targetFloatParams;
        }

        public void UpdateForcedNextAnimationTime()
        {
            _skipNextAnimationSettingsModified = true;
            try
            {
                if (loop) return;
                if (nextAnimationName == null)
                {
                    nextAnimationTime = 0;
                }
                nextAnimationTime = (animationLength - blendDuration).Snap();
            }
            finally
            {
                _skipNextAnimationSettingsModified = false;
            }
        }

        public void Dispose()
        {
            onTargetsSelectionChanged.RemoveAllListeners();
            onAnimationKeyframesModified.RemoveAllListeners();
            onAnimationSettingsModified.RemoveAllListeners();
            foreach (var target in allCurveTargets)
            {
                target.Dispose();
            }
        }
    }
}
