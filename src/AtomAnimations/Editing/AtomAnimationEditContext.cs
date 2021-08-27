using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace VamTimeline
{
    public struct ScrubberRange
    {
        public float rangeBegin;
        public float rangeDuration;
    }

    public class AtomAnimationEditContext : MonoBehaviour
    {
        public const float DefaultSnap = 0.1f;

        public struct TimeChangedEventArgs { public float time; public float currentClipTime; }
        public class TimeChangedEvent : UnityEvent<TimeChangedEventArgs> { }
        public struct ScrubberRangeChangedEventArgs { public ScrubberRange scrubberRange; }
        public class ScrubberRangeChangedEvent : UnityEvent<ScrubberRangeChangedEventArgs> { }
        public class CurrentAnimationChangedEventArgs { public AtomAnimationClip before; public AtomAnimationClip after; }
        public class CurrentAnimationChangedEvent : UnityEvent<CurrentAnimationChangedEventArgs> { }
        public class AnimationSettingsChanged : UnityEvent<string> { }

        public readonly AnimationSettingsChanged onEditorSettingsChanged = new AnimationSettingsChanged();
        public readonly TimeChangedEvent onTimeChanged = new TimeChangedEvent();
        public readonly ScrubberRangeChangedEvent onScrubberRangeChanged = new ScrubberRangeChangedEvent();
        public readonly CurrentAnimationChangedEvent onCurrentAnimationChanged = new CurrentAnimationChangedEvent();

        public AtomClipboard clipboard { get; } = new AtomClipboard();

        private bool _sampleAfterRebuild;
        private float _lastCurrentAnimationLength;

        public int startRecordIn = 5;

        private float _snap = DefaultSnap;
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
        private bool _showPaths = true;
        public bool showPaths
        {
            get { return _showPaths; }
            set { _showPaths = value; onEditorSettingsChanged.Invoke(nameof(showPaths)); }
        }
        private bool _locked;
        public bool locked
        {
            get { return _locked; }
            set { _locked = value; onEditorSettingsChanged.Invoke(nameof(locked)); }
        }

        public TimeChangedEventArgs timeArgs => new TimeChangedEventArgs { time = playTime, currentClipTime = current.clipTime };
        private AtomAnimationClip _current;

        public AtomAnimationClip current
        {
            get { return _current; }
            private set
            {
                _current = value;
                _lastCurrentAnimationLength = value.animationLength;
                ResetScrubberRange();
            }
        }
        // This ugly property is to cleanly allow ignoring grab release at the end of a mocap recording
        public bool ignoreGrabEnd;
        private AtomAnimation _animation;
        private Coroutine _lateSample;

        public AtomAnimation animation
        {
            get
            {
                return _animation;
            }
            set
            {
                if (_animation != null)
                {
                    _animation.onAnimationRebuilt.RemoveListener(OnAnimationRebuilt);
                }
                _animation = value;
                _animation.onAnimationRebuilt.AddListener(OnAnimationRebuilt);
            }
        }

        private void OnAnimationRebuilt()
        {
            if (_sampleAfterRebuild)
            {
                _sampleAfterRebuild = false;
                Sample();
            }

            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (_lastCurrentAnimationLength != current.animationLength)
            {
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (scrubberRange.rangeDuration == _lastCurrentAnimationLength || scrubberRange.rangeDuration > current.animationLength)
                {
                    ResetScrubberRange();
                }
                _lastCurrentAnimationLength = current.animationLength;
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
                animation.playTime = value;
                if (current == null) return;
                var clips = animation.GetClips(current.animationName);
                for(var i = 0; i < clips.Count; i++)
                {
                    var clip = clips[i];
                    clip.clipTime = value;
                    if (animation.isPlaying && !clip.playbackEnabled && clip.playbackMainInLayer) animation.PlayClip(clip, animation.sequencing);
                }
                if (!animation.isPlaying || animation.paused)
                    Sample();
                if (current.animationPattern != null)
                    current.animationPattern.SetFloatParamValue("currentTime", playTime);
                // TODO: If the scrubber range does not contain the time, update the range
                onTimeChanged.Invoke(timeArgs);
            }
        }

        private ScrubberRange _scrubberRange;

        public ScrubberRange scrubberRange
        {
            get
            {
                return _scrubberRange;
            }
            set
            {
                _scrubberRange = value;
                // TODO: If the clip time is out of range, update the scrubber position
                onScrubberRangeChanged.Invoke(new ScrubberRangeChangedEventArgs {scrubberRange = value});
            }
        }

        public float playTime
        {
            get
            {
                return animation.playTime;
            }
            set
            {
                animation.playTime = value;
                if (!current.playbackEnabled)
                    current.clipTime = value;
                if (!animation.isPlaying || animation.paused)
                    Sample();
                onTimeChanged.Invoke(timeArgs);
            }
        }

        public void Initialize()
        {
            if (animation.clips.Count == 0)
                animation.AddClip(new AtomAnimationClip("Anim 1", AtomAnimationClip.DefaultAnimationLayer));
            current = animation.GetDefaultClip();
            if (animation.clips.Any(c => c.IsDirty()))
            {
                animation.RebuildAnimationNow();
            }
        }

        public bool CanEdit()
        {
            if (SuperController.singleton.gameMode != SuperController.GameMode.Edit) return false;
            if (locked) return false;
            if (!isActiveAndEnabled) return false;
            if (animation.isPlaying) return false;
            return true;
        }

        #region Scrubber

        public void ResetScrubberRange()
        {
            scrubberRange = new ScrubberRange
            {
                rangeBegin = 0f,
                rangeDuration = current.animationLength
            };
        }

        public void ZoomScrubberRangeIn()
        {
            ZoomScrubberRange(0.8f);
        }

        public void ZoomScrubberRangeOut()
        {
            ZoomScrubberRange(1.2f);
        }

        private void ZoomScrubberRange(float ratio)
        {
            var rangeDuration = scrubberRange.rangeDuration * ratio;
            var rangeBegin = Mathf.Max(0f, scrubberRange.rangeBegin - (rangeDuration - scrubberRange.rangeDuration) / 2f);
            rangeDuration = Mathf.Min(current.animationLength - rangeBegin, rangeDuration);
            scrubberRange = new ScrubberRange
            {
                rangeBegin = rangeBegin,
                rangeDuration = rangeDuration
            };
        }

        public void MoveScrubberRangeBackward()
        {
            MoveScrubberRange(scrubberRange.rangeBegin - 1f);
        }

        public void MoveScrubberRangeForward()
        {
            MoveScrubberRange(scrubberRange.rangeBegin + 1f);
        }

        public void MoveScrubberRange(float rangeBegin)
        {
            scrubberRange = new ScrubberRange
            {
                rangeBegin = Mathf.Clamp(
                    rangeBegin,
                    0,
                    current.animationLength - scrubberRange.rangeDuration),
                rangeDuration = scrubberRange.rangeDuration
            };
        }

        #endregion

        #region Animation Control

        public void Stop()
        {
            if (animation.isPlaying)
                animation.StopAll();
            else
                animation.ResetAll();
            onTimeChanged.Invoke(timeArgs);
            Sample();
        }

        public void PlayCurrentClip()
        {
            animation.PlayClips(current.animationName, false);
        }

        public void PlayAll()
        {
            PlayCurrentAndOtherMainsInLayers();
        }

        public void PreviousFrame()
        {
            clipTime = GetPreviousFrame(clipTime);
        }

        public void NextFrame()
        {
            clipTime = GetNextFrame(clipTime);
        }

        public void RewindSeconds(float seconds)
        {
            var time = clipTime - seconds;
            if (time < 0)
                time = 0;
            clipTime = time;
        }

        public void ForwardSeconds(float seconds)
        {
            var time = clipTime + seconds;
            if (time >= current.animationLength - 0.001f)
                time = current.loop ? current.animationLength - seconds : current.animationLength;
            clipTime = time;
        }

        public void SnapTo(float span)
        {
            clipTime = clipTime.Snap(span);
        }

        public void SnapToClosestKeyframe()
        {
            var closest = float.PositiveInfinity;
            foreach (var time in GetAllOrSelectedTargets().Select(t => t.GetTimeClosestTo(clipTime)))
            {
                if (Mathf.Abs(time - clipTime) < Mathf.Abs(closest - clipTime))
                    closest = time;
            }
            if(float.IsInfinity(closest))
                return;
            clipTime = closest;
        }

        public void Delete()
        {
            try
            {
                if (!CanEdit()) return;
                var time = clipTime;
                if (time.IsSameFrame(0f)) return;
                if (time.IsSameFrame(current.animationLength))
                {
                    if (current.loop) return;
                    foreach (var target in GetAllOrSelectedTargets().OfType<ICurveAnimationTarget>())
                    {
                        target.ChangeCurveByTime(current.animationLength, CurveTypeValues.CopyPrevious);
                    }

                    return;
                }
                foreach (var target in GetAllOrSelectedTargets())
                {
                    target.DeleteFrame(time);
                }
            }
            catch (Exception exc)
            {
                SuperController.LogError($"Timeline.{nameof(AtomAnimationEditContext)}.{nameof(Delete)}: {exc}");
            }
        }

        public void Cut()
        {
            try
            {
                if (!CanEdit()) return;

				var time = clipTime;
				var entry = AtomAnimationClip.Copy(time, GetAllOrSelectedTargets().ToList());

				if (entry.empty)
				{
                    SuperController.LogMessage("Timeline: Nothing to cut");
				}
				else
				{
					clipboard.Clear();
					clipboard.time = time;
					clipboard.entries.Add(entry);
					if (time.IsSameFrame(0f) || time.IsSameFrame(current.animationLength)) return;
					foreach (var target in GetAllOrSelectedTargets())
					{
						target.DeleteFrame(time);
					}
				}
            }
            catch (Exception exc)
            {
                SuperController.LogError($"Timeline.{nameof(AtomAnimationEditContext)}.{nameof(Cut)}: {exc}");
            }
        }

        public void Copy()
        {
            try
            {
                if (!CanEdit()) return;

				var time = clipTime;
				var entry = AtomAnimationClip.Copy(time, GetAllOrSelectedTargets().ToList());

				if (entry.empty)
				{
                    SuperController.LogMessage("Timeline: Nothing to copy");
				}
				else
				{
					clipboard.Clear();
					clipboard.time = time;
					clipboard.entries.Add(entry);
				}
            }
            catch (Exception exc)
            {
                SuperController.LogError($"Timeline.{nameof(AtomAnimationEditContext)}.{nameof(Copy)}: {exc}");
            }
        }

        public void Paste()
        {
            try
            {
                if (!CanEdit()) return;
                if (clipboard.entries.Count == 0)
                {
                    SuperController.LogMessage("Timeline: Clipboard is empty");
                    return;
                }
                var timeOffset = clipboard.time;
                foreach (var entry in clipboard.entries)
                {
                    current.Paste(clipTime + entry.time - timeOffset, entry);
                }
                Sample();
            }
            catch (Exception exc)
            {
                SuperController.LogError($"Timeline.{nameof(AtomAnimationEditContext)}.{nameof(Paste)}: {exc}");
            }
        }

        #endregion

        #region Keyframing

        public int SetKeyframeToCurrentTransform(FreeControllerV3AnimationTarget target, float time)
        {
            time = time.Snap();
            if (time > current.animationLength)
                time = current.animationLength;
            return target.SetKeyframeToCurrent(time);
        }

        #endregion

        #region Playback

        public void StopAndReset()
        {
            animation.StopAndReset();
            SelectAnimation(animation.GetDefaultClip());
            onTimeChanged.Invoke(timeArgs);
            Sample();
        }

        public void PlayCurrentAndOtherMainsInLayers(bool sequencing = true)
        {
            foreach (var clip in GetMainClipPerLayer())
            {
                animation.PlayClip(clip, sequencing);
            }
        }

        public void Sample()
        {
            var hasPose = current.pose != null;
            if(hasPose && clipTime == 0f)
                current.pose.Apply();
            else
                SampleNow();
            var hasParenting = GetMainClipPerLayer().SelectMany(c => c.targetControllers).Any(t => t.parentRigidbodyId != null);
            if (!hasPose && !hasParenting) return;
            if (_lateSample != null) StopCoroutine(_lateSample);
            _lateSample = StartCoroutine(LateSample(0.1f));
        }

        private IEnumerator LateSample(float settleDuration)
        {
            var settleTime = Time.time + settleDuration;
            yield return 0;
            while (animation.simulationFrozen)
                yield return 0;
            // Give a little bit of time for physics to settle and re-sample
            if(Time.time > settleTime)
                yield return new WaitForSeconds(Time.time - settleTime);
            SampleNow();
        }

        private void SampleNow()
        {
            if (animation.RebuildPending())
            {
                _sampleAfterRebuild = true;
                return;
            }

            var clips = GetMainClipPerLayer();
            foreach (var clip in clips) clip.temporarilyEnabled = true;
            animation.Sample();
            foreach (var clip in clips) clip.temporarilyEnabled = false;
        }

        private List<AtomAnimationClip> GetMainClipPerLayer()
        {
            var list = new List<AtomAnimationClip>(animation.index.ByLayer().Count());
            list.AddRange(animation.index
                .ByLayer()
                .Select(g =>
                {
                    return g[0].animationLayer == current.animationLayer
                        ? current
                        : g.FirstOrDefault(c => c.playbackMainInLayer) ?? g.FirstOrDefault(c => c.animationName == current.animationName) ?? g.FirstOrDefault(c => c.autoPlay) ?? g[0];
                }));
            return list;
        }

        #endregion

        #region Selection

        public void SelectAnimation(string animationNameQualified)
        {
            var clip = animation.GetClipQualified(animationNameQualified);
            if (clip == null) throw new NullReferenceException($"Could not find animation '{animationNameQualified}'. Found animations: '{string.Join("', '", animation.clips.Select(c => c.animationNameQualified).ToArray())}'.");
            SelectAnimation(clip);
        }

        public void SelectAnimation(AtomAnimationClip clip)
        {
            if (current == clip) return;
            var previous = current;
            current = clip;
            onCurrentAnimationChanged.Invoke(new CurrentAnimationChangedEventArgs
            {
                before = previous,
                after = current
            });

            if (animation.isPlaying)
            {
                animation.PlayClip(current, animation.sequencing);
            }
            else if (!SuperController.singleton.freezeAnimation)
            {
                Sample();
            }
        }

        public IEnumerable<IAtomAnimationTarget> GetAllOrSelectedTargets()
        {
            var targets = GetSelectedTargets().ToList();
            if (targets.Count == 0) return current.GetAllTargets();
            return targets;
        }

        public IEnumerable<IAtomAnimationTarget> GetSelectedTargets()
        {
            return current.GetAllTargets().Where(t => t.selected);
        }

        #endregion

        #region Frame Nav

        public float GetNextFrame(float time)
        {
            time = time.Snap();
            if (time.IsSameFrame(current.animationLength))
                return 0f;
            var nextTime = current.animationLength;
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
            if (nextTime.IsSameFrame(current.animationLength) && current.loop)
                return 0f;
            return nextTime;
        }

        public float GetPreviousFrame(float time)
        {
            time = time.Snap();
            if (time.IsSameFrame(0))
            {
                try
                {
                    return GetAllOrSelectedTargets().Select(t => t.GetAllKeyframesTime()).Select(c => c[c.Length - (current.loop ? 2 : 1)]).Max();
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

        #region Unity Lifecycle

        public void OnDestroy()
        {
            if (_animation != null)
            {
                _animation.onAnimationRebuilt.RemoveListener(OnAnimationRebuilt);
            }

            onTimeChanged.RemoveAllListeners();
            onCurrentAnimationChanged.RemoveAllListeners();
            onEditorSettingsChanged.RemoveAllListeners();
        }

        #endregion

        public void ChangeCurveType(int curveType)
        {
            if (!CanEdit())
                return;

            var time = clipTime.Snap();

            foreach (var target in GetAllOrSelectedTargets().OfType<ICurveAnimationTarget>())
                target.ChangeCurveByTime(time, curveType);

            if (curveType == CurveTypeValues.CopyPrevious)
                Sample();
        }
    }
}
