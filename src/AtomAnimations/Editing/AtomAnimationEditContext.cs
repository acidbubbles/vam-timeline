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

        public PeerManager peers;

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

        public Logger logger;

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
                if (current == null) return;
                var clips = animation.index.GetSiblingsByLayer(current);
                for(var i = 0; i < clips.Count; i++)
                {
                    var clip = clips[i];
                    clip.clipTime = value;
                    if (animation.isPlaying && !clip.playbackEnabled && clip.playbackMainInLayer) animation.PlayClip(clip, animation.sequencing);
                }
                SampleTriggers();
                if (!animation.isPlaying || animation.paused)
                    Sample();
                if (current.animationPattern != null)
                    current.animationPattern.SetFloatParamValue("currentTime", value);

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

        #region Edit-Time Updates

        public void FixedUpdate()
        {
            if(_animation.liveParenting && !_animation.isPlaying)
                _animation.SampleParentedControllers(current);
        }

        #endregion

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

        private float _lastStop;

        public void Stop()
        {
            if (logger.general) logger.Log(logger.generalCategory, "Edit: Stop");

            var wasCurrentMainInLayer = current.playbackMainInLayer;

            if (animation.isPlaying)
                animation.StopAll();
            else
                animation.ResetAll();

            // Apply pose on stop fast double-click
            if (!wasCurrentMainInLayer || _lastStop > Time.realtimeSinceStartup - 0.2f)
                current.pose?.Apply();

            _lastStop = Time.realtimeSinceStartup;

            onTimeChanged.Invoke(timeArgs);
            Sample();

            peers.SendStop();
        }

        public void PlayCurrentClip()
        {
            logger.Begin();
            if (logger.general) logger.Log(logger.generalCategory, $"Edit: Play '{current.animationNameQualified}'");

            animation.PlayClip(current, false);
        }

        public void PlayAll()
        {
            logger.Begin();
            if (logger.general) logger.Log(logger.generalCategory,"Edit: Play All");

            foreach (var clip in GetMainClipPerLayer())
            {
                if (clip == null) continue;
                animation.PlayClip(clip, true);
            }
        }

        public void PreviousFrame()
        {
            clipTime = GetPreviousFrame(clipTime);
        }

        public void NextFrame()
        {
            clipTime = GetNextFrame(clipTime);
        }

        public void GoToPreviousAnimation(string layerName)
        {
            if (!animation.isPlaying && current.animationLayer != layerName) return;
            var layer = animation.index.ByLayer(layerName);
            var main = animation.isPlaying ? layer.FirstOrDefault(c => c.playbackMainInLayer) : current;
            if (main == null) return;
            var animIdx = layer.IndexOf(main);
            if (animIdx == 0) return;
            var prev = layer[animIdx - 1];
            if (animation.isPlaying)
                animation.PlayClip(prev, true);
            else
                SelectAnimation(prev);
        }

        public void GoToNextAnimation(string layerName)
        {
            if (!animation.isPlaying && current.animationLayer != layerName) return;
            var layer = animation.index.ByLayer(layerName);
            var main = animation.isPlaying ? layer.FirstOrDefault(c => c.playbackMainInLayer) : current;
            if (main == null) return;
            var animIdx = layer.IndexOf(main);
            if (animIdx == layer.Count - 1) return;
            var next = layer[animIdx + 1];
            if (animation.isPlaying)
                animation.PlayClip(next, true);
            else
                SelectAnimation(next);
        }

        public void GoToPreviousLayer()
        {
            var layers = animation.index.ByLayer().Select(l => l[0].animationLayer).ToList();
            var animIdx = layers.IndexOf(current.animationLayer);
            if (animIdx == 0) return;
            var prev = layers[animIdx - 1];
            SelectAnimation(animation.index.ByLayer(prev)[0]);
        }

        public void GoToNextLayer()
        {
            var layers = animation.index.ByLayer().Select(l => l[0].animationLayer).ToList();
            var animIdx = layers.IndexOf(current.animationLayer);
            if (animIdx == layers.Count - 1) return;
            var next = layers[animIdx + 1];
            SelectAnimation(animation.index.ByLayer(next)[0]);
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
            var wasCurrentMainInLayer = current.playbackMainInLayer;
            animation.StopAndReset();
            var defaultClip = animation.GetDefaultClip();
            if (defaultClip != current)
                SelectAnimation(defaultClip);
            else if(!wasCurrentMainInLayer)
                SampleOrPose();
            onTimeChanged.Invoke(timeArgs);
        }

        public void Sample()
        {
            SampleNow();
            if (_animation.liveParenting) return;
            var hasParenting = GetMainClipPerLayer()
                .Where(c => c != null)
                .SelectMany(c => c.targetControllers)
                .Any(t => t.parentRigidbodyId != null);
            if (!hasParenting) return;
            if (_lateSample != null) StopCoroutine(_lateSample);
            _lateSample = StartCoroutine(LateSample(0.1f));
        }

        private void SampleTriggers()
        {
            var clips = GetMainClipPerLayer();
            foreach (var clip in clips)
            {
                for (var triggerIndex = 0; triggerIndex < clip.targetTriggers.Count; triggerIndex++)
                {
                    var target = current.targetTriggers[triggerIndex];
                    target.Sync(current.clipTime);
                    target.SyncAudio(current.clipTime);
                    CancelInvoke(nameof(LeaveSampledTriggers));
                    Invoke(nameof(LeaveSampledTriggers), 0.2f);
                }
            }
        }

        private void LeaveSampledTriggers()
        {
            if (_animation.isPlaying) return;
            var clips = GetMainClipPerLayer();
            foreach (var clip in clips)
            {
                for (var triggerIndex = 0; triggerIndex < clip.targetTriggers.Count; triggerIndex++)
                {
                    var target = current.targetTriggers[triggerIndex];
                    target.Leave();
                }
            }
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
            foreach (var clip in clips)
                if (clip != null)
                    clip.temporarilyEnabled = true;
            try
            {
                animation.Sample();
            }
            finally
            {
                foreach (var clip in clips)
                    if (clip != null)
                        clip.temporarilyEnabled = false;
            }
        }

        private AtomAnimationClip[] GetMainClipPerLayer()
        {
            var layers = animation.index.ByLayer();
            var list = new AtomAnimationClip[layers.Count];
            for (var i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];
                list[i] = layer[0].animationLayer == current.animationLayer
                    ? current
                    : GetPrincipalClipInLayer(layer, current.animationName, current.animationSet);
            }

            // Always start with the selected clip to avoid animation sets starting another animation on the currently shown layer
            var currentIdx = Array.IndexOf(list, current);
            if (currentIdx > -1)
            {
                list[currentIdx] = list[0];
                list[0] = current;
            }

            return list;
        }

        private static AtomAnimationClip GetPrincipalClipInLayer(IList<AtomAnimationClip> layer, string animationName, string animationSet)
        {
            if (animationSet != null)
            {
                var clip = layer.FirstOrDefault(c => c.animationSet == animationSet);
                // This is to prevent playing on the main layer, starting a set on another layer, which will then override the clip you just played on the main layer
                if (clip?.animationSet != null && clip.animationSet != animationSet)
                    clip = null;
                if (clip != null)
                    return clip;
            }

            return layer.FirstOrDefault(c => c.playbackMainInLayer) ??
                   layer.FirstOrDefault(c => c.animationName == animationName) ??
                   layer.FirstOrDefault(c => c.autoPlay) ??
                   layer[0];
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

            if (animation.isPlaying)
            {
                animation.PlayClip(current, animation.sequencing);
            }
            else
            {
                previous.clipTime = 0f;
                SampleOrPose();
            }

            onCurrentAnimationChanged.Invoke(new CurrentAnimationChangedEventArgs
            {
                before = previous,
                after = current
            });
        }

        private void SampleOrPose()
        {
            if (current.pose != null)
                current.pose.Apply();
            else if (!SuperController.singleton.freezeAnimation)
                Sample();
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

        public void SelectAll(bool selected)
        {
            foreach (var target in current.GetAllTargets())
            {
                target.selected = selected;
            }
        }
    }
}
