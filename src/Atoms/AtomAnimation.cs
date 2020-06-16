using System;
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
    public class AtomAnimation : IDisposable
    {
        public struct TimeChangedEventArgs { public float time; public float currentClipTime; }
        public class TimeChangedEvent : UnityEvent<TimeChangedEventArgs> { }
        public class CurrentAnimationChangedEventArgs { public AtomAnimationClip before; public AtomAnimationClip after; }
        public class CurrentAnimationChangedEvent : UnityEvent<CurrentAnimationChangedEventArgs> { }

        public const float PaddingBeforeLoopFrame = 0.001f;
        public const string RandomizeAnimationName = "(Randomize)";
        public const string RandomizeGroupSuffix = "/*";
        public const float PlayBlendDuration = 0.25f;

        private readonly Atom _atom;
        public readonly AtomPlaybackState state = new AtomPlaybackState();
        private AtomAnimationClip _current;

        public TimeChangedEvent onTimeChanged = new TimeChangedEvent();
        public UnityEvent onAnimationRebuildRequested = new UnityEvent();
        public CurrentAnimationChangedEvent onCurrentAnimationChanged = new CurrentAnimationChangedEvent();
        public UnityEvent onAnimationSettingsChanged = new UnityEvent();
        public UnityEvent onClipsListChanged = new UnityEvent();

        public TimeChangedEventArgs timeArgs => new TimeChangedEventArgs { time = state.playTime, currentClipTime = currentClipState.clipTime };
        public List<AtomAnimationClip> clips { get; } = new List<AtomAnimationClip>();
        public AtomClipPlaybackState currentClipState { get; private set; }
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
                if (current == null) return;
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
                if (current == null) return;
                Sample();
                if (current.animationPattern != null)
                    current.animationPattern.SetFloatParamValue("currentTime", state.playTime);
                onTimeChanged.Invoke(timeArgs);
            }
        }

        public float speed
        {
            get
            {
                return state.speed;
            }

            set
            {
                if (value <= 0) throw new InvalidOperationException();
                state.speed = value;
                foreach (var clip in clips)
                {
                    if (clip.animationPattern != null)
                        clip.animationPattern.SetFloatParamValue("speed", value);
                }
            }
        }

        public AtomAnimation(Atom atom)
        {
            if (atom == null) throw new ArgumentNullException(nameof(atom));
            _atom = atom;
        }

        public void Initialize()
        {
            if (clips.Count == 0)
                AddClip(new AtomAnimationClip("Anim 1", AtomAnimationClip.DefaultAnimationLayer));
            if (current == null)
                current = clips.First();
            RebuildAnimation();
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
        }

        public void RemoveClip(AtomAnimationClip clip)
        {
            clips.Remove(clip);
            state.clips.Remove(new AtomClipPlaybackState(clip));
            clip.Dispose();
            onClipsListChanged.Invoke();
            OnAnimationModified();
        }

        private void OnAnimationSettingsModified(string param)
        {
            onAnimationSettingsChanged.Invoke();
            if (param == nameof(AtomAnimationClip.animationName) || param == nameof(AtomAnimationClip.animationLayer))
                onClipsListChanged.Invoke();
        }

        private void OnAnimationModified()
        {
            onAnimationRebuildRequested.Invoke();
        }

        public bool IsEmpty()
        {
            if (clips.Count == 0) return true;
            if (clips.Count == 1 && clips[0].IsEmpty()) return true;
            return false;
        }

        protected string GetNewAnimationName()
        {
            for (var i = clips.Count + 1; i < 999; i++)
            {
                var animationName = "Anim " + i;
                if (!clips.Any(c => c.animationName == animationName)) return animationName;
            }
            return Guid.NewGuid().ToString();
        }

        public void SetKeyframe(FloatParamAnimationTarget target, float time, float val)
        {
            time = time.Snap();
            if (time > current.animationLength)
                time = current.animationLength;
            target.SetKeyframe(time, val);
        }

        public void SetKeyframeToCurrentTransform(FreeControllerAnimationTarget target, float time)
        {
            time = time.Snap();
            if (time > current.animationLength)
                time = current.animationLength;
            target.SetKeyframeToCurrentTransform(time);
        }

        public AtomAnimationClip GetClip(string name)
        {
            return clips.FirstOrDefault(c => c.animationName == name);
        }

        public void Play(string animationName = null, bool sequencing = true)
        {
            if (animationName == null && current == null)
            {
                SuperController.LogError($"VamTimeline: Cannot play animation, Timeline is still loading");
                return;
            }
            var clip = animationName != null ? GetClip(animationName) : current;
            var clipState = state.GetClip(clip.animationName);
            if (state.isPlaying)
            {
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
            }
            else
            {
                state.Blend(clipState, 1f, PlayBlendDuration);
                clipState.mainInLayer = true;
            }
            state.isPlaying = true;
            if (clip.animationPattern)
            {
                clip.animationPattern.SetBoolParamValue("loopOnce", false);
                clip.animationPattern.ResetAndPlay();
            }
            if (sequencing && clip.nextAnimationName != null)
                AssignNextAnimation(clipState);
        }

        public void Stop(string animationName = null)
        {
            if (animationName != null)
            {
                var clip = state.GetClip(animationName);
                clip.Reset(false);

                if (state.clips.Any(c => c.mainInLayer))
                    return;
            }

            state.isPlaying = false;
            if (current == null) return;
            foreach (var clip in clips)
            {
                if (clip.animationPattern)
                {
                    clip.animationPattern.SetBoolParamValue("loopOnce", true);
                }
            }
            state.Reset(false);
            playTime = playTime.Snap();
        }

        public bool IsPlaying()
        {
            return state.isPlaying;
        }

        private void AssignNextAnimation(AtomClipPlaybackState clipState)
        {
            if (clipState.clip.nextAnimationName == null) return;
            if (clips.Count == 1) return;

            if (clipState.clip.nextAnimationTime < 0 + float.Epsilon)
                return;

            var nextTime = (playTime + current.nextAnimationTime).Snap();

            if (current.nextAnimationName == RandomizeAnimationName)
            {
                var idx = Random.Range(0, clips.Count - 1);
                if (idx >= clips.IndexOf(current)) idx += 1;
                clipState.SetNext(clips[idx].animationName, nextTime);
            }
            else if (current.nextAnimationName.EndsWith(RandomizeGroupSuffix))
            {
                var prefix = current.nextAnimationName.Substring(0, current.nextAnimationName.Length - RandomizeGroupSuffix.Length);
                var group = clips
                    .Where(c => c.animationName != current.animationName)
                    .Where(c => c.animationName.StartsWith(prefix))
                    .ToList();
                var idx = Random.Range(0, group.Count);
                clipState.SetNext(group[idx].animationName, nextTime);
            }
            else
            {
                clipState.SetNext(current.nextAnimationName, nextTime);
            }
        }

        public void Sample()
        {
            if (state.isPlaying) return;
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
                foreach (var target in current.targetFloatParams)
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
                    var rb = target.controller.GetComponent<Rigidbody>();
                    var position = Vector3.Lerp(rb.transform.localPosition, target.EvaluatePosition(clip.clipTime), clip.weight);
                    var rotation = Quaternion.Slerp(rb.transform.localRotation, target.EvaluateRotation(clip.clipTime), clip.weight);
                    // TODO: Store in the target
                    rb.transform.localRotation = rotation;
                    rb.transform.localPosition = position;
                }
            }
        }

        public void Update()
        {
            if (state.isPlaying)
            {
                SampleParamsAnimation();

                foreach (var clip in state.clips)
                {
                    if (clip.sequencing && state.playTime >= clip.nextTime)
                    {
                        TransitionAnimation(clip, state.GetClip(clip.nextAnimationName));
                    }
                }
            }
        }

        public void FixedUpdate()
        {
            if (state.isPlaying)
            {
                state.playTime += Time.fixedDeltaTime * state.speed;

                SampleControllers();
            }
        }

        public void RebuildAnimation()
        {
            if (current == null) throw new NullReferenceException("No current animation set");
            var sw = Stopwatch.StartNew();
            foreach (var clip in clips)
            {
                clip.Validate();
                PrepareClipCurves(clip);
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

        private void PrepareClipCurves(AtomAnimationClip clip)
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

        private bool HasAnimatableControllers()
        {
            return current.targetControllers.Count > 0;
        }

        public AtomAnimationClip AddAnimation(string animationLayer)
        {
            string animationName = GetNewAnimationName();
            var clip = new AtomAnimationClip(animationName, animationLayer);
            AddClip(clip);
            return clip;
        }

        public void TransitionAnimation(AtomClipPlaybackState from, AtomClipPlaybackState to)
        {
            if (from == null) throw new ArgumentNullException(nameof(from));
            if (to == null) throw new ArgumentNullException(nameof(to));

            state.Blend(from, 0f, current.blendDuration);
            from.mainInLayer = false;
            state.Blend(to, 1f, current.blendDuration);
            to.mainInLayer = true;

            if (from.sequencing)
            {
                from.sequencing = false;
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
                    TransitionAnimation(previousMain, currentClipState);
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

        public void Dispose()
        {
            onTimeChanged.RemoveAllListeners();
            onAnimationRebuildRequested.RemoveAllListeners();
            onCurrentAnimationChanged.RemoveAllListeners();
            onAnimationSettingsChanged.RemoveAllListeners();
            foreach (var clip in clips)
            {
                clip.Dispose();
            }
        }
    }
}
