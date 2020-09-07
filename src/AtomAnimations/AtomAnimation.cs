using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using Random = UnityEngine.Random;

namespace VamTimeline
{
    public class AtomAnimation : MonoBehaviour
    {
        public struct TimeChangedEventArgs { public float time; public float currentClipTime; }
        public class TimeChangedEvent : UnityEvent<TimeChangedEventArgs> { }
        public class CurrentAnimationChangedEventArgs { public AtomAnimationClip before; public AtomAnimationClip after; }
        public class CurrentAnimationChangedEvent : UnityEvent<CurrentAnimationChangedEventArgs> { }
        public class AnimationSettingsChanged : UnityEvent<string> { }
        public class IsPlayingEvent : UnityEvent<AtomAnimationClip> { }

        public const float PaddingBeforeLoopFrame = 0.001f;
        public const string RandomizeAnimationName = "(Randomize)";
        public const string RandomizeGroupSuffix = "/*";

        public TimeChangedEvent onTimeChanged = new TimeChangedEvent();
        public CurrentAnimationChangedEvent onCurrentAnimationChanged = new CurrentAnimationChangedEvent();
        public UnityEvent onAnimationSettingsChanged = new UnityEvent();
        public AnimationSettingsChanged onEditorSettingsChanged = new AnimationSettingsChanged();
        public UnityEvent onSpeedChanged = new UnityEvent();
        public UnityEvent onClipsListChanged = new UnityEvent();
        public UnityEvent onTargetsSelectionChanged = new UnityEvent();
        public UnityEvent onAnimationRebuilt = new UnityEvent();
        public IsPlayingEvent onIsPlayingChanged = new IsPlayingEvent();

        #region Editor Settings
        private float _snap = 0.1f;
        public float snap
        {
            get { return _snap; }
            set { _snap = value; onEditorSettingsChanged.Invoke(nameof(snap)); }
        }
        private bool _autoKeyframeAllControllers;
        public bool autoKeyframeAllControllers
        {
            get { return _autoKeyframeAllControllers; }
            set { _autoKeyframeAllControllers = value; onEditorSettingsChanged.Invoke(nameof(autoKeyframeAllControllers)); }
        }
        #endregion

        public List<AtomAnimationClip> clips { get; } = new List<AtomAnimationClip>();
        public TimeChangedEventArgs timeArgs => new TimeChangedEventArgs { time = playTime, currentClipTime = current.clipTime };
        public bool isPlaying { get; private set; }
        public bool isSampling { get; private set; }
        private bool allowAnimationProcessing => isPlaying && !SuperController.singleton.freezeAnimation;

        public AtomAnimationClip current { get; private set; }

        public float clipTime
        {
            get
            {
                return current.clipTime;
            }
            set
            {
                playTime = value;
                if (current == null) return;
                current.clipTime = value;
                if (isPlaying && !current.playbackEnabled && current.playbackMainInLayer) PlayClip(current, sequencing);
                Sample();
                if (current.animationPattern != null)
                    current.animationPattern.SetFloatParamValue("currentTime", playTime);
                onTimeChanged.Invoke(timeArgs);
            }
        }

        private float _playTime;
        public float playTime
        {
            get
            {
                return _playTime;
            }
            set
            {
                SetPlayTime(value);
                if (!current.playbackEnabled)
                    current.clipTime = value;
                Sample();
                foreach (var clip in clips)
                {
                    if (clip.animationPattern != null)
                        clip.animationPattern.SetFloatParamValue("currentTime", clip.clipTime);
                }
                onTimeChanged.Invoke(timeArgs);
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
                foreach (var clip in clips)
                {
                    if (clip.animationPattern != null)
                        clip.animationPattern.SetFloatParamValue("speed", value);
                }
                onSpeedChanged.Invoke();
            }
        }

        public bool sequencing { get; private set; }

        private bool _animationRebuildRequestPending;
        private bool _animationRebuildInProgress;
        private bool _sampleAfterRebuild;

        public AtomAnimation()
        {
        }

        public void Initialize()
        {
            if (clips.Count == 0)
                AddClip(new AtomAnimationClip("Anim 1", AtomAnimationClip.DefaultAnimationLayer));
            current = GetDefaultClip();
            if (clips.Any(c => c.IsDirty()))
            {
                RebuildAnimationNow();
            }
        }

        public AtomAnimationClip GetDefaultClip()
        {
            var firstLayer = clips[0].animationLayer;
            return clips.TakeWhile(c => c.animationLayer == firstLayer).FirstOrDefault(c => c.animationLayer == firstLayer && c.autoPlay) ?? clips[0];
        }

        public bool IsEmpty()
        {
            if (clips.Count == 0) return true;
            if (clips.Count == 1 && clips[0].IsEmpty()) return true;
            return false;
        }

        public int SetKeyframeToCurrentTransform(FreeControllerAnimationTarget target, float time)
        {
            time = time.Snap();
            if (time > current.animationLength)
                time = current.animationLength;
            return target.SetKeyframeToCurrentTransform(time);
        }

        #region Clips

        public AtomAnimationClip GetClip(string name)
        {
            return clips.FirstOrDefault(c => c.animationName == name);
        }

        public AtomAnimationClip AddClip(AtomAnimationClip clip)
        {
            var lastIndexOfLayer = clips.FindLastIndex(c => c.animationLayer == clip.animationLayer);
            if (lastIndexOfLayer == -1)
                clips.Add(clip);
            else
                clips.Insert(lastIndexOfLayer + 1, clip);
            clip.onAnimationSettingsChanged.AddListener(OnAnimationSettingsChanged);
            clip.onAnimationKeyframesDirty.AddListener(OnAnimationKeyframesDirty);
            clip.onTargetsListChanged.AddListener(OnAnimationKeyframesDirty);
            clip.onTargetsSelectionChanged.AddListener(OnTargetsSelectionChanged);
            onClipsListChanged.Invoke();
            if (clip.IsDirty()) clip.onAnimationKeyframesDirty.Invoke();
            return clip;
        }

        public AtomAnimationClip CreateClip(string animationLayer)
        {
            string animationName = GetNewAnimationName();
            return CreateClip(animationLayer, animationName);
        }

        public AtomAnimationClip CreateClip(string animationLayer, string animationName)
        {
            if (clips.Any(c => c.animationName == animationName)) throw new InvalidOperationException($"Animation '{animationName}' already exists");
            var clip = new AtomAnimationClip(animationName, animationLayer);
            AddClip(clip);
            return clip;
        }

        public void RemoveClip(AtomAnimationClip clip)
        {
            clips.Remove(clip);
            clip.Dispose();
            onClipsListChanged.Invoke();
            OnAnimationKeyframesDirty();
        }

        private string GetNewAnimationName()
        {
            for (var i = clips.Count + 1; i < 999; i++)
            {
                var animationName = "Anim " + i;
                if (!clips.Any(c => c.animationName == animationName)) return animationName;
            }
            return Guid.NewGuid().ToString();
        }

        public IEnumerable<string> EnumerateLayers()
        {
            if (clips.Count == 0) yield break;
            if (clips.Count == 1)
            {
                yield return clips[0].animationLayer;
                yield break;
            }
            var lastLayer = clips[0].animationLayer;
            yield return lastLayer;
            for (var i = 1; i < clips.Count; i++)
            {
                var clip = clips[i];
                if (clip.animationLayer == lastLayer) continue;
                yield return lastLayer = clip.animationLayer;
            }
        }

        #endregion

        #region Playback

        public void PlayClip(string animationName, bool sequencing)
        {
            var clip = GetClip(animationName);
            PlayClip(clip, sequencing);
        }

        public void PlayClip(AtomAnimationClip clip, bool sequencing)
        {
            if (clip.playbackEnabled && clip.playbackMainInLayer) return;
            if (!isPlaying)
            {
                isPlaying = true;
                this.sequencing = this.sequencing || sequencing;
            }
            if (sequencing && !clip.playbackEnabled) clip.clipTime = 0;
            var previousMain = clips.FirstOrDefault(c => c.playbackMainInLayer && c.animationLayer == clip.animationLayer);
            if (previousMain != null && previousMain != clip)
            {
                if (clip.syncTransitionTime && previousMain.loop && !clip.loop)
                {
                    previousMain.SetNext(clip.animationName, playTime + (previousMain.animationLength - previousMain.clipTime));
                }
                else
                {
                    TransitionAnimation(previousMain, clip);
                }
            }
            else
            {
                if (clip.clipTime == clip.animationLength) clip.clipTime = 0f;
                Blend(clip, 1f, clip.blendInDuration);
                clip.playbackMainInLayer = true;
            }
            if (clip.animationPattern)
            {
                clip.animationPattern.SetBoolParamValue("loopOnce", false);
                clip.animationPattern.ResetAndPlay();
            }
            if (sequencing && clip.nextAnimationName != null)
                AssignNextAnimation(clip);

            onIsPlayingChanged.Invoke(clip);
        }

        public void PlayCurrentAndOtherMainsInLayers(bool sequencing = true)
        {
            PlayOneAndOtherMainsInLayers(current, sequencing);
        }

        public void PlayOneAndOtherMainsInLayers(AtomAnimationClip selected, bool sequencing = true)
        {
            foreach (var clip in GetMainClipPerLayer())
            {
                if (clip.animationLayer == selected.animationLayer)
                    PlayClip(selected, sequencing);
                else
                    PlayClip(clip, sequencing);
            }
        }

        private IEnumerable<AtomAnimationClip> GetMainClipPerLayer()
        {
            return clips
                .GroupBy(c => c.animationLayer)
                .Select(g =>
                {
                    return g.FirstOrDefault(c => c == current || c.playbackMainInLayer) ?? g.FirstOrDefault(c => c.autoPlay) ?? g.First();
                });
        }

        public void StopClip(string animationName)
        {
            var clip = GetClip(animationName);
            StopClip(clip);
        }

        public void StopClip(AtomAnimationClip clip)
        {
            if (clip.playbackEnabled)
            {
                clip.Leave();
                clip.Reset(false);
                if (clip.animationPattern)
                    clip.animationPattern.SetBoolParamValue("loopOnce", true);
            }
            else
            {
                clip.playbackMainInLayer = false;
            }

            if (isPlaying)
            {
                if (!clips.Any(c => c.playbackMainInLayer))
                {
                    isPlaying = false;
                    sequencing = false;
                    playTime = current.clipTime;
                }

                onIsPlayingChanged.Invoke(clip);
            }
        }

        public void StopAll()
        {
            foreach (var clip in clips)
            {
                StopClip(clip);
            }
            foreach (var clip in clips)
            {
                clip.Reset(false);
            }
        }

        public void ResetAll()
        {
            isPlaying = false;
            _playTime = 0f;
            foreach (var clip in clips)
            {
                clip.Reset(true);
            }
            playTime = playTime;
        }

        public void StopAndReset()
        {
            if (isPlaying) StopAll();
            ResetAll();
            var defaultClip = GetDefaultClip();
            if (current != defaultClip) SelectAnimation(defaultClip);
        }

        #endregion

        #region Animation state

        private void SetPlayTime(float value)
        {
            var delta = value - _playTime;
            if (delta == 0) return;
            _playTime = value;
            foreach (var clip in clips)
            {
                if (!clip.playbackEnabled) continue;

                var clipDelta = delta * clip.speed;
                clip.clipTime += clipDelta;
                if (clip.playbackBlendRate != 0)
                {
                    // TODO: Mathf.SmoothStep
                    clip.playbackWeight += clip.playbackBlendRate * clipDelta;
                    if (clip.playbackWeight >= clip.weight)
                    {
                        clip.playbackBlendRate = 0f;
                        clip.playbackWeight = clip.weight;
                    }
                    else if (clip.playbackWeight <= 0f)
                    {
                        clip.playbackBlendRate = 0f;
                        clip.playbackWeight = 0f;
                        clip.Leave();
                        clip.playbackEnabled = false;
                    }
                }
            }
        }

        private void Blend(AtomAnimationClip clip, float weight, float duration)
        {
            if (!clip.playbackEnabled) clip.playbackWeight = 0;
            clip.playbackEnabled = true;
            clip.playbackBlendRate = (weight - clip.playbackWeight) / duration;
        }

        #endregion

        #region Selection

        public void SelectAnimation(string animationName)
        {
            var clip = GetClip(animationName);
            if (clip == null) throw new NullReferenceException($"Could not find animation '{animationName}'. Found animations: '{string.Join("', '", clips.Select(c => c.animationName).ToArray())}'.");
            if (current == clip)
            {
                clipTime = 0;
                return;
            }

            SelectAnimation(clip);
        }

        public void SelectAnimation(AtomAnimationClip clip)
        {
            var previous = current;
            current = clip;

            if (previous != null) previous.Leave();

            if (previous.animationLayer != current.animationLayer)
                onClipsListChanged.Invoke();
            onTargetsSelectionChanged.Invoke();
            onCurrentAnimationChanged.Invoke(new CurrentAnimationChangedEventArgs
            {
                before = previous,
                after = current
            });

            if (isPlaying)
            {
                var previousMain = clips.FirstOrDefault(c => c.playbackMainInLayer && c.animationLayer == current.animationLayer);
                if (previousMain != null)
                {
                    TransitionAnimation(previousMain, current);
                }
            }
            else
            {
                clipTime = 0f;
                Sample();
            }
        }

        #endregion

        #region Transitions and sequencing

        private void TransitionAnimation(AtomAnimationClip from, AtomAnimationClip to)
        {
            if (from == null) throw new ArgumentNullException(nameof(from));
            if (to == null) throw new ArgumentNullException(nameof(to));

            from.SetNext(null, 0);
            Blend(from, 0f, to.blendInDuration);
            from.playbackMainInLayer = false;
            Blend(to, 1f, to.blendInDuration);
            to.playbackMainInLayer = true;
            if (to.playbackWeight == 0)
            {
                to.clipTime = to.loop && to.syncTransitionTime ? from.clipTime : 0f;
            }

            if (sequencing)
            {
                AssignNextAnimation(to);
            }

            if (from.animationPattern != null)
            {
                // Let the loop finish during the transition
                from.animationPattern.SetBoolParamValue("loopOnce", true);
            }

            if (to.animationPattern != null)
            {
                to.animationPattern.SetBoolParamValue("loopOnce", false);
                to.animationPattern.ResetAndPlay();
            }
        }

        private void AssignNextAnimation(AtomAnimationClip source)
        {
            if (source.nextAnimationName == null) return;
            if (clips.Count == 1) return;

            if (source.nextAnimationTime <= 0)
                return;

            var next = FindClip(source.nextAnimationName, source);

            if (next != null) source.SetNext(next.animationName, (playTime + source.nextAnimationTime).Snap());
        }

        public AtomAnimationClip FindClip(string animationName, AtomAnimationClip source)
        {
            if (animationName == RandomizeAnimationName)
            {
                var group = clips
                    .Where(c => source == null || c.animationName != source.animationName && c.animationLayer == source.animationLayer)
                    .ToList();
                if (group.Count == 0) return null;
                var idx = Random.Range(0, group.Count);
                return group[idx];
            }
            else if (animationName.EndsWith(RandomizeGroupSuffix))
            {
                var prefix = animationName.Substring(0, animationName.Length - RandomizeGroupSuffix.Length);
                var group = clips
                    .Where(c => source == null || c.animationName != source.animationName && c.animationLayer == source.animationLayer)
                    .Where(c => c.animationName.StartsWith(prefix))
                    .ToList();
                if (group.Count == 0) return null;
                var idx = Random.Range(0, group.Count);
                return group[idx];
            }
            else
            {
                return GetClip(animationName);
            }
        }

        #endregion

        #region Sampling

        public void Sample(bool ignoreRebuild = false)
        {
            if (isPlaying || !enabled) return;

            if (!ignoreRebuild && (_animationRebuildRequestPending || _animationRebuildInProgress))
            {
                _sampleAfterRebuild = true;
                return;
            }

            foreach (var clip in GetMainClipPerLayer())
            {
                clip.playbackEnabled = true;
                clip.playbackWeight = 1f;
            }
            isSampling = true;
            try
            {
                SampleTriggers();
                SampleFloatParams();
                SampleControllers();
            }
            finally
            {
                isSampling = false;
            }
            foreach (var clip in GetMainClipPerLayer())
            {
                clip.playbackEnabled = false;
                clip.playbackWeight = 0f;
            }
        }

        private void SampleTriggers()
        {
            foreach (var clip in clips)
            {
                if (!clip.playbackEnabled) continue;
                foreach (var target in clip.targetTriggers)
                {
                    target.Sample(clip.clipTime);
                }
            }
        }

        private void SampleFloatParams()
        {
            foreach (var clip in clips)
            {
                if (!clip.playbackEnabled) continue;
                foreach (var target in clip.targetFloatParams)
                {
                    target.Sample(clip.clipTime, clip.playbackWeight);
                }
            }
        }

        private void SampleControllers()
        {
            foreach (var clip in clips)
            {
                if (!clip.playbackEnabled) continue;
                foreach (var target in clip.targetControllers)
                {
                    target.Sample(clip.clipTime, clip.playbackWeight);
                }
            }
        }

        #endregion

        #region Animation Rebuilding

        private IEnumerator RebuildDeferred()
        {
            yield return new WaitForEndOfFrame();
            RebuildAnimationNow();
        }

        public void RebuildAnimationNow()
        {
            if (_animationRebuildInProgress) throw new InvalidOperationException($"A rebuild is already in progress. This is usually caused by by RebuildAnimation triggering dirty (internal error).");
            _animationRebuildRequestPending = false;
            _animationRebuildInProgress = true;
            try
            {
                RebuildAnimationNowImpl();
            }
            catch (Exception exc)
            {
                SuperController.LogError($"Timeline.{nameof(AtomAnimation)}.{nameof(RebuildAnimationNow)}: " + exc);
            }
            finally
            {
                _animationRebuildInProgress = false;
            }
        }

        private void RebuildAnimationNowImpl()
        {
            if (current == null) throw new NullReferenceException("No current animation set");
            var sw = Stopwatch.StartNew();
            foreach (var clip in clips)
            {
                clip.Validate();
                RebuildClip(clip);
            }
            foreach (var clip in clips)
            {
                RebuildTransition(clip);
            }
            foreach (var clip in clips)
            {
                if (!clip.IsDirty()) continue;

                foreach (var target in clip.GetAllTargets())
                {
                    target.dirty = false;
                    target.onAnimationKeyframesRebuilt.Invoke();
                }

                clip.onAnimationKeyframesRebuilt.Invoke();
            }
            if (sw.ElapsedMilliseconds > 1000)
            {
                SuperController.LogError($"Timeline.{nameof(RebuildAnimationNowImpl)}: Suspiciously long animation rebuild ({sw.Elapsed})");
            }

            if (_sampleAfterRebuild)
            {
                _sampleAfterRebuild = false;
                Sample(true);
            }

            onAnimationRebuilt.Invoke();
        }

        private void RebuildTransition(AtomAnimationClip clip)
        {
            bool realign = false;
            if (clip.autoTransitionPrevious)
            {
                var previous = clips.FirstOrDefault(c => c.nextAnimationName == clip.animationName);
                if (previous != null && (previous.IsDirty() || clip.IsDirty()))
                {
                    CopySourceFrameToClip(previous, previous.animationLength, clip, 0f);
                    realign = true;
                }
            }
            if (clip.autoTransitionNext)
            {
                var next = GetClip(clip.nextAnimationName);
                if (next != null && (next.IsDirty() || clip.IsDirty()))
                {
                    CopySourceFrameToClip(next, 0f, clip, clip.animationLength);
                    realign = true;
                }
            }
            if (realign)
            {
                foreach (var target in clip.targetControllers)
                {
                    if (clip.ensureQuaternionContinuity)
                    {
                        UnitySpecific.EnsureQuaternionContinuityAndRecalculateSlope(
                            target.rotX,
                            target.rotY,
                            target.rotZ,
                            target.rotW);
                    }
                }
            }
        }

        private void CopySourceFrameToClip(AtomAnimationClip source, float sourceTime, AtomAnimationClip clip, float clipTime)
        {
            foreach (var sourceTarget in source.targetControllers)
            {
                if (!sourceTarget.EnsureParentAvailable()) continue;
                var currentTarget = clip.targetControllers.FirstOrDefault(t => t.TargetsSameAs(sourceTarget));
                if (currentTarget == null) continue;
                if (!currentTarget.EnsureParentAvailable()) continue;
                var sourceParent = sourceTarget.GetParent();
                var currentParent = currentTarget.GetParent();
                if (sourceParent == currentParent)
                {
                    currentTarget.SetCurveSnapshot(clipTime, sourceTarget.GetCurveSnapshot(sourceTime), false);
                }
                else
                {
                    var position = sourceParent.TransformPoint(sourceTarget.EvaluatePosition(sourceTime));
                    var rotation = Quaternion.Inverse(sourceParent.rotation) * sourceTarget.EvaluateRotation(sourceTime);
                    currentTarget.SetKeyframe(clipTime, currentParent.TransformPoint(position), Quaternion.Inverse(currentParent.rotation) * rotation, CurveTypeValues.Undefined, false);
                }
            }
            foreach (var sourceTarget in source.targetFloatParams)
            {
                var currentTarget = clip.targetFloatParams.FirstOrDefault(t => t.TargetsSameAs(sourceTarget));
                if (currentTarget == null) continue;
                currentTarget.value.SetKeySnapshot(clipTime, sourceTarget.value.GetKeyframeAt(sourceTime));
            }
        }

        private void RebuildClip(AtomAnimationClip clip)
        {
            foreach (var target in clip.targetControllers)
            {
                if (!target.dirty) continue;

                if (clip.loop)
                    target.SetCurveSnapshot(clip.animationLength, target.GetCurveSnapshot(0f), false);

                target.ComputeCurves(clip.loop);

                if (clip.ensureQuaternionContinuity)
                {
                    UnitySpecific.EnsureQuaternionContinuityAndRecalculateSlope(
                        target.rotX,
                        target.rotY,
                        target.rotZ,
                        target.rotW);
                }

                foreach (var curve in target.GetCurves())
                    curve.ComputeCurves();
            }

            foreach (var target in clip.targetFloatParams)
            {
                if (!target.dirty) continue;

                if (clip.loop)
                    target.SetCurveSnapshot(clip.animationLength, target.GetCurveSnapshot(0), false);

                target.value.ComputeCurves();
            }

            foreach (var target in clip.targetTriggers)
            {
                if (!target.dirty) continue;

                target.RebuildKeyframes(clip.animationLength);
            }
        }

        #endregion

        #region Event Handlers

        private void OnTargetsSelectionChanged()
        {
            foreach (var target in current.GetAllTargets())
            {
                foreach (var clip in clips.Where(c => c != current))
                {
                    var t = clip.GetAllTargets().FirstOrDefault(x => x.TargetsSameAs(target));
                    if (t == null) continue;
                    t.selected = target.selected;
                }
            }

            onTargetsSelectionChanged.Invoke();
        }

        private void OnAnimationSettingsChanged(string param)
        {
            onAnimationSettingsChanged.Invoke();
            if (param == nameof(AtomAnimationClip.animationName) || param == nameof(AtomAnimationClip.animationLayer))
                onClipsListChanged.Invoke();
        }

        private void OnAnimationKeyframesDirty()
        {
            if (_animationRebuildInProgress) throw new InvalidOperationException($"A rebuild is already in progress. This is usually caused by by RebuildAnimation triggering dirty (internal error).");
            if (_animationRebuildRequestPending) return;
            _animationRebuildRequestPending = true;
            StartCoroutine(RebuildDeferred());
        }

        #endregion

        #region Unity Lifecycle

        public void Update()
        {
            if (!allowAnimationProcessing) return;

            SampleFloatParams();
            SampleTriggers();
            ProcessAnimationSequence();
        }

        private void ProcessAnimationSequence()
        {
            foreach (var clip in clips)
            {
                if (clip.playbackMainInLayer && clip.playbackScheduledNextAnimationName != null && playTime >= clip.playbackScheduledNextTime)
                {
                    var nextAnimationName = clip.playbackScheduledNextAnimationName;
                    clip.playbackScheduledNextAnimationName = null;
                    clip.playbackScheduledNextTime = 0f;
                    var nextClip = GetClip(nextAnimationName);
                    if (nextClip == null)
                    {
                        SuperController.LogError($"Timeline: Cannot sequence from animation '{clip.animationName}' to '{nextAnimationName}' because the target animation does not exist.");
                        continue;
                    }
                    TransitionAnimation(clip, nextClip);
                }

                if (!clip.loop && clip.playbackEnabled && clip.clipTime == clip.animationLength)
                {
                    clip.playbackEnabled = false;
                }
            }
        }

        public void FixedUpdate()
        {
            if (!allowAnimationProcessing) return;

            SetPlayTime(playTime + Time.fixedDeltaTime * _speed);

            SampleControllers();
        }

        public void OnDisable()
        {
        }

        public void OnDestroy()
        {
            onTimeChanged.RemoveAllListeners();
            onCurrentAnimationChanged.RemoveAllListeners();
            onAnimationSettingsChanged.RemoveAllListeners();
            onIsPlayingChanged.RemoveAllListeners();
            onEditorSettingsChanged.RemoveAllListeners();
            onSpeedChanged.RemoveAllListeners();
            onClipsListChanged.RemoveAllListeners();
            onAnimationRebuilt.RemoveAllListeners();
            foreach (var clip in clips)
            {
                clip.Dispose();
            }
        }

        #endregion
    }
}
