using System;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace VamTimeline
{
    public partial class AtomAnimation
    {
        #region Processing

        private void ProcessAnimationSequence(float deltaTime)
        {
            var clipsPlaying = 0;
            var clipsQueued = 0;
            for (var i = 0; i < clips.Count; i++)
            {
                var clip = clips[i];

                if (clip.playbackEnabled)
                    clipsPlaying++;

                if (clip.playbackScheduledNextAnimation != null)
                    clipsQueued++;

                if (pauseSequencing)
                    continue;

                bool needsNextAssignment = false;
                if (!clip.loop && clip.playbackEnabled && clip.clipTime >= clip.animationLength && float.IsNaN(clip.playbackScheduledNextTimeLeft) && !clip.infinite)
                {
                    needsNextAssignment = true;
                }
                else if (clip.playbackEnabled && clip.playbackMainInLayer && clip.playbackScheduledNextAnimation == null)
                {
                    AssignNextAnimation(clip);
                }
                if (clip.playbackMainInLayer && clip.playbackScheduledNextAnimation != null)
                {
                    var adjustedDeltaTime = deltaTime * clip.speed * globalSpeed;
                    clip.playbackScheduledNextTimeLeft -= adjustedDeltaTime;

                    if (clip.playbackScheduledNextTimeLeft <= clip.playbackScheduledFadeOutAtRemaining)
                    {
                        _scheduleFadeIn = float.MaxValue;
                        clip.playbackScheduledFadeOutAtRemaining = float.NaN;
                        if (fadeManager?.black == false)
                        {
                            if (logger.sequencing) logger.Log(logger.sequencingCategory, $"Fade out {clip.playbackScheduledNextTimeLeft:0.000}s before transition.");
                            fadeManager.FadeOut();
                        }
                    }

                    if (clip.playbackScheduledNextTimeLeft <= 0)
                    {
                        var nextClip = clip.playbackScheduledNextAnimation;
                        clip.playbackScheduledNextAnimation = null;
                        clip.playbackScheduledNextTimeLeft = float.NaN;

                        if (nextClip.isOnSegment && playingAnimationSegmentId != nextClip.animationSegmentId)
                        {
                            PlaySegment(nextClip, true, isQueueActive);
                        }
                        else
                        {
                            TransitionClips(clip, nextClip, 0f);
                            if (!isQueueActive)
                            {
                                PlaySiblings(nextClip);
                            }
                        }

                        if (nextClip.fadeOnTransition && fadeManager?.black == true && nextClip.animationLayerId == index.segmentsById[nextClip.animationSegmentId].layerIds[0])
                        {
                            _scheduleFadeIn = playTime + fadeManager.halfBlackTime;
                        }
                        continue;
                    }
                }
                if (needsNextAssignment && float.IsNaN(clip.playbackScheduledNextTimeLeft))
                {
                    if (clip.playbackMainInLayer)
                    {
                        if (float.IsNaN(clip.playbackScheduledNextTimeLeft))
                        {
                            if (logger.general) logger.Log(logger.generalCategory, $"Leave '{clip.animationNameQualified}' (non-looping main complete, no sequence/queue)");
                            clip.Leave();
                            clip.Reset(true);
                            onClipIsPlayingChanged.Invoke(clip);
                        }
                    }
                    else
                    {
                        if (logger.general) logger.Log(logger.generalCategory, $"Leave '{clip.animationNameQualified}' (non-looping non-main complete)");
                        clip.Leave();
                        clip.Reset(true);
                        onClipIsPlayingChanged.Invoke(clip);
                    }
                }
            }
            if (clipsPlaying == 0 && clipsQueued == 0 && !isQueueActive)
            {
                StopAll();
            }
        }

        #endregion

        #region Transitions and sequencing

        private void TransitionClips(AtomAnimationClip from, AtomAnimationClip to, float siblingClipTime = 0f)
        {
            if (to == null) throw new ArgumentNullException(nameof(to));

            if (from == null)
            {
                to.clipTime = siblingClipTime + to.timeOffset;
                to.playbackMainInLayer = true;
                BlendIn(to, to.blendInDuration);
                onMainClipPerLayerChanged.Invoke(new AtomAnimationChangeClipEventArgs { before = null, after = to });
                if (!ReferenceEquals(to.animationPattern, null))
                {
                    to.animationPattern.SetBoolParamValue("loopOnce", false);
                    to.animationPattern.ResetAndPlay();
                }
                if (!isQueueActive)
                {
                    AssignNextAnimation(to);
                }
                return;
            }

            from.playbackScheduledNextAnimation = null;
            from.playbackScheduledNextTimeLeft = float.NaN;
            from.playbackScheduledFadeOutAtRemaining = float.NaN;

            if (!to.playbackEnabled)
            {
                if (to.loop)
                {
                    var fromClipTime = from.clipTime - from.timeOffset;
                    if (!from.loop)
                        to.clipTime = Mathf.Abs(to.animationLength - (from.animationLength - fromClipTime)) + to.timeOffset;
                    else if (to.preserveLoops)
                        to.clipTime = (to.animationLength - (from.animationLength - fromClipTime)).Modulo(to.animationLength) + to.timeOffset;
                    else
                        to.clipTime = to.timeOffset;
                }
                else
                {
                    to.clipTime = to.timeOffset;
                }
            }

            // if(!from.loop && to.blendInDuration > from.animationLength - from.clipTime)
            //     SuperController.LogError($"Timeline: Transition from '{from.animationName}' to '{to.animationName}' will stop the former animation after it ends, because the blend-in time of the latter is too long for the sequenced time.");
            from.playbackMainInLayer = false;
            BlendOut(from, to.blendInDuration);
            to.playbackMainInLayer = true;
            BlendIn(to, to.blendInDuration);

            onMainClipPerLayerChanged.Invoke(new AtomAnimationChangeClipEventArgs { before = from, after = to });

            if (!isQueueActive)
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
            if (source == null) return;
            if (source.playbackScheduledNextAnimation != null) return;

            if (isQueueActive)
            {
                AtomAnimationClip nextClipFromQueue;
                if (TryGetNextClipFromQueue(source, queueIndex + 1, out nextClipFromQueue))
                {
                    ScheduleNextAnimation(source, nextClipFromQueue);
                    queueIndex++;
                    return;
                }
                else
                {
                    if (logger.sequencing) logger.Log(logger.sequencingCategory, "Queue finished or next clip not found, deactivating.");
                    DeactivateQueue();
                    onQueueFinished.Invoke();
                }
            }

            if (source.nextAnimationNameId == -1) return;
            if (clips.Count == 1) return;

            if (source.loop && source.nextAnimationTime <= 0)
                return;

            if (source.nextAnimationNameId == AtomAnimationClip.SlaveAnimationNameId)
                return;

            AtomAnimationClip next;

            if (source.nextAnimationSegmentRefId != -1)
            {
                AtomAnimationsClipsIndex.IndexedSegment segment;
                if (index.segmentsById.TryGetValue(source.nextAnimationSegmentRefId, out segment))
                {
                    next = segment.mainClip;
                }
                else
                {
                    next = null;
                    SuperController.LogError($"Timeline: Animation '{source.animationNameQualified}' could not transition to segment '{source.nextAnimationName}' because it did not exist");
                }
            }
            else if (source.nextAnimationNameId == AtomAnimationClip.RandomizeAnimationNameId)
            {
                var candidates = index
                    .ByLayerQualified(source.animationLayerQualifiedId)
                    .Where(c => c != source)
                    .ToList();
                next = SelectRandomClip(candidates);
            }
            else if (source.nextAnimationGroupId != -1)
            {
                var candidates = index
                    .ByLayerQualified(source.animationLayerQualifiedId)
                    .Where(c => c != source && c.animationNameGroupId == source.nextAnimationGroupId && (!source.nextAnimationPreventGroupExit || c.animationNameGroupId == c.nextAnimationGroupId))
                    .ToList();
                next = SelectRandomClip(candidates);
            }
            else
            {
                next = index.ByLayerQualified(source.animationLayerQualifiedId).FirstOrDefault(c => c.animationNameId == source.nextAnimationNameId);
            }

            if (next == null) return;

            ScheduleNextAnimation(source, next);
        }

        private void ScheduleNextAnimation(AtomAnimationClip source, AtomAnimationClip next)
        {
            var nextTime = source.nextAnimationTime;

            if (isQueueActive && source.playbackScheduledNextAnimation == null)
            {
                if (source.loop)
                {
                    nextTime = source.animationLength - next.blendInDuration;

                    if (source.clipTime > source.animationLength * 0.9f)
                    {
                        nextTime = source.animationLength + (source.animationLength - next.blendInDuration);
                    }
                }
                else
                {
                    float remainingTime = source.animationLength - source.clipTime;
                    nextTime = remainingTime - next.blendInDuration;
                }
            }
            else if (source.loop)
            {
                if (source.preserveLoops)
                {
                    if (source.nextAnimationTimeRandomize > 0f)
                    {
                        nextTime += Random
                            .Range(source.animationLength * -0.49f, source.nextAnimationTimeRandomize.RoundToNearest(source.animationLength) + source.animationLength * 0.49f)
                            .RoundToNearest(source.animationLength);
                    }
                    else
                    {

                        nextTime = nextTime.RoundToNearest(source.animationLength);
                    }

                    if (source.clipTime > 0f)
                        nextTime += source.animationLength - source.clipTime;
                }
                else if (source.nextAnimationTimeRandomize > 0f)
                {
                    nextTime = Random.Range(nextTime, nextTime + source.nextAnimationTimeRandomize);
                }
                nextTime -= next.halfBlendInDuration;
            }
            else
            {
                // NOTE: For compatibility reasons, regardless of preserveLength we wait for the end
                nextTime = Mathf.Min(nextTime, source.animationLength - next.blendInDuration);
            }

            if (nextTime < float.Epsilon)
            {
                // SuperController.LogError($"Timeline: Blending from animation {source.animationNameQualified} to {next.animationNameQualified} with blend time {next.blendInDuration} results in negative value: {nextTime}. Transition will be skipped.");
                nextTime = 0f;
            }

            if (logger.sequencing && isQueueActive)
            {
                logger.Log(logger.sequencingCategory,
                    $"Queue mode transition scheduled in {nextTime}s (clipTime={source.clipTime}, animLength={source.animationLength})");
            }

            ScheduleNextAnimation(source, next, nextTime);
        }

        private void ScheduleNextAnimation(AtomAnimationClip source, AtomAnimationClip next, float nextTime)
        {
            if (source.playbackScheduledNextAnimation == next && Mathf.Approximately(source.playbackScheduledNextTimeLeft, nextTime))
                return;

            source.playbackScheduledNextAnimation = next;
            source.playbackScheduledNextTimeLeft = nextTime;
            source.playbackScheduledFadeOutAtRemaining = float.NaN;

            if (logger.sequencing) logger.Log(logger.sequencingCategory, $"Schedule transition '{source.animationNameQualified}' -> '{next.animationName}' in {nextTime:0.000}s");

            if (next.fadeOnTransition && next.animationLayer == index.segmentsById[next.animationSegmentId].layerNames[0] && fadeManager != null)
            {
                source.playbackScheduledFadeOutAtRemaining = (fadeManager.fadeOutTime + fadeManager.halfBlackTime) * source.speed * globalSpeed;
                if (source.playbackScheduledNextTimeLeft < source.playbackScheduledFadeOutAtRemaining)
                {
                    if (logger.sequencing) logger.Log(logger.sequencingCategory, $"Fade out instantly {source.playbackScheduledNextTimeLeft:0.000}s before transition.");
                    fadeManager.FadeOutInstant();
                    source.playbackScheduledFadeOutAtRemaining = float.NaN;
                }
            }
        }

        private bool TryGetNextClipFromQueue(AtomAnimationClip currentClip, int targetQueueIndex, out AtomAnimationClip nextClip)
        {
            nextClip = null;
            if (targetQueueIndex < 0 || targetQueueIndex >= animationQueue.Count)
            {
                return false;
            }

            var nextAnimationName = animationQueue[targetQueueIndex];
            nextClip = FindClipInPriorityOrder(nextAnimationName, currentClip.animationSegmentId, currentClip.animationLayer);

            if (nextClip == null)
            {
                logger.Log(logger.sequencingCategory, $"Could not find animation '{nextAnimationName}' from queue at index {targetQueueIndex}.");
                return false;
            }

            if (logger.sequencing) logger.Log(logger.sequencingCategory, $"Queue providing next clip: '{nextClip.animationNameQualified}' (from index {targetQueueIndex})");
            return true;
        }

        #endregion
    }
}
