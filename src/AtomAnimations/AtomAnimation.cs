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
        public class IsPlayingEvent : UnityEvent<AtomAnimationClip> { }

        public const float PaddingBeforeLoopFrame = 0.001f;
        public const string RandomizeAnimationName = "(Randomize)";
        public const string RandomizeGroupSuffix = "/*";

        public UnityEvent onAnimationSettingsChanged = new UnityEvent();
        public UnityEvent onSpeedChanged = new UnityEvent();
        public UnityEvent onClipsListChanged = new UnityEvent();
        public UnityEvent onAnimationRebuilt = new UnityEvent();
        public IsPlayingEvent onIsPlayingChanged = new IsPlayingEvent();
        public IsPlayingEvent onClipIsPlayingChanged = new IsPlayingEvent();


        public List<AtomAnimationClip> clips { get; } = new List<AtomAnimationClip>();
        public bool isPlaying { get; private set; }
        public bool isSampling { get; private set; }
        private bool allowAnimationProcessing => isPlaying && !SuperController.singleton.freezeAnimation;

        public bool master { get; set; }

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
                foreach (var clip in clips)
                {
                    if (clip.animationPattern != null)
                        clip.animationPattern.SetFloatParamValue("currentTime", clip.clipTime);
                }
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

        public AtomAnimation()
        {
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

        public void Clear()
        {
            foreach (var clip in clips.ToList())
                RemoveClip(clip);

            speed = 1f;
            _playTime = 0f;
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
                    previousMain.SetNext(clip.animationName, Mathf.Max(previousMain.animationLength - previousMain.clipTime, 0f));
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
                    return g.FirstOrDefault(c => c.playbackMainInLayer) ?? g.FirstOrDefault(c => c.autoPlay) ?? g.First();
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
            Sample();
        }

        public void StopAndReset()
        {
            if (isPlaying) StopAll();
            ResetAll();
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
                        onClipIsPlayingChanged.Invoke(clip);
                    }
                }
            }
        }

        private void Blend(AtomAnimationClip clip, float weight, float duration)
        {
            if (!clip.playbackEnabled) clip.playbackWeight = 0;
            clip.playbackEnabled = true;
            clip.playbackBlendRate = (weight - clip.playbackWeight) / duration;
            onClipIsPlayingChanged.Invoke(clip);
        }

        #endregion

        #region Transitions and sequencing

        private void TransitionAnimation(AtomAnimationClip from, AtomAnimationClip to)
        {
            if (from == null) throw new ArgumentNullException(nameof(from));
            if (to == null) throw new ArgumentNullException(nameof(to));

            from.SetNext(null, float.NaN);
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

            if (next != null) source.SetNext(next.animationName, source.nextAnimationTime);
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

        public bool RebuildPending()
        {
            return _animationRebuildRequestPending || _animationRebuildInProgress;
        }

        public void Sample()
        {
            if (isPlaying || !enabled) return;

            SampleTriggers();
            SampleFloatParams();
            SampleControllers();
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

            onAnimationRebuilt.Invoke();
        }

        private void RebuildAnimationNowImpl()
        {
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
            ProcessAnimationSequence(Time.deltaTime * speed);
        }

        private void ProcessAnimationSequence(float deltaTime)
        {
            foreach (var clip in clips)
            {
                if (!clip.loop && clip.playbackEnabled && clip.clipTime == clip.animationLength)
                {
                    clip.playbackEnabled = false;
                    onClipIsPlayingChanged.Invoke(clip);
                }

                if (clip.playbackMainInLayer && clip.playbackScheduledNextAnimationName != null)
                {
                    clip.playbackScheduledNextTimeLeft = Mathf.Max(clip.playbackScheduledNextTimeLeft - deltaTime * clip.speed, 0f);
                    if (clip.playbackScheduledNextTimeLeft == 0)
                    {
                        var nextAnimationName = clip.playbackScheduledNextAnimationName;
                        clip.SetNext(null, float.NaN);
                        var nextClip = GetClip(nextAnimationName);
                        if (nextClip == null)
                        {
                            SuperController.LogError($"Timeline: Cannot sequence from animation '{clip.animationName}' to '{nextAnimationName}' because the target animation does not exist.");
                            continue;
                        }
                        TransitionAnimation(clip, nextClip);
                    }
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
            onAnimationSettingsChanged.RemoveAllListeners();
            onIsPlayingChanged.RemoveAllListeners();
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
