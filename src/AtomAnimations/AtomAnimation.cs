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
        public const float PlayBlendDuration = 0.25f;

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
            current = clips[0];
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
            clip.onAnimationSettingsChanged.AddListener(OnAnimationSettingsChanged);
            clip.onAnimationKeyframesDirty.AddListener(OnAnimationKeyframesDirty);
            clip.onTargetsListChanged.AddListener(OnAnimationKeyframesDirty);
            clip.onTargetsSelectionChanged.AddListener(OnTargetsSelectionChanged);
            onClipsListChanged.Invoke();
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
                TransitionAnimation(previousMain, clip);
            }
            else
            {
                Blend(clip, 1f, PlayBlendDuration);
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

        public void PlayAll()
        {
            var firstOrMainPerLayer = clips
                .GroupBy(c => c.animationLayer)
                .Select(g => g.FirstOrDefault(c => c.playbackMainInLayer) ?? g.First());

            foreach (var clip in firstOrMainPerLayer)
            {
                if (clip.animationLayer == current.animationLayer)
                    PlayClip(current, true);
                else
                    PlayClip(clip, true);
            }
        }

        public void StopClip(string animationName)
        {
            var clip = GetClip(animationName);
            StopClip(clip);
        }

        public void StopClip(AtomAnimationClip clip)
        {
            if (!clip.playbackEnabled) return;
            clip.Leave();
            clip.Reset(false);
            if (clip.animationPattern)
                clip.animationPattern.SetBoolParamValue("loopOnce", true);

            if (!clips.Any(c => c.playbackMainInLayer))
            {
                isPlaying = false;
                playTime = current.clipTime;
            }

            onIsPlayingChanged.Invoke(clip);
        }

        public void StopAll()
        {
            isPlaying = false;

            foreach (var clip in clips)
            {
                if (clip.playbackEnabled)
                    StopClip(clip);
            }

            foreach (var clip in clips)
            {
                clip.Reset(false);
            }
            playTime = playTime.Snap(snap);
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
            Blend(from, 0f, current.blendDuration);
            from.playbackMainInLayer = false;
            Blend(to, 1f, current.blendDuration);
            to.playbackMainInLayer = true;
            if (to.playbackWeight == 0) to.clipTime = 0f;

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

        private void AssignNextAnimation(AtomAnimationClip clip)
        {
            if (clip.nextAnimationName == null) return;
            if (clips.Count == 1) return;

            if (clip.nextAnimationTime <= 0)
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
            if (isPlaying || !enabled) return;

            if (!force && (_animationRebuildRequestPending || _animationRebuildInProgress))
                _sampleAfterRebuild = true;

            current.playbackEnabled = true;
            current.playbackWeight = 1f;
            SampleTriggers();
            SampleFloatParams();
            SampleControllers();
            current.playbackEnabled = false;
            current.playbackWeight = 0f;
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
                SuperController.LogError($"VamTimeline.{nameof(AtomAnimation)}.{nameof(RebuildAnimationNow)}: " + exc);
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
                if (!clip.transition) continue;
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
                SuperController.LogError($"VamTimeline.{nameof(RebuildAnimationNow)}: Suspiciously long animation rebuild ({sw.Elapsed})");
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
            var previous = clips.FirstOrDefault(c => c.nextAnimationName == clip.animationName);
            if (previous != null && (previous.IsDirty() || clip.IsDirty()))
            {
                clip.Paste(0f, previous.Copy(previous.animationLength, previous.GetAllCurveTargets().Cast<IAtomAnimationTarget>()), false);
                realign = true;
            }
            var next = GetClip(clip.nextAnimationName);
            if (next != null && (next.IsDirty() || clip.IsDirty()))
            {
                clip.Paste(clip.animationLength, next.Copy(0f, next.GetAllCurveTargets().Cast<IAtomAnimationTarget>()), false);
                realign = true;
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

        private void RebuildClip(AtomAnimationClip clip)
        {
            foreach (var target in clip.targetControllers)
            {
                if (!target.dirty) continue;

                if (clip.loop)
                    target.SetCurveSnapshot(clip.animationLength, target.GetCurveSnapshot(0f), false);

                target.ReapplyCurveTypes(clip.loop);

                if (clip.ensureQuaternionContinuity)
                {
                    UnitySpecific.EnsureQuaternionContinuityAndRecalculateSlope(
                        target.rotX,
                        target.rotY,
                        target.rotZ,
                        target.rotW);
                }

                RebuildClipLoop(clip, target);
            }

            foreach (var target in clip.targetFloatParams)
            {
                if (!target.dirty) continue;

                if (clip.loop)
                    target.value.SetKeyframe(clip.animationLength, target.value[0].value);

                target.ReapplyCurveTypes(clip.loop);

                RebuildClipLoop(clip, target);
            }

            foreach (var target in clip.targetTriggers)
            {
                if (!target.dirty) continue;

                target.RebuildKeyframes(clip.animationLength);
            }
        }

        private static void RebuildClipLoop(AtomAnimationClip clip, ICurveAnimationTarget target)
        {
            KeyframeSettings settings;
            if (clip.loop && target.settings.TryGetValue(0, out settings) && settings.curveType == CurveTypeValues.Smooth)
            {
                foreach (var curve in target.GetCurves())
                    curve.SmoothLoop();
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

            foreach (var clip in clips)
            {
                if (clip.playbackScheduledNextAnimationName != null && playTime >= clip.playbackScheduledNextTime)
                {
                    TransitionAnimation(clip, GetClip(clip.playbackScheduledNextAnimationName));
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
