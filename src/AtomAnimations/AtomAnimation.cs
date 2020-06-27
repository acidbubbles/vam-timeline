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
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class AtomAnimation : MonoBehaviour
    {
        public struct TimeChangedEventArgs { public float time; public float currentClipTime; }
        public class TimeChangedEvent : UnityEvent<TimeChangedEventArgs> { }
        public class CurrentAnimationChangedEventArgs { public AtomAnimationClip before; public AtomAnimationClip after; }
        public class CurrentAnimationChangedEvent : UnityEvent<CurrentAnimationChangedEventArgs> { }

        public const float PaddingBeforeLoopFrame = 0.001f;
        public const string RandomizeAnimationName = "(Randomize)";
        public const string RandomizeGroupSuffix = "/*";
        public const float PlayBlendDuration = 0.25f;

        public TimeChangedEvent onTimeChanged = new TimeChangedEvent();
        public CurrentAnimationChangedEvent onCurrentAnimationChanged = new CurrentAnimationChangedEvent();
        public UnityEvent onAnimationSettingsChanged = new UnityEvent();
        public UnityEvent onClipsListChanged = new UnityEvent();
        public UnityEvent onTargetsSelectionChanged = new UnityEvent();

        public readonly AtomPlaybackState state = new AtomPlaybackState();
        public AtomClipPlaybackState currentClipState { get; private set; }
        public List<AtomAnimationClip> clips { get; } = new List<AtomAnimationClip>();
        public TimeChangedEventArgs timeArgs => new TimeChangedEventArgs { time = state.playTime, currentClipTime = currentClipState.clipTime };
        public bool isPlaying => state.isPlaying;

        private AtomAnimationClip _current;
        public AtomAnimationClip current
        {
            get
            {
                return _current;
            }
            set
            {
                var previous = _current;
                _current = value;
                currentClipState = state.GetClip(value.animationName);
                onCurrentAnimationChanged.Invoke(new CurrentAnimationChangedEventArgs { before = previous, after = _current });
                onTargetsSelectionChanged.Invoke();
            }
        }

        public float clipTime
        {
            get
            {
                return currentClipState.clipTime;
            }
            set
            {
                state.playTime = value;
                if (currentClipState == null) return;
                currentClipState.clipTime = value;
                Sample();
                if (current.animationPattern != null)
                    current.animationPattern.SetFloatParamValue("currentTime", state.playTime);
                onTimeChanged.Invoke(timeArgs);
            }
        }

        public float playTime
        {
            get
            {
                return state.playTime;
            }
            set
            {
                state.playTime = value;
                if (!currentClipState.enabled)
                    currentClipState.clipTime = value;
                Sample();
                foreach (var clipState in state.clips)
                {
                    if (clipState.clip.animationPattern != null)
                        clipState.clip.animationPattern.SetFloatParamValue("currentTime", clipState.clipTime);
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
                if (value <= 0) throw new InvalidOperationException();
                _speed = value;
                foreach (var clip in clips)
                {
                    if (clip.animationPattern != null)
                        clip.animationPattern.SetFloatParamValue("speed", value);
                }
            }
        }
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
            if (current == null)
                current = clips.First();
            RebuildAnimation();
        }

        public bool IsEmpty()
        {
            if (clips.Count == 0) return true;
            if (clips.Count == 1 && clips[0].IsEmpty()) return true;
            return false;
        }

        public void SetKeyframeToCurrentTransform(FreeControllerAnimationTarget target, float time)
        {
            time = time.Snap();
            if (time > current.animationLength)
                time = current.animationLength;
            target.SetKeyframeToCurrentTransform(time);
        }

        #region Clips

        public AtomAnimationClip GetClip(string name)
        {
            return clips.FirstOrDefault(c => c.animationName == name);
        }

        public void AddClip(AtomAnimationClip clip)
        {
            var lastIndexOfLayer = clips.FindLastIndex(c => c.animationLayer == clip.animationLayer);
            if (lastIndexOfLayer == -1)
                clips.Add(clip);
            else
                clips.Insert(lastIndexOfLayer + 1, clip);
            state.clips.Add(new AtomClipPlaybackState(clip));
            clip.onAnimationSettingsModified.AddListener(OnAnimationSettingsModified);
            clip.onAnimationKeyframesModified.AddListener(OnAnimationModified);
            clip.onTargetsListChanged.AddListener(OnAnimationModified);
            clip.onTargetsSelectionChanged.AddListener(OnTargetsSelectionChanged);
            onClipsListChanged.Invoke();
        }

        public AtomAnimationClip CreateClip(string animationLayer)
        {
            string animationName = GetNewAnimationName();
            var clip = new AtomAnimationClip(animationName, animationLayer);
            AddClip(clip);
            return clip;
        }

        public void RemoveClip(AtomAnimationClip clip)
        {
            clips.Remove(clip);
            state.clips.Remove(new AtomClipPlaybackState(clip));
            clip.Dispose();
            onClipsListChanged.Invoke();
            OnAnimationModified();
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

        #endregion

        #region Playback

        public void PlayClip(string animationName, bool sequencing)
        {
            var clipState = state.GetClip(animationName);
            if (clipState.enabled && clipState.mainInLayer) return;
            var clip = clipState.clip;
            if (!state.isPlaying)
            {
                state.isPlaying = true;
                state.sequencing = sequencing || state.sequencing;
            }
            var previousMain = state.clips.FirstOrDefault(c => c.mainInLayer && c.clip.animationLayer == clip.animationLayer);
            if (previousMain != null && previousMain != clipState)
            {
                TransitionAnimation(previousMain, clipState);
            }
            else
            {
                state.Blend(clipState, 1f, PlayBlendDuration);
                clipState.mainInLayer = true;
            }
            if (clip.animationPattern)
            {
                clip.animationPattern.SetBoolParamValue("loopOnce", false);
                clip.animationPattern.ResetAndPlay();
            }
            if (sequencing && clip.nextAnimationName != null)
                AssignNextAnimation(clipState);
        }

        public void PlayAll()
        {
            var firstOrMainPerLayer = state.clips
                .GroupBy(c => c.clip.animationLayer)
                .Select(g => g.FirstOrDefault(c => c.mainInLayer) ?? g.First());

            foreach (var clipState in firstOrMainPerLayer)
            {
                if (clipState.clip.animationLayer == current.animationLayer)
                    PlayClip(current.animationName, true);
                else
                    PlayClip(clipState.clip.animationName, true);
            }
        }

        public void StopClip(string animationName)
        {
            var clipState = state.GetClip(animationName);
            clipState.Reset(false);
            if (clipState.clip.animationPattern)
                clipState.clip.animationPattern.SetBoolParamValue("loopOnce", true);

            if (!state.clips.Any(c => c.mainInLayer))
                state.isPlaying = false;
        }

        public void StopAll()
        {
            state.isPlaying = false;

            foreach (var clip in state.clips)
            {
                if (clip.enabled)
                    StopClip(clip.clip.animationName);
            }

            state.Reset(false);
            playTime = playTime.Snap();
        }

        public void Reset()
        {
            state.isPlaying = false;
            state.Reset(true);
            playTime = 0f;
        }

        #endregion

        #region Selection

        public void SelectAnimation(string animationName)
        {
            var previous = current;
            var previousClipState = currentClipState;
            current = GetClip(animationName);

            if (current == null) throw new NullReferenceException($"Could not find animation '{animationName}'. Found animations: '{string.Join("', '", clips.Select(c => c.animationName).ToArray())}'.");
            if (state.isPlaying)
            {
                var previousMain = state.clips.FirstOrDefault(c => c.mainInLayer && c.clip.animationLayer == current.animationLayer);
                if (previousMain != null)
                {
                    TransitionAnimation(previousMain, currentClipState);
                }
            }
            else
            {
                Sample();
            }

            if (previous.animationLayer != current.animationLayer)
                onClipsListChanged.Invoke();
            onCurrentAnimationChanged.Invoke(new CurrentAnimationChangedEventArgs
            {
                before = previous,
                after = current
            });
        }

        #endregion

        #region Transitions and sequencing

        private void TransitionAnimation(AtomClipPlaybackState from, AtomClipPlaybackState to)
        {
            if (from == null) throw new ArgumentNullException(nameof(from));
            if (to == null) throw new ArgumentNullException(nameof(to));

            from.SetNext(null, 0);
            state.Blend(from, 0f, current.blendDuration);
            from.mainInLayer = false;
            state.Blend(to, 1f, current.blendDuration);
            to.mainInLayer = true;
            if (to.weight == 0) to.clipTime = 0f;

            if (state.sequencing)
            {
                AssignNextAnimation(to);
            }

            if (from.clip.animationPattern != null)
            {
                // Let the loop finish during the transition
                from.clip.animationPattern.SetBoolParamValue("loopOnce", true);
            }

            if (to.clip.animationPattern != null)
            {
                to.clip.animationPattern.SetBoolParamValue("loopOnce", false);
                to.clip.animationPattern.ResetAndPlay();
            }
        }

        private void AssignNextAnimation(AtomClipPlaybackState clipState)
        {
            var clip = clipState.clip;
            if (clip.nextAnimationName == null) return;
            if (clips.Count == 1) return;

            if (clip.nextAnimationTime < 0 + float.Epsilon)
                return;

            var nextTime = (playTime + clip.nextAnimationTime).Snap();

            if (clip.nextAnimationName == RandomizeAnimationName)
            {
                var idx = Random.Range(0, clips.Count - 1);
                if (idx >= clips.IndexOf(clip)) idx += 1;
                clipState.SetNext(clips[idx].animationName, nextTime);
            }
            else if (clip.nextAnimationName.EndsWith(RandomizeGroupSuffix))
            {
                var prefix = clip.nextAnimationName.Substring(0, clip.nextAnimationName.Length - RandomizeGroupSuffix.Length);
                var group = clips
                    .Where(c => c.animationName != clip.animationName)
                    .Where(c => c.animationName.StartsWith(prefix))
                    .ToList();
                var idx = Random.Range(0, group.Count);
                clipState.SetNext(group[idx].animationName, nextTime);
            }
            else
            {
                clipState.SetNext(clip.nextAnimationName, nextTime);
            }
        }

        #endregion

        #region Sampling

        public void Sample(bool force = false)
        {
            if (state.isPlaying) return;

            if (!force && (_animationRebuildRequestPending || _animationRebuildInProgress))
                _sampleAfterRebuild = true;

            currentClipState.enabled = true;
            currentClipState.weight = 1f;
            SampleParamsAnimation();
            SampleControllers();
            currentClipState.enabled = false;
            currentClipState.weight = 0f;
        }

        private void SampleParamsAnimation()
        {
            foreach (var clip in state.clips)
            {
                if (!clip.enabled) continue;
                foreach (var target in clip.clip.targetFloatParams)
                {
                    target.floatParam.val = Mathf.Lerp(target.floatParam.val, target.value.Evaluate(clip.clipTime), clip.weight);
                }
            }
        }

        private void SampleControllers()
        {
            foreach (var clip in state.clips)
            {
                if (!clip.enabled) continue;
                foreach (var target in clip.clip.targetControllers)
                {
                    var control = target.controller.control;

                    var rotState = target.controller.currentRotationState;
                    if (rotState == FreeControllerV3.RotationState.On)
                    {
                        var localRotation = Quaternion.Slerp(control.localRotation, target.EvaluateRotation(clip.clipTime), clip.weight);
                        control.localRotation = localRotation;
                        // control.rotation = target.controller.linkToRB.rotation * localRotation;
                    }

                    var posState = target.controller.currentPositionState;
                    if (posState == FreeControllerV3.PositionState.On)
                    {
                        var localPosition = Vector3.Lerp(control.localPosition, target.EvaluatePosition(clip.clipTime), clip.weight);
                        control.localPosition = localPosition;
                        // control.position = target.controller.linkToRB.position + Vector3.Scale(localPosition, control.transform.localScale);
                    }
                }
            }
        }

        #endregion

        #region Animation Rebuilding

        private IEnumerator RebuildDeferred()
        {
            yield return new WaitForEndOfFrame();
            _animationRebuildRequestPending = false;
            try
            {
                _animationRebuildInProgress = true;
                RebuildAnimation();
                if (_sampleAfterRebuild)
                {
                    _sampleAfterRebuild = false;
                    Sample(true);
                }
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AtomAnimation)}.{nameof(RebuildDeferred)}: " + exc);
            }
            finally
            {
                _animationRebuildInProgress = false;
            }
        }

        public void RebuildAnimation()
        {
            if (current == null) throw new NullReferenceException("No current animation set");
            var sw = Stopwatch.StartNew();
            foreach (var clip in clips)
            {
                clip.Validate();
                RebuildClip(clip);
                if (clip.transition)
                {
                    var previous = GetClip(clip.animationName);
                    if (previous != null && (previous.IsDirty() || clip.IsDirty()))
                        clip.Paste(0f, previous.Copy(previous.animationLength, true), false);
                    var next = GetClip(clip.nextAnimationName);
                    if (next != null && (next.IsDirty() || clip.IsDirty()))
                        clip.Paste(clip.animationLength, next.Copy(0f, true), false);
                }
            }
            if (sw.ElapsedMilliseconds > 1000)
            {
                SuperController.LogError($"VamTimeline.{nameof(RebuildAnimation)}: Suspiciously long animation rebuild ({sw.Elapsed})");
            }
        }

        private void RebuildClip(AtomAnimationClip clip)
        {
            foreach (var target in clip.targetControllers)
            {
                if (!target.dirty) continue;

                target.dirty = false;

                if (clip.loop)
                    target.SetCurveSnapshot(clip.animationLength, target.GetCurveSnapshot(0f), false);

                target.ReapplyCurveTypes();

                if (clip.loop)
                    target.SmoothLoop();

                if (clip.ensureQuaternionContinuity)
                {
                    UnitySpecific.EnsureQuaternionContinuityAndRecalculateSlope(
                        target.rotX,
                        target.rotY,
                        target.rotZ,
                        target.rotW);
                }
            }

            foreach (var target in clip.targetFloatParams)
            {
                if (!target.dirty) continue;

                target.dirty = false;

                if (clip.loop)
                    target.value.SetKeyframe(clip.animationLength, target.value[0].value);

                target.value.FlatAllFrames();
            }
        }

        #endregion

        #region Event Handlers

        private void OnTargetsSelectionChanged()
        {
            foreach (var target in current.allTargets)
            {
                foreach (var clip in clips.Where(c => c != current))
                {
                    var t = clip.allTargets.FirstOrDefault(x => x.TargetsSameAs(target));
                    if (t == null) continue;
                    t.selected = target.selected;
                }
            }

            onTargetsSelectionChanged.Invoke();
        }

        private void OnAnimationSettingsModified(string param)
        {
            onAnimationSettingsChanged.Invoke();
            if (param == nameof(AtomAnimationClip.animationName) || param == nameof(AtomAnimationClip.animationLayer))
                onClipsListChanged.Invoke();
        }

        private void OnAnimationModified()
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
            if (!state.isPlaying) return;

            SampleParamsAnimation();

            foreach (var clip in state.clips)
            {
                if (clip.nextAnimationName != null && state.playTime >= clip.nextTime)
                {
                    TransitionAnimation(clip, state.GetClip(clip.nextAnimationName));
                }
            }
        }

        public void FixedUpdate()
        {
            if (!state.isPlaying) return;

            state.playTime += Time.fixedDeltaTime * _speed;

            SampleControllers();
        }

        public void OnDisable()
        {
            StopAll();
        }

        public void OnDestroy()
        {
            onTimeChanged.RemoveAllListeners();
            onCurrentAnimationChanged.RemoveAllListeners();
            onAnimationSettingsChanged.RemoveAllListeners();
            foreach (var clip in clips)
            {
                clip.Dispose();
            }
        }

        #endregion
    }
}
