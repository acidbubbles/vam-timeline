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
    public class AtomAnimationClip : IAtomAnimationClip, AnimationTimelineTriggerHandler
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
        public IEnumerable<IAnimationTargetWithCurves> allCurveTargets => targetControllers.Cast<IAnimationTargetWithCurves>().Concat(targetFloatParams.Cast<IAnimationTargetWithCurves>());
        public IEnumerable<IAtomAnimationTarget> allTargets => targetTriggers.Cast<IAtomAnimationTarget>().Concat(allCurveTargets.Cast<IAtomAnimationTarget>());
        public int allTargetsCount => targetTriggers.Count + targetControllers.Count + targetFloatParams.Count;
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

        public AtomAnimationClip(string animationName, string animationLayer)
        {
            this.animationName = animationName;
            this.animationLayer = animationLayer;
        }

        public bool IsEmpty()
        {
            return allTargets.Count() == 0;
        }

        public bool IsDirty()
        {
            return allTargets.Any(t => t.dirty);
        }

        public void DirtyAll()
        {
            foreach (var s in allTargets)
                s.dirty = true;
        }

        public IEnumerable<IAtomAnimationTargetsList> GetTargetGroups()
        {
            yield return targetTriggers;
            yield return targetControllers;
            yield return targetFloatParams;
        }

        public void Validate()
        {
            foreach (var target in allTargets)
            {
                if (!target.dirty) continue;
                target.Validate(_animationLength);
            }
        }

        #region Animation State

        private float _clipTime;
        public float previousClipTime;
        public float weight;
        public bool enabled;
        public bool mainInLayer;
        public float blendRate;
        public string scheduledNextAnimationName;
        public float scheduledNextTime;

        public float clipTime
        {
            get
            {
                return _clipTime;
            }

            set
            {
                previousClipTime = _clipTime;
                _clipTime = Mathf.Abs(loop ? value % animationLength : Mathf.Min(value, animationLength));
            }
        }

        public void SetNext(string nextAnimationName, float nextTime)
        {
            scheduledNextAnimationName = nextAnimationName;
            scheduledNextTime = nextAnimationName != null ? nextTime : float.MaxValue;
        }

        public void Reset(bool resetTime)
        {
            enabled = false;
            weight = 0f;
            blendRate = 0f;
            mainInLayer = false;
            SetNext(null, 0f);
            if (resetTime) clipTime = previousClipTime = 0f;
            else clipTime = previousClipTime = clipTime.Snap();
        }

        #endregion

        #region Add/Remove Targets

        public FreeControllerAnimationTarget Add(FreeControllerV3 controller)
        {
            if (targetControllers.Any(c => c.controller == controller)) return null;
            return Add(new FreeControllerAnimationTarget(controller));
        }

        public FreeControllerAnimationTarget Add(FreeControllerAnimationTarget target)
        {
            return Add(targetControllers, new FreeControllerAnimationTarget.Comparer(), target);
        }

        public FloatParamAnimationTarget Add(JSONStorable storable, JSONStorableFloat jsf)
        {
            if (targetFloatParams.Any(s => s.storable.name == storable.name && s.name == jsf.name)) return null;
            return Add(new FloatParamAnimationTarget(storable, jsf));
        }

        public FloatParamAnimationTarget Add(FloatParamAnimationTarget target)
        {
            return Add(targetFloatParams, new FloatParamAnimationTarget.Comparer(), target);
        }

        public TriggersAnimationTarget Add(TriggersAnimationTarget target)
        {
            return Add(targetTriggers, new TriggersAnimationTarget.Comparer(), target);
        }

        private T Add<T>(AtomAnimationTargetsList<T> list, IComparer<T> comparer, T target) where T : IAtomAnimationTarget
        {
            if (target == null) throw new NullReferenceException(nameof(target));
            list.Add(target);
            list.Sort(comparer);
            target.onSelectedChanged.AddListener(OnTargetSelectionChanged);
            target.onAnimationKeyframesModified.AddListener(OnAnimationModified);
            onTargetsListChanged.Invoke();
            return target;
        }

        public void Remove(FreeControllerV3 controller)
        {
            Remove(targetControllers, targetControllers.FirstOrDefault(c => c.controller == controller));
        }

        public void Remove(JSONStorable storable, JSONStorableFloat jsf)
        {
            Remove(targetFloatParams, targetFloatParams.FirstOrDefault(c => c.storable == storable && c.floatParam == jsf));
        }

        public void Remove(TriggersAnimationTarget target)
        {
            Remove(targetTriggers, target);
        }

        private void Remove<T>(List<T> list, T target) where T : IAtomAnimationTarget
        {
            if (target == null) return;
            list.Remove(target);
            target.Dispose();
            onTargetsListChanged.Invoke();
        }

        #endregion

        #region Event callbacks

        private void OnTargetSelectionChanged()
        {
            onTargetsSelectionChanged.Invoke();
        }

        private void OnAnimationModified()
        {
            onAnimationKeyframesModified.Invoke();
        }

        #endregion

        #region Frame Nav

        public float GetNextFrame(float time)
        {
            time = time.Snap();
            if (time.IsSameFrame(animationLength))
                return 0f;
            var nextTime = animationLength;
            foreach (var controller in GetAllOrSelectedTargets())
            {
                // TODO: Use bisect for more efficient navigation
                var keyframes = controller.GetAllKeyframesTime();
                for (var key = 0; key < keyframes.Length; key++)
                {
                    var potentialNextTime = keyframes[key];
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
                    return GetAllOrSelectedTargets().Select(t => t.GetAllKeyframesTime()).Select(c => c[c.Length - (loop ? 2 : 1)]).Max();
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
                var keyframes = controller.GetAllKeyframesTime();
                for (var key = keyframes.Length - 2; key >= 0; key--)
                {
                    var potentialPreviousTime = keyframes[key];
                    if (potentialPreviousTime >= time) continue;
                    if (potentialPreviousTime < previousTime) continue;
                    previousTime = potentialPreviousTime;
                    break;
                }
            }
            return previousTime;
        }

        #endregion

        public IEnumerable<IAtomAnimationTarget> GetAllOrSelectedTargets()
        {
            var result = allTargets
                .Where(t => t.selected)
                .ToList();
            return result.Count > 0 ? result : allTargets;
        }

        public IEnumerable<IAtomAnimationTarget> GetSelectedTargets()
        {
            return allTargets
                .Where(t => t.selected)
                .ToList();
        }

        #region Clipboard

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
                target.SetKeyframe(time, null);
                throw new NotImplementedException();
            }
        }

        #endregion

        private void UpdateForcedNextAnimationTime()
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
            onTargetsListChanged.RemoveAllListeners();
            foreach (var target in allTargets)
            {
                target.Dispose();
            }
        }

        #region AnimationTimelineTriggerHandler

        float AnimationTimelineTriggerHandler.GetCurrentTimeCounter()
        {
            return clipTime;
        }

        float AnimationTimelineTriggerHandler.GetTotalTime()
        {
            return animationLength;
        }

        #endregion
    }
}
