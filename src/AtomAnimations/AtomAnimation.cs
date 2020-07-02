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

        public const float PaddingBeforeLoopFrame = 0.001f;
        public const string RandomizeAnimationName = "(Randomize)";
        public const string RandomizeGroupSuffix = "/*";
        public const float PlayBlendDuration = 0.25f;

        public TimeChangedEvent onTimeChanged = new TimeChangedEvent();
        public CurrentAnimationChangedEvent onCurrentAnimationChanged = new CurrentAnimationChangedEvent();
        public UnityEvent onAnimationSettingsChanged = new UnityEvent();
        public AnimationSettingsChanged onEditorSettingsChanged = new AnimationSettingsChanged();
        public UnityEvent onSpeedChanged = new UnityEvent();
        public UnityEvent onClipsListChanged = new UnityEvent();
        public UnityEvent onTargetsSelectionChanged = new UnityEvent();

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
        private bool _locked;
        public bool locked
        {
            get { return _locked; }
            set { _locked = value; onEditorSettingsChanged.Invoke(nameof(locked)); }
        }
        #endregion

        public List<AtomAnimationClip> clips { get; } = new List<AtomAnimationClip>();
        public TimeChangedEventArgs timeArgs => new TimeChangedEventArgs { time = playTime, currentClipTime = current.clipTime };
        public bool isPlaying { get; private set; }

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
                if (previous != null) previous.Leave();
                onCurrentAnimationChanged.Invoke(new CurrentAnimationChangedEventArgs { before = previous, after = _current });
                onTargetsSelectionChanged.Invoke();
            }
        }

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
                if (!current.enabled)
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
                if (value <= 0) throw new InvalidOperationException();
                _speed = value;
                foreach (var clip in clips)
                {
                    if (clip.animationPattern != null)
                        clip.animationPattern.SetFloatParamValue("speed", value);
                }
                onEditorSettingsChanged.Invoke(nameof(speed));
            }
        }
        private bool _animationRebuildRequestPending;
        private bool _animationRebuildInProgress;
        private bool _sampleAfterRebuild;
        private bool _sequencing;

        public AtomAnimation()
        {
        }

        public void Initialize()
        {
            if (clips.Count == 0)
                AddClip(new AtomAnimationClip("Anim 1", AtomAnimationClip.DefaultAnimationLayer));
            if (current == null)
                current = clips.First();
            RebuildAnimationNow();
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
            clip.onAnimationSettingsModified.AddListener(OnAnimationSettingsModified);
            clip.onAnimationKeyframesDirty.AddListener(OnAnimationKeyframesDirty);
            clip.onTargetsListChanged.AddListener(OnAnimationKeyframesDirty);
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
            if (clips.Count == 1) yield return clips[0].animationLayer;
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
            if (clip.enabled && clip.mainInLayer) return;
            if (!isPlaying)
            {
                isPlaying = true;
                _sequencing = _sequencing || sequencing;
            }
            var previousMain = clips.FirstOrDefault(c => c.mainInLayer && c.animationLayer == clip.animationLayer);
            if (previousMain != null && previousMain != clip)
            {
                TransitionAnimation(previousMain, clip);
            }
            else
            {
                Blend(clip, 1f, PlayBlendDuration);
                clip.mainInLayer = true;
            }
            if (clip.animationPattern)
            {
                clip.animationPattern.SetBoolParamValue("loopOnce", false);
                clip.animationPattern.ResetAndPlay();
            }
            if (sequencing && clip.nextAnimationName != null)
                AssignNextAnimation(clip);
        }

        public void PlayAll()
        {
            var firstOrMainPerLayer = clips
                .GroupBy(c => c.animationLayer)
                .Select(g => g.FirstOrDefault(c => c.mainInLayer) ?? g.First());

            foreach (var clip in firstOrMainPerLayer)
            {
                if (clip.animationLayer == current.animationLayer)
                    PlayClip(current.animationName, true);
                else
                    PlayClip(clip.animationName, true);
            }
        }

        public void StopClip(string animationName)
        {
            var clip = GetClip(animationName);
            if (clip.enabled)
                clip.Leave();
            clip.Reset(false);
            if (clip.animationPattern)
                clip.animationPattern.SetBoolParamValue("loopOnce", true);

            if (!clips.Any(c => c.mainInLayer))
                isPlaying = false;
        }

        public void StopAll()
        {
            isPlaying = false;

            foreach (var clip in clips)
            {
                if (clip.enabled)
                    StopClip(clip.animationName);
            }

            Reset(false);
            playTime = playTime.Snap();
        }

        public void Reset()
        {
            isPlaying = false;
            Reset(true);
            playTime = 0f;
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
                if (!clip.enabled) continue;

                clip.clipTime += delta;
                if (clip.blendRate != 0)
                {
                    // TODO: Mathf.SmoothStep
                    clip.weight += clip.blendRate * delta;
                    if (clip.weight >= 1f)
                    {
                        clip.blendRate = 0f;
                        clip.weight = 1f;
                    }
                    else if (clip.weight <= 0f)
                    {
                        clip.blendRate = 0f;
                        clip.weight = 0f;
                        clip.Leave();
                        clip.enabled = false;
                    }
                }
            }
        }

        private void Blend(AtomAnimationClip clip, float weight, float duration)
        {
            clip.enabled = true;
            clip.blendRate = (weight - clip.weight) / duration;
        }

        private void Reset(bool resetTime)
        {
            if (resetTime) _playTime = 0f;
            foreach (var clip in clips)
            {
                clip.Reset(resetTime);
            }
        }

        #endregion

        #region Selection

        public void SelectAnimation(string animationName)
        {
            var previous = current;
            current = GetClip(animationName);

            if (current == null) throw new NullReferenceException($"Could not find animation '{animationName}'. Found animations: '{string.Join("', '", clips.Select(c => c.animationName).ToArray())}'.");

            if (isPlaying)
            {
                var previousMain = clips.FirstOrDefault(c => c.mainInLayer && c.animationLayer == current.animationLayer);
                if (previousMain != null)
                {
                    TransitionAnimation(previousMain, current);
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

        private void TransitionAnimation(AtomAnimationClip from, AtomAnimationClip to)
        {
            if (from == null) throw new ArgumentNullException(nameof(from));
            if (to == null) throw new ArgumentNullException(nameof(to));

            from.SetNext(null, 0);
            Blend(from, 0f, current.blendDuration);
            from.mainInLayer = false;
            Blend(to, 1f, current.blendDuration);
            to.mainInLayer = true;
            if (to.weight == 0) to.clipTime = 0f;

            if (_sequencing)
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

        private void AssignNextAnimation(AtomAnimationClip clip)
        {
            if (clip.nextAnimationName == null) return;
            if (clips.Count == 1) return;

            if (clip.nextAnimationTime < 0 + float.Epsilon)
                return;

            var nextTime = (playTime + clip.nextAnimationTime).Snap();

            if (clip.nextAnimationName == RandomizeAnimationName)
            {
                var idx = Random.Range(0, clips.Count - 1);
                if (idx >= clips.IndexOf(clip)) idx += 1;
                clip.SetNext(clips[idx].animationName, nextTime);
            }
            else if (clip.nextAnimationName.EndsWith(RandomizeGroupSuffix))
            {
                var prefix = clip.nextAnimationName.Substring(0, clip.nextAnimationName.Length - RandomizeGroupSuffix.Length);
                var group = clips
                    .Where(c => c.animationName != clip.animationName)
                    .Where(c => c.animationName.StartsWith(prefix))
                    .ToList();
                var idx = Random.Range(0, group.Count);
                clip.SetNext(group[idx].animationName, nextTime);
            }
            else
            {
                clip.SetNext(clip.nextAnimationName, nextTime);
            }
        }

        #endregion

        #region Sampling

        public void Sample(bool force = false)
        {
            if (isPlaying) return;

            if (!force && (_animationRebuildRequestPending || _animationRebuildInProgress))
                _sampleAfterRebuild = true;

            current.enabled = true;
            current.weight = 1f;
            SampleTriggers();
            SampleFloatParams();
            SampleControllers();
            current.enabled = false;
            current.weight = 0f;
        }

        private void SampleTriggers()
        {
            foreach (var clip in clips)
            {
                if (!clip.enabled) continue;
                foreach (var target in clip.targetTriggers)
                {
                    target.Sample(clip.previousClipTime);
                }
            }
        }

        private void SampleFloatParams()
        {
            foreach (var clip in clips)
            {
                if (!clip.enabled) continue;
                foreach (var target in clip.targetFloatParams)
                {
                    target.Sample(clip.clipTime, clip.weight);
                }
            }
        }

        private void SampleControllers()
        {
            foreach (var clip in clips)
            {
                if (!clip.enabled) continue;
                foreach (var target in clip.targetControllers)
                {
                    target.Sample(clip.clipTime, clip.weight);
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
                RebuildAnimationNow();
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

        public void RebuildAnimationNow()
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
                if (!clip.transition) continue;

                var previous = GetClip(clip.animationName);
                if (previous != null && (previous.IsDirty() || clip.IsDirty()))
                    clip.Paste(0f, previous.Copy(previous.animationLength, true), false);
                var next = GetClip(clip.nextAnimationName);
                if (next != null && (next.IsDirty() || clip.IsDirty()))
                    clip.Paste(clip.animationLength, next.Copy(0f, true), false);
            }
            foreach (var clip in clips)
            {
                if (!clip.IsDirty()) continue;

                foreach (var target in clip.allTargets)
                {
                    target.dirty = false;
                    target.onAnimationKeyframesRebuilt.Invoke();
                }

                clip.onAnimationKeyframesRebuilt.Invoke();
            }
            if (sw.ElapsedMilliseconds > 1000)
            {
                SuperController.LogError($"VamTimeline.{nameof(RebuildAnimationNow)}: Suspiciously long animation rebuild ({sw.Elapsed})");
            }
        }

        private void RebuildClip(AtomAnimationClip clip)
        {
            foreach (var target in clip.targetControllers)
            {
                if (!target.dirty) continue;

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

                if (clip.loop)
                    target.value.SetKeyframe(clip.animationLength, target.value[0].value);

                target.value.FlatAllFrames();
            }

            foreach (var target in clip.targetTriggers)
            {
                if (!target.dirty) continue;

                target.RebuildKeyframes(clip);
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
            if (!isPlaying) return;

            SampleFloatParams();
            SampleTriggers();

            foreach (var clip in clips)
            {
                if (clip.scheduledNextAnimationName != null && playTime >= clip.scheduledNextTime)
                {
                    TransitionAnimation(clip, GetClip(clip.scheduledNextAnimationName));
                }
            }
        }

        public void FixedUpdate()
        {
            if (!isPlaying) return;

            SetPlayTime(playTime + Time.fixedDeltaTime * _speed);

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
