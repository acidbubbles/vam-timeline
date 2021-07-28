using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace VamTimeline
{
    public class AtomAnimationClip : IAtomAnimationClip
    {
        public class AnimationSettingModifiedEvent : UnityEvent<string> { }

        public const float DefaultAnimationLength = 2f;
        public const float DefaultBlendDuration = 0.75f;
        public const string DefaultAnimationLayer = "Main Layer";

        private bool _loop = true;
        private string _nextAnimationName;
        private float _animationLength = DefaultAnimationLength;
        private bool _autoTransitionPrevious;
        private bool _autoTransitionNext;
        private bool _preserveLoops = true;
        private float _blendDuration = DefaultBlendDuration;
        private float _nextAnimationTime;
        private float _nextAnimationTimeRandomize;
        private string _animationName;
        private string _animationLayer;
        private bool _ensureQuaternionContinuity = true;
        private bool _skipNextAnimationSettingsModified;
        private AnimationPattern _animationPattern;
        private AtomPose _pose;
        private bool _applyPoseOnTransition;

        public UnityEvent onTargetsListChanged { get; } = new UnityEvent();
        public UnityEvent onAnimationKeyframesDirty { get; } = new UnityEvent();
        public UnityEvent onAnimationKeyframesRebuilt { get; } = new UnityEvent();
        public UnityEvent onPlaybackSettingsChanged { get; } = new UnityEvent();
        public AnimationSettingModifiedEvent onAnimationSettingsChanged { get; } = new AnimationSettingModifiedEvent();
        public AnimationPattern animationPattern
        {
            get
            {
                return _animationPattern;
            }
            set
            {
                _animationPattern = value;
                onAnimationSettingsChanged.Invoke(nameof(animationPattern));
            }
        }
        public readonly AtomAnimationTargetsList<TriggersTrackAnimationTarget> targetTriggers = new AtomAnimationTargetsList<TriggersTrackAnimationTarget> { label = "Triggers" };
        public readonly AtomAnimationTargetsList<FreeControllerV3AnimationTarget> targetControllers = new AtomAnimationTargetsList<FreeControllerV3AnimationTarget> { label = "Controllers" };
        public readonly AtomAnimationTargetsList<JSONStorableFloatAnimationTarget> targetFloatParams = new AtomAnimationTargetsList<JSONStorableFloatAnimationTarget> { label = "Float Params" };

        public IEnumerable<ICurveAnimationTarget> GetAllCurveTargets()
        {
            foreach (var t in targetControllers)
                yield return t;
            foreach (var t in targetFloatParams)
                yield return t;
        }

        public IEnumerable<IAtomAnimationTarget> GetAllTargets()
        {
            foreach (var t in targetTriggers)
                yield return t;
            foreach (var t in targetControllers)
                yield return t;
            foreach (var t in targetFloatParams)
                yield return t;
        }

        public int GetAllTargetsCount()
        {
            return targetTriggers.Count + targetControllers.Count + targetFloatParams.Count;
        }

        public string animationNameQualified { get; private set; }
        private void UpdateAnimationNameQualified() => animationNameQualified = $"{_animationLayer}::{_animationName}";

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
                UpdateAnimationNameQualified();
                onAnimationSettingsChanged.Invoke(nameof(animationLayer));
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
                onAnimationSettingsChanged.Invoke(nameof(ensureQuaternionContinuity));
            }
        }
        public string animationNameGroup { get; private set; }
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
                var idxOfGroupSeparator = _animationName.IndexOf('/');
                if (idxOfGroupSeparator > -1)
                {
                    animationNameGroup = _animationName.Substring(0, idxOfGroupSeparator);
                }
                UpdateAnimationNameQualified();
                onAnimationSettingsChanged.Invoke(nameof(animationName));
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
                onAnimationSettingsChanged.Invoke(nameof(animationLength));
            }
        }
        public bool autoPlay { get; set; }
        public bool uninterruptible { get; set; }
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
                foreach (var curve in GetAllCurveTargets().SelectMany(t => t.GetCurves()))
                {
                    curve.loop = value;
                }

                try
                {
                    // ReSharper disable RedundantEnumerableCastCall
                    foreach (var target in targetControllers.Cast<ICurveAnimationTarget>().Concat(targetFloatParams.Cast<ICurveAnimationTarget>()))
                    // ReSharper restore RedundantEnumerableCastCall
                    {
                        foreach (var curve in target.GetCurves())
                        {
                            if (curve.length != 2) continue;
                            var keyframe = curve.GetLastFrame();
                            keyframe.curveType = value ? curve.GetFirstFrame().curveType : CurveTypeValues.CopyPrevious;
                            curve.SetLastFrame(keyframe);
                        }
                    }

                    if (value)
                        autoTransitionNext = false;
                }
                finally
                {
                    _skipNextAnimationSettingsModified = false;
                }

                UpdateForcedNextAnimationTime();
                if (!_skipNextAnimationSettingsModified) onAnimationSettingsChanged.Invoke(nameof(loop));
                DirtyAll();
            }
        }

        public bool autoTransitionPrevious
        {
            get
            {
                return _autoTransitionPrevious;
            }
            set
            {
                if (_autoTransitionPrevious == value) return;
                _autoTransitionPrevious = value;
                onAnimationSettingsChanged.Invoke(nameof(autoTransitionPrevious));
                DirtyAll();
            }
        }

        public bool autoTransitionNext
        {
            get
            {
                return _autoTransitionNext;
            }
            set
            {
                if (_autoTransitionNext == value) return;
                _autoTransitionNext = value;
                _skipNextAnimationSettingsModified = true;
                try
                {
                    if (loop) loop = false;
                }
                finally
                {
                    _skipNextAnimationSettingsModified = false;
                }
                if (!_skipNextAnimationSettingsModified) onAnimationSettingsChanged.Invoke(nameof(autoTransitionNext));
                DirtyAll();
            }
        }

        public bool preserveLoops
        {
            get
            {
                return _preserveLoops;
            }
            set
            {
                if (_preserveLoops == value) return;
                _preserveLoops = value;
                if (!_skipNextAnimationSettingsModified) onAnimationSettingsChanged.Invoke(nameof(preserveLoops));
            }
        }

        public float blendInDuration
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
                onAnimationSettingsChanged.Invoke(nameof(blendInDuration));
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
                onAnimationSettingsChanged.Invoke(nameof(nextAnimationName));
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
                if (!_skipNextAnimationSettingsModified) onAnimationSettingsChanged.Invoke(nameof(nextAnimationTime));
            }
        }
        public float nextAnimationTimeRandomize
        {
            get
            {
                return _nextAnimationTimeRandomize;
            }
            set
            {
                if (_nextAnimationTimeRandomize == value) return;
                _nextAnimationTimeRandomize = value;
                if (!_skipNextAnimationSettingsModified) onAnimationSettingsChanged.Invoke(nameof(nextAnimationTimeRandomize));
            }
        }
        private float _speed = 1f;
        public float speed
        {
            get
            {
                return _speed;
            }

            set
            {
                _speed = value;
                onPlaybackSettingsChanged.Invoke();
            }
        }
        private float _weight = 1f;
        public float scaledWeight { get; private set; } = 1f;
        public float weight
        {
            get
            {
                return _weight;
            }

            set
            {
                _weight = Mathf.Clamp01(value);
                scaledWeight = value.ExponentialScale(0.1f, 1f);
                if (playbackBlendRate > 0) playbackBlendRate = 0;
                if (playbackBlendWeight != _weight && playbackBlendRate == 0) playbackBlendWeight = _weight;
                onPlaybackSettingsChanged.Invoke();
            }
        }

        public AtomPose pose
        {
            get
            {
                return _pose;
            }
            set
            {
                _pose = value;
                if (value == null) applyPoseOnTransition = false;
                onAnimationSettingsChanged.Invoke(nameof(pose));
            }
        }

        public bool applyPoseOnTransition
        {
            get
            {
                return _applyPoseOnTransition;
            }
            set
            {
                #warning This means zero transition time
                #warning This should skip an animation frame before applying morphs, to validate (see collider bug)
                _applyPoseOnTransition = value;
                onAnimationSettingsChanged.Invoke(nameof(applyPoseOnTransition));
                #warning We should make some effort to keep those settings in a valid state in both directions
                blendInDuration = 0;
            }
        }

        public AtomAnimationClip(string animationName, string animationLayer)
        {
            this.animationName = animationName;
            this.animationLayer = animationLayer;
        }

        public bool IsEmpty()
        {
            return !GetAllTargets().Any();
        }

        public bool IsDirty()
        {
            return GetAllTargets().Any(t => t.dirty);
        }

        public void DirtyAll()
        {
            foreach (var s in GetAllTargets())
                s.dirty = true;
        }

        public IEnumerable<IAtomAnimationTargetsList> GetTargetGroups()
        {
            yield return targetTriggers;
            yield return targetControllers;
            foreach (var group in targetFloatParams.GroupBy(t => t.animatableRef.storableId))
                yield return new AtomAnimationTargetsList<JSONStorableFloatAnimationTarget>(group) {label = group.Key};
        }

        public void Validate()
        {
            foreach (var target in GetAllTargets())
            {
                if (!target.dirty) continue;

                target.Validate(_animationLength);
            }
        }

        #region Animation State

        private float _clipTime;
        public float playbackBlendWeight { get; set; }
        public bool playbackEnabled { get; set; }
        public bool temporarilyEnabled { get; set; }
        public bool playbackMainInLayer;
        public float playbackBlendRate;
        public string playbackScheduledNextAnimationName;
        public float playbackScheduledNextTimeLeft;

        public float clipTime
        {
            get
            {
                return _clipTime;
            }

            set
            {
                if (loop)
                {
                    if (value >= 0)
                    {
                        _clipTime = value % animationLength;
                    }
                    else
                    {
                        _clipTime = animationLength + value;
                    }
                }
                else
                {
                    _clipTime = Mathf.Clamp(value, 0, animationLength);
                }
            }
        }

        public void SetNext(string nextAnimationName, float nextTime)
        {
            playbackScheduledNextAnimationName = nextAnimationName;
            playbackScheduledNextTimeLeft = nextTime;
        }

        public void Reset(bool resetTime)
        {
            playbackEnabled = false;
            playbackBlendWeight = 0f;
            playbackBlendRate = 0f;
            playbackMainInLayer = false;
            SetNext(null, 0f);
            if (resetTime)
            {
                clipTime = 0f;
            }
            else
            {
                clipTime = clipTime.Snap();
            }
        }

        public void Leave()
        {
            foreach (var target in targetTriggers)
            {
                foreach (var trigger in target.triggersMap.Values)
                {
                    trigger.Leave();
                }
            }
        }

        #endregion

        #region Add/Remove Targets

        public IAtomAnimationTarget Add(IAtomAnimationTarget target)
        {
            if (target is FreeControllerV3AnimationTarget)
                return Add((FreeControllerV3AnimationTarget)target);
            if (target is JSONStorableFloatAnimationTarget)
                return Add((JSONStorableFloatAnimationTarget)target);
            if (target is TriggersTrackAnimationTarget)
                return Add((TriggersTrackAnimationTarget)target);
            throw new NotSupportedException($"Cannot add unknown target type {target}");
        }

        public FreeControllerV3AnimationTarget Add(FreeControllerV3Ref controllerRef)
        {
            if (targetControllers.Any(c => c.animatableRef == controllerRef)) return null;
            return Add(new FreeControllerV3AnimationTarget(controllerRef));
        }

        public FreeControllerV3AnimationTarget Add(FreeControllerV3AnimationTarget target)
        {
            if (targetControllers.Any(t => t.animatableRef == target.animatableRef)) return null;
            foreach (var curve in target.curves) { curve.loop = _loop; }
            return Add(targetControllers, new FreeControllerV3AnimationTarget.Comparer(), target);
        }

        public JSONStorableFloatAnimationTarget Add(JSONStorableFloatRef floatRef)
        {
            if (targetFloatParams.Any(t => t.animatableRef == floatRef)) return null;
            return Add(new JSONStorableFloatAnimationTarget(floatRef));
        }

        public JSONStorableFloatAnimationTarget Add(JSONStorableFloatAnimationTarget target)
        {
            target.value.loop = _loop;
            return Add(targetFloatParams, new JSONStorableFloatAnimationTarget.Comparer(), target);
        }

        public TriggersTrackAnimationTarget Add(TriggersTrackAnimationTarget target)
        {
            return Add(targetTriggers, new TriggersTrackAnimationTarget.Comparer(), target);
        }

        private T Add<T>(AtomAnimationTargetsList<T> list, IComparer<T> comparer, T target) where T : IAtomAnimationTarget
        {
            if (target == null) throw new NullReferenceException(nameof(target));
            if (target.clip != null) throw new InvalidOperationException($"Target {target.name} already assigned to clip {target.clip.animationNameQualified}");
            target.clip = this;
            list.Add(target);
            list.Sort(comparer);
            target.onAnimationKeyframesDirty.AddListener(OnAnimationKeyframesDirty);
            onTargetsListChanged.Invoke();
            return target;
        }

        public void Remove(IAtomAnimationTarget target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));

            target.clip = null;

            var freeControllerAnimationTarget = target as FreeControllerV3AnimationTarget;
            if (freeControllerAnimationTarget != null)
            {
                Remove(freeControllerAnimationTarget);
                return;
            }

            var floatParamAnimationTarget = target as JSONStorableFloatAnimationTarget;
            if (floatParamAnimationTarget != null)
            {
                Remove(floatParamAnimationTarget);
                return;
            }

            var animationTarget = target as TriggersTrackAnimationTarget;
            if (animationTarget != null)
            {
                Remove(animationTarget);
                return;
            }

            #warning Cleanup unused refs?

            throw new NotSupportedException($"Cannot remove unknown target type {target}");
        }

        private void Remove(FreeControllerV3AnimationTarget target)
        {
            Remove(targetControllers, target);
        }

        private void Remove(JSONStorableFloatAnimationTarget target)
        {
            Remove(targetFloatParams, target);
        }

        private void Remove(TriggersTrackAnimationTarget target)
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

        private void OnAnimationKeyframesDirty()
        {
            onAnimationKeyframesDirty.Invoke();
        }

        #endregion

        #region Clipboard

        public static AtomClipboardEntry Copy(float time, IList<IAtomAnimationTarget> targets)
        {
            var controllers = new List<FreeControllerV3ClipboardEntry>();
            foreach (var target in targets.OfType<FreeControllerV3AnimationTarget>())
            {
                var snapshot = target.GetCurveSnapshot(time);
                if (snapshot == null) continue;
                controllers.Add(new FreeControllerV3ClipboardEntry
                {
                    controllerRef = target.animatableRef,
                    snapshot = snapshot
                });
            }
            var floatParams = new List<FloatParamValClipboardEntry>();
            foreach (var target in targets.OfType<JSONStorableFloatAnimationTarget>())
            {
                var snapshot = target.GetCurveSnapshot(time);
                if (snapshot == null) continue;
                floatParams.Add(new FloatParamValClipboardEntry
                {
                    floatRef = target.animatableRef,
                    snapshot = snapshot
                });
            }
            var triggers = new List<TriggersClipboardEntry>();
            foreach (var target in targets.OfType<TriggersTrackAnimationTarget>())
            {
                var snapshot = target.GetCurveSnapshot(time);
                if (snapshot == null) continue;
                triggers.Add(new TriggersClipboardEntry
                {
                    name = target.name,
                    snapshot = snapshot
                });
            }
            return new AtomClipboardEntry
            {
                time = time.Snap(),
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
                var target = targetControllers.FirstOrDefault(c => c.animatableRef == entry.controllerRef);
                if (target == null)
                {
                    SuperController.LogError($"Cannot paste controller {entry.controllerRef.name} in animation [{animationLayer}] {animationName} because the target was not added.");
                    continue;
                }
                target.SetCurveSnapshot(time, entry.snapshot, dirty);
            }
            foreach (var entry in clipboard.floatParams)
            {
                var target = targetFloatParams.FirstOrDefault(c => c.animatableRef == entry.floatRef);
                if (target == null)
                {
                    SuperController.LogError($"Cannot paste storable {entry.floatRef.name} in animation [{animationLayer}] {animationName} because the target was not added.");
                    continue;
                }
                target.SetCurveSnapshot(time, entry.snapshot, dirty);
            }
            foreach (var entry in clipboard.triggers)
            {
                if (!dirty) throw new InvalidOperationException("Cannot paste triggers without dirty");
                var target = targetTriggers.FirstOrDefault(t => t.name == entry.name) ?? targetTriggers.FirstOrDefault();
                if (target == null)
                {
                    SuperController.LogError($"Cannot paste triggers {entry.name} in animation [{animationLayer}] {animationName} because the target was not added.");
                    continue;
                }
                target.SetCurveSnapshot(time, entry.snapshot);
            }
        }

        public void CopySettingsTo(AtomAnimationClip target)
        {
            target.loop = loop;
            target.animationLength = animationLength;
            target.animationLayer = animationLayer;
            target.nextAnimationName = nextAnimationName;
            target.nextAnimationTime = nextAnimationTime;
            target.ensureQuaternionContinuity = ensureQuaternionContinuity;
            target.blendInDuration = blendInDuration;
            target.speed = speed;
            target.nextAnimationTimeRandomize = nextAnimationTimeRandomize;
            target.preserveLoops = preserveLoops;
        }

        #endregion

        private void UpdateForcedNextAnimationTime()
        {
            _skipNextAnimationSettingsModified = true;
            try
            {
                if (loop) return;
                if (nextAnimationName == null)
                    nextAnimationTime = 0;
                else
                    nextAnimationTime = animationLength;
            }
            finally
            {
                _skipNextAnimationSettingsModified = false;
            }
        }

        public void Dispose()
        {
            onAnimationKeyframesDirty.RemoveAllListeners();
            onAnimationKeyframesRebuilt.RemoveAllListeners();
            onAnimationSettingsChanged.RemoveAllListeners();
            onTargetsListChanged.RemoveAllListeners();
            foreach (var target in GetAllTargets())
            {
                target.Dispose();
            }
        }
    }
}
