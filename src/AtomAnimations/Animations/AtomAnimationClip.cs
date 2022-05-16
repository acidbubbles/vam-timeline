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
        public const float DefaultBlendDuration = 1.0f;
        public const string SharedAnimationSegment = "[SHARED]";
        public static readonly int SharedAnimationSegmentId = SharedAnimationSegment.ToId();
        public const string NoneAnimationSegment = "[]";
        public static readonly int NoneAnimationSegmentId = NoneAnimationSegment.ToId();
        public const string DefaultAnimationSegment = NoneAnimationSegment;
        public const string DefaultAnimationLayer = "Main";
        public const string DefaultAnimationName = "Anim 1";

        public const string RandomizeAnimationName = "(Randomize)";
        public static readonly int RandomizeAnimationNameId = RandomizeAnimationName.ToId();
        public const string SlaveAnimationName = "(Slave)";
        public static readonly int SlaveAnimationNameId = SlaveAnimationName.ToId();
        public const string RandomizeGroupSuffix = "/*";
        public const string NextAnimationSegmentPrefix = "Segment: ";

        private readonly Logger _logger;
        private bool _loop = true;
        private float _timeOffset;
        private string _nextAnimationName;
        private float _animationLength = DefaultAnimationLength;
        private bool _autoTransitionPrevious;
        private bool _autoTransitionNext;
        private bool _preserveLoops = true;
        private float _blendDuration = DefaultBlendDuration;
        private float _nextAnimationTime;
        private float _nextAnimationRandomizeWeight = 1;
        private float _nextAnimationTimeRandomize;
        private string _animationName;
        private string _animationLayer;
        private string _animationSegment;
        private bool _ensureQuaternionContinuity = true;
        private bool _skipNextAnimationSettingsModified;
        private AnimationPattern _animationPattern;
        private AudioSourceControl _audioSourceControl;
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
        public AudioSourceControl audioSourceControl
        {
            get
            {
                return _audioSourceControl;
            }
            set
            {
                _audioSourceControl = value;
                onAnimationSettingsChanged.Invoke(nameof(audioSourceControl));
            }
        }

        public float timeOffset
        {
            get
            {
                return _timeOffset;
            }
            set
            {
                if (playbackEnabled)
                {
                    clipTime += (value - _timeOffset);
                }
                _timeOffset = value;
                onAnimationSettingsChanged.Invoke(nameof(timeOffset));
            }
        }

        public readonly AtomAnimationTargetsList<TriggersTrackAnimationTarget> targetTriggers = new AtomAnimationTargetsList<TriggersTrackAnimationTarget> { label = "Triggers" };
        public readonly AtomAnimationTargetsList<FreeControllerV3AnimationTarget> targetControllers = new AtomAnimationTargetsList<FreeControllerV3AnimationTarget> { label = "Controls" };
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

        #region Calculated fields

        public string animationNameQualified { get; private set; }
        public string animationLayerQualified { get; private set; }
        public string animationSetQualified { get; private set; }

        public int animationNameId { get; private set; }

        public int animationNameQualifiedId { get; private set; }

        public int animationLayerId { get; private set; }

        public int animationLayerQualifiedId { get; private set; }

        public int animationSegmentId { get; private set; }

        public int animationSetId { get; private set; }

        public bool isOnSharedSegment { get; private set; }
        public bool isOnNoneSegment { get; private set; }

        private void UpdateAnimationNameQualified()
        {
            animationNameQualified = $"{_animationSegment}::{_animationLayer}::{_animationName}";
            animationLayerQualified = $"{_animationSegment}::{_animationLayer}";
            animationSetQualified = $"{_animationSegment}::{_animationSet}";

            animationNameId = animationName.ToId();
            animationNameQualifiedId = animationNameQualified.ToId();
            animationLayerId = animationLayer.ToId();
            animationLayerQualifiedId = animationLayerQualified.ToId();
            animationSegmentId = animationSegment.ToId();
            animationSetId = animationSet.ToId();
        }

        public string animationNameGroup { get; private set; }
        public int animationNameGroupId { get; private set; }

        private void UpdateAnimationNameGroup()
        {
            var idxOfGroupSeparator = _animationName.IndexOf('/');
            if (idxOfGroupSeparator > -1)
            {
                animationNameGroup = _animationName.Substring(0, idxOfGroupSeparator);
                animationNameGroupId = animationNameGroup.ToId();
            }
            else
            {
                animationNameGroup = null;
                animationNameGroupId = -1;
            }
        }

        #endregion

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

        public string animationSegment
        {
            get
            {
                return _animationSegment;
            }
            set
            {
                if (_animationSegment == value) return;
                _animationSegment = value;
                UpdateAnimationNameQualified();
                isOnNoneSegment = value == NoneAnimationSegment;
                isOnSharedSegment = value == SharedAnimationSegment;
                onAnimationSettingsChanged.Invoke(nameof(animationSegment));
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
                UpdateAnimationNameGroup();
                UpdateAnimationNameQualified();
                onAnimationSettingsChanged.Invoke(nameof(animationName));
            }
        }

        private string _animationSet;

        public string animationSet
        {
            get
            {
                return _animationSet;
            }
            set
            {
                if (_animationSet == value) return;
                _animationSet = value == string.Empty ? null : value;
                UpdateAnimationNameQualified();
                onAnimationSettingsChanged.Invoke(nameof(animationSet));
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

        public float halfBlendInDuration { get; private set; } = DefaultBlendDuration / 2f;
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
                halfBlendInDuration = value / 2f;
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
                nextAnimationNameId = -1;
                nextAnimationSegmentRefId = -1;
                nextAnimationGroupId = -1;
                if (string.IsNullOrEmpty(value))
                {
                    _nextAnimationName = null;
                }
                else
                {
                    _nextAnimationName = value;
                    nextAnimationNameId = value.ToId();

                    if (_nextAnimationName != null && _nextAnimationName.StartsWith(NextAnimationSegmentPrefix))
                        nextAnimationSegmentRefId = _nextAnimationName.Substring(NextAnimationSegmentPrefix.Length).ToId();

                    if (_nextAnimationName != null && _nextAnimationName.EndsWith(RandomizeGroupSuffix))
                        nextAnimationGroupId = animationName.Substring(0, animationName.Length - RandomizeGroupSuffix.Length).ToId();
                }

                UpdateForcedNextAnimationTime();
                onAnimationSettingsChanged.Invoke(nameof(nextAnimationName));
            }
        }

        public int nextAnimationNameId { get; private set; }
        public int nextAnimationGroupId { get; private set; }
        public int nextAnimationSegmentRefId { get; private set; }

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
        public float nextAnimationRandomizeWeight
        {
            get
            {
                return _nextAnimationRandomizeWeight;
            }
            set
            {
                if (_nextAnimationRandomizeWeight == value) return;
                _nextAnimationRandomizeWeight = value;
                if (!_skipNextAnimationSettingsModified) onAnimationSettingsChanged.Invoke(nameof(nextAnimationRandomizeWeight));
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
                // ReSharper disable once CompareOfFloatsByEqualityOperator
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

        private float _applyPoseOnTransitionRestoredBlendDuration = DefaultBlendDuration;
        public bool applyPoseOnTransition
        {
            get
            {
                return _applyPoseOnTransition;
            }
            set
            {
                if (value == _applyPoseOnTransition) return;
                _applyPoseOnTransition = value;
                onAnimationSettingsChanged.Invoke(nameof(applyPoseOnTransition));
                if (_applyPoseOnTransition)
                {
                    _applyPoseOnTransitionRestoredBlendDuration = _blendDuration;
                    blendInDuration = 0;
                }
                else
                {
                    blendInDuration = _applyPoseOnTransitionRestoredBlendDuration;
                }
            }
        }
        private bool _fadeOnTransition;
        public bool fadeOnTransition
        {
            get
            {
                return _fadeOnTransition;
            }
            set
            {
                if (value == _fadeOnTransition) return;
                _fadeOnTransition = value;
                onAnimationSettingsChanged.Invoke(nameof(fadeOnTransition));
            }
        }

        public AtomAnimationClip(string animationName, string animationLayer, string animationSegment, Logger logger)
        {
            _animationName = animationName;
            _animationLayer = animationLayer;
            _animationSegment = animationSegment;
            _logger = logger;
            UpdateAnimationNameGroup();
            UpdateAnimationNameQualified();
        }

        public bool IsEmpty()
        {
            return !GetAllTargets().Any() && pose == null;
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
            foreach (var group in targetControllers.GroupBy(t => t.animatableRef.controller != null ? t.animatableRef.controller.containingAtom : null))
            {
                var atom = group.Key;
                string groupLabel;
                if (atom == null)
                    groupLabel = "[Deleted]";
                else if (group.First().animatableRef.owned)
                    groupLabel = "Controls";
                else
                    groupLabel = $"{group.Key.name} controls";
                yield return new AtomAnimationTargetsList<FreeControllerV3AnimationTarget>(group) { label = groupLabel };
            }
            foreach (var group in targetFloatParams.GroupBy(t => t.animatableRef.storableId))
            {
                var groupLabel = group.Key;
                if (groupLabel.StartsWith("plugin#"))
                    groupLabel = groupLabel.Substring(6);
                yield return new AtomAnimationTargetsList<JSONStorableFloatAnimationTarget>(group) { label = groupLabel };
            }
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
        private float _playbackBlendWeight;

        public float playbackBlendWeightSmoothed { get; private set; }

        public float playbackBlendWeight
        {
            get
            {
                return _playbackBlendWeight;
            }
            set
            {
                _playbackBlendWeight = value;
                playbackBlendWeightSmoothed = value.SmootherStep();
            }
        }
        public bool playbackEnabled { get; set; }
        public bool temporarilyEnabled { get; set; }
        public bool playbackMainInLayer;
        public float playbackBlendRate;
        public AtomAnimationClip playbackScheduledNextAnimation;
        public float playbackScheduledNextTimeLeft = float.NaN;
        public float playbackScheduledFadeOutAtRemaining = float.NaN;
        public bool recording;
        public bool infinite;

        public float clipTime
        {
            get
            {
                return _clipTime;
            }

            set
            {
                if (infinite)
                {
                    _clipTime = value;
                }
                else if (loop && !recording)
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

        public void Reset(bool resetTime)
        {
            playbackEnabled = false;
            playbackBlendWeight = 0f;
            playbackBlendRate = 0f;
            playbackMainInLayer = false;
            playbackScheduledFadeOutAtRemaining = float.NaN;
            playbackScheduledNextTimeLeft = float.NaN;
            playbackScheduledNextAnimation = null;
            if (recording)
            {
                recording = false;
                foreach (var target in GetAllCurveTargets())
                {
                    target.recording = false;
                }
            }
            clipTime = resetTime ? 0f : clipTime.Snap();
        }

        public void Leave()
        {
            for (var trigIdx = 0; trigIdx < targetTriggers.Count; trigIdx++)
            {
                targetTriggers[trigIdx].Leave();
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

        public JSONStorableFloatAnimationTarget Add(JSONStorableFloatRef floatRef)
        {
            if (targetFloatParams.Any(t => t.animatableRef == floatRef)) return null;
            return Add(new JSONStorableFloatAnimationTarget(floatRef));
        }

        public TriggersTrackAnimationTarget Add(TriggersTrackRef triggersRef)
        {
            if (targetTriggers.Any(t => t.animatableRef == triggersRef)) return null;
            return Add(new TriggersTrackAnimationTarget(triggersRef, _logger));
        }

        public IAtomAnimationTarget Add(AnimatableRefBase animatableRef)
        {
            if (animatableRef is FreeControllerV3Ref)
                return Add((FreeControllerV3Ref)animatableRef);
            if (animatableRef is JSONStorableFloatRef)
                return Add((JSONStorableFloatRef)animatableRef);
            if (animatableRef is TriggersTrackRef)
                return Add((TriggersTrackRef)animatableRef);
            throw new NotSupportedException($"Cannot add unknown animatableRef type {animatableRef}");
        }

        public FreeControllerV3AnimationTarget Add(FreeControllerV3AnimationTarget target)
        {
            if (targetControllers.Any(t => t.animatableRef == target.animatableRef)) return null;
            foreach (var curve in target.curves) { curve.loop = _loop; }
            return Add(targetControllers, new FreeControllerV3AnimationTarget.Comparer(), target);
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

        private T Add<T>(List<T> list, IComparer<T> comparer, T target) where T : IAtomAnimationTarget
        {
            if (target == null) throw new NullReferenceException(nameof(target));
            if (target.clip != null) throw new InvalidOperationException($"Target {target.name} cannot be assigned to {animationNameQualified} because it is already assigned to clip {target.clip.animationNameQualified}");
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
                    animatableRef = target.animatableRef,
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
                    animatableRef = target.animatableRef,
                    snapshot = snapshot
                });
            }
            var triggers = new List<TriggersClipboardEntry>();
            foreach (var target in targets.OfType<TriggersTrackAnimationTarget>())
            {
                var snapshot = target.GetTypedSnapshot(time);
                if (snapshot == null) continue;
                triggers.Add(new TriggersClipboardEntry
                {
                    animatableRef = target.animatableRef,
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
                var target = targetControllers.FirstOrDefault(c => c.animatableRef == entry.animatableRef);
                if (target == null)
                {
                    SuperController.LogError($"Cannot paste controller {entry.animatableRef.name} in animation '{animationNameQualified}' because the target could not be added to the current layer.");
                    continue;
                }
                target.SetCurveSnapshot(time, entry.snapshot, dirty);
            }
            foreach (var entry in clipboard.floatParams)
            {
                var target = targetFloatParams.FirstOrDefault(c => c.animatableRef == entry.animatableRef);
                if (target == null)
                {
                    SuperController.LogError($"Cannot paste storable {entry.animatableRef.name} in animation '{animationNameQualified}' because the target could not be added to the current layer.");
                    continue;
                }
                target.SetCurveSnapshot(time, entry.snapshot, dirty);
            }
            foreach (var entry in clipboard.triggers)
            {
                if (!dirty) throw new InvalidOperationException("Cannot paste triggers without dirty");
                var target = targetTriggers.FirstOrDefault(t => t.animatableRef == entry.animatableRef) ?? targetTriggers.FirstOrDefault();
                if (target == null)
                {
                    SuperController.LogError($"Cannot paste triggers {entry.animatableRef.name} in animation '{animationNameQualified}' because the target could not be added to the current layer.");
                    continue;
                }
                target.SetCurveSnapshot(time, entry.snapshot);
            }
        }

        public void CopySettingsTo(AtomAnimationClip target)
        {
            target.loop = loop;
            target.animationLength = animationLength;
            target.animationSet = animationSet;
            target.ensureQuaternionContinuity = ensureQuaternionContinuity;
            target.speed = speed;
            target.preserveLoops = preserveLoops;
            target.timeOffset = timeOffset;
            target.weight = weight;
            target.blendInDuration = blendInDuration;
            target.nextAnimationName = nextAnimationName;
            target.nextAnimationTime = nextAnimationTime;
            target.nextAnimationTimeRandomize = nextAnimationTimeRandomize;
            target.nextAnimationRandomizeWeight = nextAnimationRandomizeWeight;
        }

        #endregion

        #region Animation rebuilding

        public void Rebuild(AtomAnimationClip previous)
        {
            foreach (var target in targetControllers)
            {
                if (!target.dirty) continue;

                if (loop)
                    target.SetCurveSnapshot(animationLength, target.GetCurveSnapshot(0f), false);

                target.ComputeCurves();

                if (ensureQuaternionContinuity)
                {
                    var lastMatching = previous?.targetControllers.FirstOrDefault(t => t.TargetsSameAs(target));
                    var q = lastMatching?.GetRotationAtKeyframe(lastMatching.rotX.length - 1) ?? target.GetRotationAtKeyframe(target.rotX.length - 1);
                    UnitySpecific.EnsureQuaternionContinuityAndRecalculateSlope(
                        target.rotX,
                        target.rotY,
                        target.rotZ,
                        target.rotW,
                        q);
                }

                foreach (var curve in target.GetCurves())
                    curve.ComputeCurves();
            }

            foreach (var target in targetFloatParams)
            {
                if (!target.dirty) continue;

                if (loop)
                    target.SetCurveSnapshot(animationLength, target.GetCurveSnapshot(0), false);

                target.value.ComputeCurves();
            }

            foreach (var target in targetTriggers)
            {
                if (!target.dirty) continue;

                target.RebuildKeyframes(animationLength);
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
