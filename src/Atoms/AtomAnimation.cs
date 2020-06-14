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
        public class TimeChangedEvent : UnityEvent<float> { }
        public class CurrentAnimationChangedEventArgs { public AtomAnimationClip before; public AtomAnimationClip after; }
        public class CurrentAnimationChangedEvent : UnityEvent<CurrentAnimationChangedEventArgs> { }

        public const float PaddingBeforeLoopFrame = 0.001f;
        public const string RandomizeAnimationName = "(Randomize)";
        public const string RandomizeGroupSuffix = "/*";

        private readonly Atom _atom;
        private readonly AtomPlaybackState _state = new AtomPlaybackState();
        private AtomAnimationClip _current;

        public TimeChangedEvent onTimeChanged = new TimeChangedEvent();
        public UnityEvent onAnimationRebuildRequested = new UnityEvent();
        public CurrentAnimationChangedEvent onCurrentAnimationChanged = new CurrentAnimationChangedEvent();
        public UnityEvent onAnimationSettingsChanged = new UnityEvent();
        public UnityEvent onClipsListChanged = new UnityEvent();
        public List<AtomAnimationClip> clips { get; } = new List<AtomAnimationClip>();
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
                _state.current = _state.GetClip(value.animationName);
                onCurrentAnimationChanged.Invoke(new CurrentAnimationChangedEventArgs { before = previous, after = _current });
            }
        }

        public float time
        {
            get
            {
                var time = _state.time;
                if (current.loop) return time % current.animationLength;
                return time;
            }
            set
            {
                _state.time = value;
                if (current == null) return;
                Sample();
                onTimeChanged.Invoke(value);
            }
        }

        public float speed
        {
            get
            {
                return _state.speed;
            }

            set
            {
                _state.speed = value;
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
                AddClip(new AtomAnimationClip("Anim 1"));
            if (current == null)
                current = clips.First();
            RebuildAnimation();
        }

        public void AddClip(AtomAnimationClip clip)
        {
            clip.onAnimationSettingsModified.AddListener(OnAnimationSettingsModified);
            clip.onAnimationKeyframesModified.AddListener(OnAnimationModified);
            clip.onTargetsListChanged.AddListener(OnAnimationModified);
            clip.onTargetsSelectionChanged.AddListener(OnTargetsSelectionChanged);
            clips.Add(clip);
            _state.clips.Add(new AtomClipPlaybackState(clip));
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
            _state.clips.Remove(new AtomClipPlaybackState(clip));
            clip.Dispose();
            onClipsListChanged.Invoke();
            OnAnimationModified();
        }

        private void OnAnimationSettingsModified()
        {
            onAnimationSettingsChanged.Invoke();
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

        public List<string> GetAnimationNames()
        {
            var clipNames = new List<string>(clips.Count);
            for (var i = 0; i < clips.Count; i++)
                clipNames.Add(clips[i].animationName);
            return clipNames;
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

        public void Play()
        {
            if (current == null)
            {
                SuperController.LogError($"VamTimeline: Cannot play animation, Timeline is still loading");
                return;
            }
            _state.Reset(current.animationName);
            _state.Play(current.animationName);
            if (current.animationPattern)
            {
                current.animationPattern.SetBoolParamValue("loopOnce", false);
                current.animationPattern.ResetAndPlay();
            }
            DetermineNextAnimation();
        }

        private void DetermineNextAnimation()
        {
            _state.SetNext(null, 0f);

            if (current.nextAnimationName == null) return;
            if (clips.Count == 1) return;

            if (current.nextAnimationTime < 0 + float.Epsilon)
                return;

            var nextTime = (time + current.nextAnimationTime).Snap();

            if (current.nextAnimationName == RandomizeAnimationName)
            {
                var idx = Random.Range(0, clips.Count - 1);
                if (idx >= clips.IndexOf(current)) idx += 1;
                _state.SetNext(clips[idx].animationName, nextTime);
            }
            else if (current.nextAnimationName.EndsWith(RandomizeGroupSuffix))
            {
                var prefix = current.nextAnimationName.Substring(0, current.nextAnimationName.Length - RandomizeGroupSuffix.Length);
                var group = clips
                    .Where(c => c.animationName != current.animationName)
                    .Where(c => c.animationName.StartsWith(prefix))
                    .ToList();
                var idx = Random.Range(0, group.Count);
                _state.SetNext(group[idx].animationName, nextTime);
            }
            else
            {
                _state.SetNext(current.nextAnimationName, nextTime);
            }
        }

        public void Sample()
        {
            SampleParamsAnimation();
            SampleControllers();
        }

        private void SampleParamsAnimation()
        {
            foreach (var clip in _state.clips)
            {
                if (!clip.enabled) continue;
                foreach (var target in current.targetFloatParams)
                {
                    target.floatParam.val = Mathf.Lerp(target.floatParam.val, target.value.Evaluate(clip.time), clip.weight);
                }
            }
        }

        private void SampleControllers()
        {
            foreach (var clip in _state.clips)
            {
                if (!clip.enabled) continue;
                foreach (var target in current.targetControllers)
                {
                    var rb = target.controller.GetComponent<Rigidbody>();
                    var position = Vector3.Lerp(rb.transform.localPosition, target.EvaluatePosition(clip.time), clip.weight);
                    var rotation = Quaternion.Slerp(rb.transform.localRotation, target.EvaluateRotation(clip.time), clip.weight);
                    // TODO: Store in the target
                    rb.transform.localRotation = rotation;
                    rb.transform.localPosition = position;
                }
            }
        }

        public void Update()
        {
            if (_state.isPlaying)
            {
                SampleParamsAnimation();

                if (_state.nextTime > 0 + float.Epsilon && _state.time >= _state.nextTime)
                {
                    if (_state.next != null)
                    {
                        ChangeAnimation(_state.next.clip.animationName);
                    }
                }
            }
        }

        public void FixedUpdate()
        {
            if (_state.isPlaying)
            {
                _state.time += Time.fixedDeltaTime * _state.speed;

                SampleControllers();
            }
        }

        public void Stop()
        {
            _state.isPlaying = false;
            if (current == null) return;
            foreach (var clip in clips)
            {
                if (clip.animationPattern)
                {
                    clip.animationPattern.SetBoolParamValue("loopOnce", true);
                }
            }
            _state.Reset(current.animationName);
            _state.next = null;
            _state.nextTime = 0;
            if (_state.originalAnimationName != null && _state.current.clip.animationName != current.animationName)
            {
                ChangeAnimation(_state.originalAnimationName);
                _state.originalAnimationName = null;
            }
            if (time > current.animationLength - 0.001f)
            {
                time = current.loop ? 0f : current.animationLength;
            }
            else
            {
                time = time.Snap();
            }
            Sample();
        }

        public bool IsPlaying()
        {
            return _state.isPlaying;
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
                ReapplyClipCurve(clip);
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

        private void ReapplyClipCurve(AtomAnimationClip clip)
        {
            clip.alip.ClearCurves();

            foreach (var target in clip.targetControllers)
            {
                target.ReapplyCurvesToClip(clip.alip);
            }
        }

        private bool HasAnimatableControllers()
        {
            return current.targetControllers.Count > 0;
        }

        public AtomAnimationClip AddAnimation()
        {
            string animationName = GetNewAnimationName();
            var clip = new AtomAnimationClip(animationName);
            AddClip(clip);
            return clip;
        }

        public void ChangeAnimation(string animationName)
        {
            var clip = GetClip(animationName);
            if (clip == null) throw new NullReferenceException($"Could not find animation '{animationName}'. Found animations: '{string.Join("', '", clips.Select(c => c.animationName).ToArray())}'.");
            var time = this.time;
            if (_state.isPlaying)
            {
                _state.Blend(current.animationName, 0f, current.blendDuration);
                _state.Blend(animationName, 1f, current.blendDuration);
                if (current.animationPattern != null)
                {
                    // Let the loop finish during the transition
                    current.animationPattern.SetBoolParamValue("loopOnce", true);
                }
            }

            var previous = current;
            current = clip;

            if (_state.isPlaying)
            {
                DetermineNextAnimation();

                if (current.animationPattern != null)
                {
                    current.animationPattern.SetBoolParamValue("loopOnce", false);
                    current.animationPattern.ResetAndPlay();
                }
            }
            else
            {
                this.time = 0f;
                Sample();
                onCurrentAnimationChanged.Invoke(new CurrentAnimationChangedEventArgs
                {
                    before = previous,
                    after = current
                });
            }
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
