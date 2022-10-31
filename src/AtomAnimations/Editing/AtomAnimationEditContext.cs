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
        private IList<AtomAnimationClip> _currentAsList;

        public AtomAnimationClip current
        {
            get { return _current; }
            private set
            {
                _current = value;
                _currentAsList = new[] { value };
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
            if (current == null) return;

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

                IList<AtomAnimationClip> clips;
                if (animation.focusOnLayer)
                {
                    clips = _currentAsList;
                }
                else if (current.isOnNoneSegment || current.isOnSharedSegment)
                {
                    clips = animation.index.GetSiblingsByLayer(current);
                }
                else
                {
                    clips = animation.GetDefaultClipsPerLayer(current, false);
                }

                var baseOffset = current.timeOffset;
                var baseSpeed = current.speed;
                if (baseSpeed != 0)
                {
                    for (var i = 0; i < clips.Count; i++)
                    {
                        var clip = clips[i];
                        clip.clipTime = ((value + clip.timeOffset - baseOffset) / baseSpeed * clip.speed);
                        if (animation.isPlaying && !clip.playbackEnabled && clip.playbackMainInLayer)
                            animation.PlayClip(clip, animation.sequencing);
                    }
                }

                SampleLiveTriggers();
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
                animation.AddClip(new AtomAnimationClip(AtomAnimationClip.DefaultAnimationName, AtomAnimationClip.DefaultAnimationLayer, AtomAnimationClip.DefaultAnimationSegment, logger));
            current = animation.GetDefaultClip();
            if (animation.clips.Any(c => c.IsDirty()))
                animation.RebuildAnimationNow();
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

            var wasPlaying = current.playbackMainInLayer;

            if (animation.isPlaying)
            {
                animation.StopAll();
                onTimeChanged.Invoke(timeArgs);
            }
            else
            {
                animation.ResetAll();
                // Adjust time offsets
                clipTime = 0f;
            }

            // Apply pose on stop fast double-click
            SampleOrPose(!wasPlaying, _lastStop > Time.realtimeSinceStartup - 0.2f);

            _lastStop = Time.realtimeSinceStartup;

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

            animation.PlaySegment(current);
        }

        public void PreviousFrame()
        {
            clipTime = GetPreviousFrame(clipTime);
        }

        public void NextFrame()
        {
            clipTime = GetNextFrame(clipTime);
        }

        public void GoToPreviousAnimation(int layerNameQualifiedId)
        {
            if (!animation.isPlaying && current.animationLayerQualifiedId != layerNameQualifiedId) return;
            var layer = animation.index.ByLayerQualified(layerNameQualifiedId);
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

        public void GoToNextAnimation(int layerNameQualifiedId)
        {
            if (!animation.isPlaying && current.animationLayerQualifiedId != layerNameQualifiedId) return;
            var layer = animation.index.ByLayerQualified(layerNameQualifiedId);
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

        public AtomAnimationsClipsIndex.IndexedSegment currentSegment
        {
            get
            {
                AtomAnimationsClipsIndex.IndexedSegment segment;
                if (_animation.index.segmentsById.TryGetValue(current.animationSegmentId, out segment))
                    return segment;
                return _animation.index.emptySegment;
            }
        }

        public IList<AtomAnimationClip> currentLayer => _animation.index.ByLayerQualified(current.animationLayerQualifiedId);

        public void GoToPreviousLayer()
        {
            var segment = currentSegment;
            var layers = segment.layers.Select(l => l[0].animationLayerId).ToList();
            var animIdx = layers.IndexOf(current.animationLayerId);
            if (animIdx == 0) return;
            var prev = layers[animIdx - 1];
            SelectAnimation(segment.layersMapById[prev][0]);
        }

        public void GoToNextLayer()
        {
            var segment = currentSegment;
            var layers = segment.layers.Select(l => l[0].animationLayerId).ToList();
            var animIdx = layers.IndexOf(current.animationLayerId);
            if (animIdx == layers.Count - 1) return;
            var next = layers[animIdx + 1];
            SelectAnimation(segment.layersMapById[next][0]);
        }

        public void GoToPreviousSegment()
        {
            var segments = animation.index.segmentIds;
            var idx = segments.IndexOf(current.animationSegmentId);
            if (idx == 0) return;
            var prev = segments[idx - 1];
            SelectAnimation(animation.index.segmentsById[prev].mainClip);
        }

        public void GoToNextSegment()
        {
            var segments = animation.index.segmentIds;
            var idx = segments.IndexOf(current.animationSegmentId);
            if (idx == segments.Count - 1) return;
            var next = segments[idx + 1];
            SelectAnimation(animation.index.segmentsById[next].mainClip);
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
                    AddTargetIfMissing<FreeControllerV3ClipboardEntry, FreeControllerV3Ref, TransformTargetSnapshot>(
                        entry.controllers,
                        (c, r) => c.targetControllers.Any(t => t.animatableRef == r),
                        false,
                        r => r
                    );
                    AddTargetIfMissing<FloatParamValClipboardEntry, JSONStorableFloatRef, FloatParamTargetSnapshot>(
                        entry.floatParams,
                        (c, r) => c.targetFloatParams.Any(t => t.animatableRef == r),
                        false,
                        r => r
                    );
                    AddTargetIfMissing<TriggersClipboardEntry, TriggersTrackRef, TriggerTargetSnapshot>(
                        entry.triggers,
                        (c, r) => c.targetTriggers.Any(t => t.animatableRef.name == r.name),
                        true,
                        r => _animation.animatables.GetOrCreateTriggerTrack(current.animationLayerQualifiedId, r.name)
                    );
                    current.Paste(clipTime + entry.time - timeOffset, entry);
                }
                Sample();
            }
            catch (Exception exc)
            {
                SuperController.LogError($"Timeline.{nameof(AtomAnimationEditContext)}.{nameof(Paste)}: {exc}");
            }
        }

        private void AddTargetIfMissing<TEntry, TRef, TSnapshot>(List<TEntry> entries, Func<AtomAnimationClip, TRef, bool> hasTarget, bool allowManyLayers, Func<TRef, TRef> getOrCreateRef)
            where TEntry : IClipboardEntry<TRef, TSnapshot>
            where TRef : AnimatableRefBase
            where TSnapshot : ISnapshot
        {
            if (entries == null) return;
            foreach (var entry in entries)
            {
                var animatableRef = entry.animatableRef;
                var alreadyHasTarget = hasTarget(current, animatableRef);
                if (alreadyHasTarget)
                {
                    continue;
                }

                if (!allowManyLayers)
                {
                    var targetUsedElsewhere = animation.clips
                        .Where(c => (c.animationSegment == current.animationSegment && c.animationLayerQualified != current.animationLayerQualified) || c.isOnSharedSegment)
                        .Any(c => hasTarget(c, animatableRef));
                    if (targetUsedElsewhere) continue;
                }

                foreach (var clip in currentLayer)
                {
                    var animatableRefToAdd = getOrCreateRef(animatableRef);
                    if (hasTarget(clip, animatableRefToAdd))
                    {
                        continue;
                    }
                    var added = clip.Add(animatableRefToAdd);
                    if (added == null)
                    {
                        SuperController.LogError($"Timeline: Cannot paste {animatableRef.GetFullName()}, invalid add state.");
                        continue;
                    }
                    added.SetSnapshot(0f, entry.snapshot);
                    added.SetSnapshot(clip.animationLength, entry.snapshot);
                }
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
            {
                SelectAnimation(defaultClip);
            }
            else if(!wasCurrentMainInLayer)
            {
                // Adjust time offsets
                clipTime = 0f;
            }

            SampleOrPose(true, true);
            onTimeChanged.Invoke(timeArgs);
        }

        public void Sample()
        {
            SampleNow();
            if (_animation.liveParenting) return;
            var hasParenting = animation.GetDefaultClipsPerLayer(current)
                .SelectMany(c => c.targetControllers)
                .Any(t => t.parentRigidbodyId != null);
            if (!hasParenting) return;
            if (_lateSample != null) StopCoroutine(_lateSample);
            _lateSample = StartCoroutine(LateSample(0.1f));
        }

        private void SampleLiveTriggers()
        {
            var clips = animation.GetDefaultClipsPerLayer(current);
            for (var clipIndex = 0; clipIndex < clips.Count; clipIndex++)
            {
                var clip = clips[clipIndex];
                for (var triggerIndex = 0; triggerIndex < clip.targetTriggers.Count; triggerIndex++)
                {
                    var target = clip.targetTriggers[triggerIndex];
                    if (!target.animatableRef.live) continue;
                    target.Sync(current.clipTime, true);
                    target.SyncAudio(current.clipTime);
                    CancelInvoke(nameof(LeaveSampledTriggers));
                    Invoke(nameof(LeaveSampledTriggers), 0.2f);
                }
            }
        }

        private void LeaveSampledTriggers()
        {
            if (_animation.isPlaying) return;
            var clips = animation.GetDefaultClipsPerLayer(current);
            foreach (var clip in clips)
            {
                for (var triggerIndex = 0; triggerIndex < clip.targetTriggers.Count; triggerIndex++)
                {
                    var target = clip.targetTriggers[triggerIndex];
                    if (!target.animatableRef.live) continue;
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

            if (animation.isPlaying && !animation.paused)
                return;

            animation.playingAnimationSegment = current.animationSegment;

            var clips = animation.GetDefaultClipsPerLayer(current);
            for (var i = 0; i < clips.Count; i++)
            {
                var clip = clips[i];
                clip.temporarilyEnabled = true;
            }

            try
            {
                animation.Sample();
            }
            finally
            {
                for (var i = 0; i < clips.Count; i++)
                {
                    var clip = clips[i];
                    clip.temporarilyEnabled = false;
                }
            }
        }

        #endregion

        #region Selection

        public void SelectAnimation(string animationSegment, string animationLayer, string animationName)
        {
            var clip = animation.GetClip(animationSegment, animationLayer, animationName);
            if (clip == null) throw new NullReferenceException($"Could not find animation '{animationSegment}::{animationLayer}::{animationName}'. Found animations: '{string.Join("', '", animation.clips.Select(c => c.animationNameQualified).ToArray())}'.");
            SelectAnimation(clip);
        }

        public void SelectAnimation(AtomAnimationClip clip)
        {
            if (clip == null) return;
            if (current == clip) return;
            var previous = current;
            current = clip;

            if (animation.isPlaying)
            {
                var to = current;
                if (to.pose == null)
                {
                    var toWithPose = animation.index.GetSiblingsByLayer(current).FirstOrDefault(c => c.pose != null);
                    if (toWithPose != null)
                        to = toWithPose;
                }
                animation.PlayClip(to, animation.sequencing);
            }
            else
            {
                animation.playingAnimationSegment = clip.animationSegment;
                var differentAnimation = previous.animationSegmentId != current.animationSegmentId || previous.animationNameId != current.animationNameId;
                var differentPose = false;
                if (differentAnimation)
                {
                    previous.clipTime = 0f;
                    // Adjust time offsets
                    clipTime = 0f;

                    var previousDefault = animation.GetDefaultClipsPerLayer(previous, true);
                    var currentDefault = animation.GetDefaultClipsPerLayer(current, true);
                    if (previousDefault.FirstOrDefault(c => c.pose != null)?.pose != currentDefault.FirstOrDefault(c => c.pose != null)?.pose)
                        differentPose = true;
                }

                try
                {
                    SampleOrPose(true, differentPose);
                }
                catch (Exception exc)
                {
                    SuperController.LogError($"Timeline: There was an error in animation {clip.animationNameQualified}: {exc}");
                }
            }

            onCurrentAnimationChanged.Invoke(new CurrentAnimationChangedEventArgs
            {
                before = previous,
                after = current
            });
        }

        private void SampleOrPose(bool pose, bool force)
        {
            if (pose)
            {
                var clips = animation.GetDefaultClipsPerLayer(current);
                var poseClip = clips.FirstOrDefault(c => c?.pose != null)?.pose;
                if (poseClip != null && (poseClip != animation.lastAppliedPose || force))
                {
                    poseClip.Apply();
                    animation.lastAppliedPose = poseClip;
                    if (_lateSample != null) StopCoroutine(_lateSample);
                    _lateSample = StartCoroutine(LateSample(0.1f));
                    return;
                }
            }

            if (!SuperController.singleton.freezeAnimation)
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

        public IEnumerable<ICurveAnimationTarget> GetSelectedCurveTargets()
        {
            return current.GetAllCurveTargets().Where(t => t.selected);
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

        public void SelectSegment(string val)
        {
            if (val == null) return;
            var clips = animation.clips.Where(c => c.animationSegment == val).ToList();
            var clip = clips.FirstOrDefault(c => c.animationLayerId == current.animationLayerId && c.animationNameId == current.animationNameId)
                       ?? clips.FirstOrDefault(c => c.animationLayerId == current.animationLayerId || c.animationNameId == current.animationNameId)
                       ?? clips.FirstOrDefault();
            SelectAnimation(clip);
        }

        public void SelectLayer(string val)
        {
            if (val == null) return;
            var clips = animation.clips.Where(c => c.animationSegmentId == current.animationSegmentId && c.animationLayer == val).ToList();
            var clip = clips.FirstOrDefault(c => c.animationNameId == current.animationNameId)
                       ?? clips.FirstOrDefault(c => c.animationSetId == current.animationSetId)
                       ?? clips.FirstOrDefault();
            SelectAnimation(clip);
        }

        public void KeyframeSelected()
        {
            foreach (var target in GetSelectedCurveTargets())
            {
                target.SetKeyframeToCurrent(current.clipTime);
            }
        }
    }
}
