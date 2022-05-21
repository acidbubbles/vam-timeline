using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace VamTimeline
{
    public partial class AtomAnimation
    {
        #region Playback (API)

        public void PlayClipByName(string animationName, bool seq)
        {
            var clipsByName = index.ByName(animationName);
            var clip = clipsByName.FirstOrDefault(c => c.animationSegment == playingAnimationSegment) ?? clipsByName.FirstOrDefault();
            if (clip == null) return;
            PlayClip(clip, seq);
        }

        public void PlayClipBySet(string animationName, string animationSet, string animationSegment, bool seq)
        {
            if (!index.segmentNames.Contains(animationSegment))
                return;

            PlayClipBySet(animationName.ToId(), animationSet.ToId(), animationSegment.ToId(), seq);
        }

        public void PlayClipBySet(int animationNameId, int animationSetId, int animationSegmentId, bool seq)
        {
            var siblings = GetMainAndBestSiblingPerLayer(animationSegmentId, animationNameId, animationSetId);

            if (animationSegmentId != playingAnimationSegmentId && animationSegmentId != AtomAnimationClip.SharedAnimationSegmentId && animationSegmentId != AtomAnimationClip.NoneAnimationSegmentId)
            {
                PlaySegment(siblings[0].target);
                for (var i = 0; i < siblings.Count; i++)
                {
                    siblings[i] = new TransitionTarget { target = siblings[i].target };
                }
            }

            for (var i = 0; i < siblings.Count; i++)
            {
                var clip = siblings[i];
                if (clip.target == null) continue;
                if(isPlaying && clip.main != null)
                    PlayClipCore(clip.main, clip.target, seq, true, false);
                else
                    PlayClipCore(null, clip.target, seq, true, false);
            }
        }

        public void PlayRandom(string groupName = null)
        {
            var candidates = clips
                .Where(c => !c.playbackMainInLayer && (groupName == null || c.animationNameGroup == groupName))
                .ToList();

            if (candidates.Count == 0)
                return;

            var clip = SelectRandomClip(candidates);
            PlayClip(clip, true);
        }

        public void PlayClip(AtomAnimationClip clip, bool seq, bool allowPreserveLoops = true)
        {
            paused = false;
            if (clip.playbackMainInLayer) return;

            PlayClipCore(
                isPlaying
                    ? GetMainClipInLayer(index.ByLayerQualified(clip.animationLayerQualifiedId))
                    : null,
                clip,
                seq,
                allowPreserveLoops,
                true
            );
        }

        public void PlaySegment(string segmentName, bool seq = true)
        {
            AtomAnimationsClipsIndex.IndexedSegment segment;
            if (!index.segmentsById.TryGetValue(segmentName.ToId(), out segment))
                return;
            PlaySegment(segment.mainClip, seq);
        }

        public void PlaySegment(AtomAnimationClip source, bool seq = true)
        {
            onSegmentPlayed.Invoke(source);

            var clipsToPlay = GetDefaultClipsPerLayer(source);

            if (!source.isOnSharedSegment && source.animationSegmentId != playingAnimationSegmentId)
            {
                playingAnimationSegment = source.animationSegment;
                var hasPose = clipsToPlay.Any(c => c.applyPoseOnTransition);
                if (hasPose)
                {
                    foreach (var clip in clips.Where(c => c.playbackEnabled && c.animationSegment != AtomAnimationClip.SharedAnimationSegment))
                    {
                        StopClip(clip);
                    }
                }
                else
                {
                    var blendOutDuration = clipsToPlay.FirstOrDefault(c => !c.isOnSharedSegment)?.blendInDuration ?? AtomAnimationClip.DefaultBlendDuration;
                    foreach (var clip in clips.Where(c => c.playbackMainInLayer && c.animationSegment != AtomAnimationClip.SharedAnimationSegment))
                    {
                        SoftStopClip(clip, blendOutDuration);
                    }
                }
            }

            foreach (var clip in clipsToPlay)
            {
                PlayClipCore(null, clip, seq, false, false);
            }
        }

        #endregion

        #region Playback (Core)

        private void PlayClipCore(AtomAnimationClip previous, AtomAnimationClip next, bool seq, bool allowPreserveLoops, bool allowSibling)
        {
            paused = false;

            if (previous != null && !previous.playbackMainInLayer)
                throw new InvalidOperationException($"PlayClip must receive an initial clip that is the main on its layer. {previous.animationNameQualified}");

            if (isPlaying && next.playbackMainInLayer)
                return;

            var isPlayingChanged = false;

            if (!isPlaying)
            {
                isPlayingChanged = true;
                isPlaying = true;
                Validate();
                sequencing = sequencing || seq;
                fadeManager?.SyncFadeTime();
                if (next.isOnSegment)
                    PlaySegment(next, sequencing);
            }


            if (next.isOnSegment && playingAnimationSegment != next.animationSegment)
            {
                PlaySegment(next, sequencing);
                return;
            }

            if (!next.playbackEnabled && sequencing)
                next.clipTime = next.timeOffset;

            float blendInDuration;

            var nextHasPose = next.applyPoseOnTransition && next.pose != null;

            if (previous != null)
            {
                if (previous.uninterruptible)
                    return;

                // Wait for the loop to sync or the non-loop to end
                if (allowPreserveLoops && !nextHasPose)
                {
                    if (previous.loop && previous.preserveLoops)
                    {
                        var nextTime = next.loop
                            ? previous.animationLength - next.blendInDuration / 2f - previous.clipTime
                            : previous.animationLength - next.blendInDuration - previous.clipTime;
                        if (nextTime < 0) nextTime += previous.animationLength;
                        ScheduleNextAnimation(previous, next, nextTime);
                        return;
                    }

                    if (!previous.loop && previous.preserveLength)
                    {
                        var nextTime = Mathf.Max(previous.animationLength - next.blendInDuration - previous.clipTime, 0f);
                        ScheduleNextAnimation(previous, next, nextTime);
                        return;
                    }
                }

                previous.playbackMainInLayer = false;
                previous.playbackScheduledNextAnimation = null;
                previous.playbackScheduledNextTimeLeft = float.NaN;

                // Blend immediately, but unlike TransitionClips, recording will ignore blending
                blendInDuration = next.recording || nextHasPose ? 0f : next.blendInDuration;
                BlendOut(previous, blendInDuration);
            }
            else
            {
                // Blend immediately (first animation to play on that layer)
                blendInDuration = next.recording ? 0f : next.blendInDuration;
            }

            BlendIn(next, blendInDuration);
            next.playbackMainInLayer = true;

            if (next.animationPattern)
            {
                next.animationPattern.SetBoolParamValue("loopOnce", false);
                next.animationPattern.ResetAndPlay();
            }

            if (isPlayingChanged)
                onIsPlayingChanged.Invoke(next);

            if (sequencing)
                AssignNextAnimation(next);

            if (allowSibling && nextHasPose)
            {
                foreach (var c in index.GetSiblingsByLayer(previous))
                {
                    c.playbackMainInLayer = false;
                    c.playbackScheduledNextAnimation = null;
                    c.playbackScheduledNextTimeLeft = float.NaN;
                    BlendOut(c, 0);
                }
            }

            if (allowSibling && (sequencing || !focusOnLayer))
                PlaySiblings(next);
        }

        private void Validate()
        {
            foreach (var controllerRef in animatables.controllers)
            {
                if (controllerRef.owned) continue;
                if (controllerRef.controller == null)
                    throw new InvalidOperationException("Timeline: An external controller has been removed");
            }
            foreach (var floatParamRef in animatables.storableFloats)
            {
                if(!floatParamRef.EnsureAvailable())
                    throw new InvalidOperationException($"Timeline: The storable {floatParamRef.storableId} has been removed");
            }
        }

        private void PlaySiblings(AtomAnimationClip clip)
        {
            var clipsByName = index.segmentsById[clip.animationSegmentId].clipMapByNameId[clip.animationNameId];

            var clipTime = clip.clipTime - clip.timeOffset;
            PlaySiblingsByName(clipsByName, clipTime);
            PlaySiblingsBySet(clip, clipsByName, clipTime);
        }

        private void PlaySiblingsByName(IList<AtomAnimationClip> clipsByName, float clipTime)
        {
            if (clipsByName.Count == 1) return;
            for (var i = 0; i < clipsByName.Count; i++)
            {
                var clip = clipsByName[i];
                if (clip.playbackMainInLayer) continue;
                TransitionClips(
                    GetMainClipInLayer(index.ByLayerQualified(clip.animationLayerQualifiedId)),
                    clip,
                    clipTime);
            }
        }

        private void PlaySiblingsBySet(AtomAnimationClip clip, IList<AtomAnimationClip> clipsByName, float clipTime)
        {
            if (clip.animationSet == null) return;
            var layers = index.segmentsById[clip.animationSegmentId].layers;
            for (var i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];
                if (LayerContainsClip(clipsByName, layer[0].animationLayerQualified)) continue;
                var sibling = GetSiblingInLayer(layer, clip.animationSet);
                if (sibling == null) continue;
                var main = GetMainClipInLayer(layer);
                TransitionClips(main, sibling, clipTime);
            }
        }

        private static bool LayerContainsClip(IList<AtomAnimationClip> clipsByName, string animationLayerQualified)
        {
            for (var j = 0; j < clipsByName.Count; j++)
            {
                if (clipsByName[j].animationLayerQualified == animationLayerQualified)
                    return true;
            }
            return false;
        }

        public void SoftStopClip(AtomAnimationClip clip, float blendOutDuration)
        {
            clip.playbackMainInLayer = false;
            clip.playbackScheduledNextAnimation = null;
            clip.playbackScheduledNextTimeLeft = float.NaN;
            BlendOut(clip, blendOutDuration);
        }

        private void StopClip(AtomAnimationClip clip)
        {
            if (clip.playbackEnabled)
            {
                if (logger.general) logger.Log(logger.generalCategory, $"Leave '{clip.animationNameQualified}' (stop)");
                clip.Leave();
                clip.Reset(false);
                if (clip.animationPattern)
                    clip.animationPattern.SetBoolParamValue("loopOnce", true);
                onClipIsPlayingChanged.Invoke(clip);
            }
            else
            {
                clip.playbackMainInLayer = false;
            }

            if (isPlaying)
            {
                if (!clips.Any(c => c.playbackMainInLayer))
                {
                    if (logger.general) logger.Log(logger.generalCategory, $"No animations currently playing, stopping Timeline");
                    isPlaying = false;
                    sequencing = false;
                    paused = false;
                    onIsPlayingChanged.Invoke(clip);
                }
            }
        }

        public void StopAll()
        {
            autoStop = 0f;

            foreach (var clip in clips)
            {
                StopClip(clip);
            }
            foreach (var clip in clips)
            {
                clip.Reset(false);
            }

            if (fadeManager?.black == true)
            {
                _scheduleFadeIn = float.MaxValue;
                fadeManager.FadeIn();
            }
        }

        public void ResetAll()
        {
            playTime = 0f;
            foreach (var clip in clips)
                clip.Reset(true);
        }

        public void StopAndReset()
        {
            if (isPlaying) StopAll();
            ResetAll();
        }

        #endregion

        #region Animation state

        private void AdvanceClipsTime(float delta)
        {
            if (delta == 0) return;

            var layers = index.clipsGroupedByLayer;
            for (var layerIndex = 0; layerIndex < layers.Count; layerIndex++)
            {
                var layerClips = layers[layerIndex];
                float clipSpeed;
                if (layerClips.Count > 1)
                {
                    var weightedClipSpeedSum = 0f;
                    var totalBlendWeights = 0f;
                    clipSpeed = 0f;
                    for (var i = 0; i < layerClips.Count; i++)
                    {
                        var clip = layerClips[i];
                        if (!clip.playbackEnabled) continue;
                        var blendWeight = clip.playbackBlendWeightSmoothed;
                        weightedClipSpeedSum += clip.speed * blendWeight;
                        totalBlendWeights += blendWeight;
                        clipSpeed = clip.speed;
                    }

                    clipSpeed = weightedClipSpeedSum == 0 ? clipSpeed : weightedClipSpeedSum / totalBlendWeights;
                }
                else
                {
                    clipSpeed = layerClips[0].speed;
                }

                for (var i = 0; i < layerClips.Count; i++)
                {
                    var clip = layerClips[i];
                    if (!clip.playbackEnabled) continue;

                    var clipDelta = delta * clipSpeed;
                    if (!ReferenceEquals(clip.audioSourceControl, null))
                    {
                        var audioTime = clip.audioSourceControl.audioSource.time;
                        // ReSharper disable once CompareOfFloatsByEqualityOperator
                        if (audioTime == clip.clipTime)
                        {
                            clip.clipTime += clipDelta * clip.audioSourceControl.audioSource.pitch;
                        }
                        else
                        {
                            clip.clipTime = audioTime;
                        }
                    }
                    else
                    {
                        clip.clipTime += clipDelta;
                    }

                    if (clip.playbackBlendRate != 0)
                    {
                        clip.playbackBlendWeight += clip.playbackBlendRate * Mathf.Abs(clipDelta);
                        if (clip.playbackBlendWeight >= clip.weight)
                        {
                            clip.playbackBlendRate = 0f;
                            clip.playbackBlendWeight = clip.weight;
                        }
                        else if (clip.playbackBlendWeight <= 0f)
                        {
                            if (!float.IsNaN(clip.playbackScheduledNextTimeLeft))
                            {
                                // Wait for the sequence time to be reached
                                clip.playbackBlendWeight = 0;
                            }
                            else
                            {
                                if (logger.general) logger.Log(logger.generalCategory, $"Leave '{clip.animationNameQualified}' (blend out complete)");
                                clip.Leave();
                                clip.Reset(true);
                                onClipIsPlayingChanged.Invoke(clip);
                            }
                        }
                    }
                }
            }
        }

        private void BlendIn(AtomAnimationClip clip, float blendDuration)
        {
            if (clip.applyPoseOnTransition)
            {
                if (!clip.recording && clip.pose != null)
                {
                    if (logger.sequencing)
                        logger.Log(logger.sequencingCategory, $"Applying pose '{clip.animationNameQualified}'");
                    clip.pose.Apply();
                    lastAppliedPose = clip.pose;
                }
                clip.playbackBlendWeight = 1f;
                clip.playbackBlendRate = 0f;
            }
            else if (blendDuration == 0)
            {
                clip.playbackBlendWeight = 1f;
                clip.playbackBlendRate = 0f;
            }
            else
            {
                if (!clip.playbackEnabled) clip.playbackBlendWeight = float.Epsilon;
                clip.playbackBlendRate = forceBlendTime
                    ? (1f - clip.playbackBlendWeight) / blendDuration
                    : 1f / blendDuration;
            }

            if (clip.playbackEnabled) return;

            clip.playbackEnabled = true;
            if (logger.general) logger.Log(logger.generalCategory, $"Enter '{clip.animationNameQualified}'");
            onClipIsPlayingChanged.Invoke(clip);
        }

        private void BlendOut(AtomAnimationClip clip, float blendDuration)
        {
            if (!clip.playbackEnabled) return;

            if (blendDuration == 0 || clip.playbackBlendWeight == 0)
            {
                if (logger.general) logger.Log(logger.generalCategory, $"Leave '{clip.animationNameQualified}' (immediate blend out)");
                clip.Leave();
                clip.Reset(true);
            }
            else
            {
                clip.playbackBlendRate = forceBlendTime
                    ? (-1f - clip.playbackBlendWeight) / blendDuration
                    : -1f / blendDuration;
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
            if (isPlaying && !paused || !enabled) return;

            SampleFloatParams();
            SampleControllers(true);
        }

        private void SyncTriggers(bool live)
        {
            for (var clipIndex = 0; clipIndex < clips.Count; clipIndex++)
            {
                var clip = clips[clipIndex];
                for (var triggerIndex = 0; triggerIndex < clip.targetTriggers.Count; triggerIndex++)
                {
                    var target = clip.targetTriggers[triggerIndex];
                    if (target.animatableRef.live != live) continue;
                    if (clip.playbackEnabled)
                    {
                        target.Sync(clip.clipTime, live);
                    }
                    target.Update();
                }
            }
        }

        [MethodImpl(256)]
        private void SampleFloatParams()
        {
            if (simulationFrozen) return;
            if (_globalScaledWeight <= 0) return;
            foreach (var x in index.ByFloatParam())
            {
                if (!x.Value[0].animatableRef.EnsureAvailable()) continue;
                SampleFloatParam(x.Value[0].animatableRef, x.Value);
            }
        }

        [MethodImpl(256)]
        private void SampleFloatParam(JSONStorableFloatRef floatParamRef, List<JSONStorableFloatAnimationTarget> targets)
        {
            const float minimumDelta = 0.00000015f;
            var weightedSum = 0f;
            var totalBlendWeights = 0f;
            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                var clip = target.clip;
                if(target.recording)
                {
                    target.SetKeyframeToCurrent(clip.clipTime.Snap(), false);
                    return;
                }
                if (!clip.playbackEnabled && !clip.temporarilyEnabled) continue;
                var localScaledWeight = clip.temporarilyEnabled ? 1f : clip.scaledWeight;
                if (localScaledWeight < float.Epsilon) continue;

                var value = target.value.Evaluate(clip.clipTime);
                var blendWeight = clip.temporarilyEnabled ? 1f : clip.playbackBlendWeightSmoothed;
                weightedSum += Mathf.Lerp(floatParamRef.val, value, localScaledWeight) * blendWeight;
                totalBlendWeights += blendWeight;
            }

            if (totalBlendWeights > minimumDelta)
            {
                var val = weightedSum / totalBlendWeights;
                if(Mathf.Abs(val - floatParamRef.val) > minimumDelta)
                {
                    floatParamRef.val = Mathf.Lerp(floatParamRef.val, val, _globalScaledWeight);
                }
            }
        }

        [MethodImpl(256)]
        private void SampleControllers(bool force = false)
        {
            if (simulationFrozen) return;
            if (_globalScaledWeight <= 0) return;
            foreach (var x in index.ByController())
            {
                SampleController(x.Key.controller, x.Value, force, x.Key.scaledWeight);
            }
        }

        public void SampleParentedControllers(AtomAnimationClip source)
        {
            if (simulationFrozen) return;
            if (_globalScaledWeight <= 0) return;
            // TODO: Index keep track if there is any parenting
            var layers = GetMainAndBestSiblingPerLayer(playingAnimationSegmentId, source.animationNameId, source.animationSetId);
            for (var layerIndex = 0; layerIndex < layers.Count; layerIndex++)
            {
                var clip = layers[layerIndex];
                if (clip.target == null) continue;
                for (var controllerIndex = 0; controllerIndex < clip.target.targetControllers.Count; controllerIndex++)
                {
                    var ctrl = clip.target.targetControllers[controllerIndex];
                    if (!ctrl.EnsureParentAvailable()) continue;
                    if (!ctrl.hasParentBound) continue;

                    var controller = ctrl.animatableRef.controller;
                    if (controller.isGrabbing) continue;
                    var positionRB = ctrl.GetPositionParentRB();
                    if (!ReferenceEquals(positionRB, null))
                    {
                        var targetPosition = positionRB.transform.TransformPoint(ctrl.EvaluatePosition(source.clipTime));
                        if (controller.currentPositionState != FreeControllerV3.PositionState.Off)
                            controller.control.position = Vector3.Lerp(controller.control.position, targetPosition, _globalWeight);
                    }

                    var rotationParentRB = ctrl.GetRotationParentRB();
                    if (!ReferenceEquals(rotationParentRB, null))
                    {
                        var targetRotation = rotationParentRB.rotation * ctrl.EvaluateRotation(source.clipTime);
                        if (controller.currentRotationState != FreeControllerV3.RotationState.Off)
                            controller.control.rotation = Quaternion.Slerp(controller.control.rotation, targetRotation, _globalWeight);
                    }
                }
            }
        }

        private Quaternion[] _rotations = new Quaternion[0];
        private float[] _rotationBlendWeights = new float[0];

        [MethodImpl(256)]
        private void SampleController(FreeControllerV3 controller, IList<FreeControllerV3AnimationTarget> targets, bool force, float animatableWeight)
        {
            if (ReferenceEquals(controller, null)) return;
            var control = controller.control;

            if (targets.Count > _rotations.Length)
            {
                _rotations = new Quaternion[targets.Count];
                _rotationBlendWeights = new float[targets.Count];
            }
            var rotationCount = 0;
            var totalRotationBlendWeights = 0f;
            var totalRotationControlWeights = 0f;

            var weightedPositionSum = Vector3.zero;
            var totalPositionBlendWeights = 0f;
            var totalPositionControlWeights = 0f;
            var animatedCount = 0;

            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                var clip = target.clip;
                if(target.recording)
                {
                    target.SetKeyframeToCurrent(clip.clipTime.Snap(), false);
                    return;
                }
                if (!clip.playbackEnabled && !clip.temporarilyEnabled) continue;
                if (!target.playbackEnabled) continue;
                if (controller.possessed) return;
                if (controller.isGrabbing) return;
                var weight = clip.temporarilyEnabled ? 1f : clip.scaledWeight * target.scaledWeight;
                if (weight < float.Epsilon) continue;

                if (!target.EnsureParentAvailable()) return;

                var blendWeight = clip.temporarilyEnabled ? 1f : clip.playbackBlendWeightSmoothed;

                if (target.controlRotation && controller.currentRotationState != FreeControllerV3.RotationState.Off)
                {
                    var rotLink = target.GetPositionParentRB();
                    var hasRotLink = !ReferenceEquals(rotLink, null);

                    var targetRotation = target.EvaluateRotation(clip.clipTime);
                    if (hasRotLink)
                    {
                        targetRotation = rotLink.rotation * targetRotation;
                        _rotations[rotationCount] = targetRotation;
                    }
                    else
                    {
                        _rotations[rotationCount] = control.transform.parent.rotation * targetRotation;
                    }

                    _rotationBlendWeights[rotationCount] = blendWeight;
                    totalRotationBlendWeights += blendWeight;
                    totalRotationControlWeights += weight * blendWeight;
                    rotationCount++;
                }

                if (target.controlPosition && controller.currentPositionState != FreeControllerV3.PositionState.Off)
                {
                    var posLink = target.GetPositionParentRB();
                    var hasPosLink = !ReferenceEquals(posLink, null);

                    var targetPosition = target.EvaluatePosition(clip.clipTime);
                    if (hasPosLink)
                    {
                        targetPosition = posLink.transform.TransformPoint(targetPosition);
                    }
                    else
                    {
                        targetPosition = control.transform.parent.TransformPoint(targetPosition);
                    }

                    weightedPositionSum += targetPosition * blendWeight;
                    totalPositionBlendWeights += blendWeight;
                    totalPositionControlWeights += weight * blendWeight;
                    animatedCount++;
                }
            }

            if (totalRotationBlendWeights > float.Epsilon && controller.currentRotationState != FreeControllerV3.RotationState.Off)
            {
                Quaternion targetRotation;
                if (rotationCount > 1)
                {
                    var cumulative = Vector4.zero;
                    for (var i = 0; i < rotationCount; i++)
                    {
                        QuaternionUtil.AverageQuaternion(ref cumulative, _rotations[i], _rotations[0], _rotationBlendWeights[i] / totalRotationBlendWeights);
                    }
                    targetRotation = QuaternionUtil.FromVector(cumulative);
                }
                else
                {
                    targetRotation = _rotations[0];
                }

                var controlWeight = animatedCount == 1 ? totalRotationControlWeights : totalRotationControlWeights / totalRotationBlendWeights;
                var rotation = Quaternion.Slerp(control.rotation, targetRotation, controlWeight * _globalScaledWeight * animatableWeight);
                control.rotation = rotation;
            }

            if (totalPositionBlendWeights > float.Epsilon && controller.currentPositionState != FreeControllerV3.PositionState.Off)
            {
                var targetPosition = weightedPositionSum / totalPositionBlendWeights;
                var controlWeight = animatedCount == 1 ? totalPositionControlWeights : (totalPositionControlWeights / totalPositionBlendWeights);

                var position = Vector3.Lerp(control.position, targetPosition, controlWeight * _globalScaledWeight * animatableWeight);
                control.position = position;
            }

            if (force && controller.currentPositionState == FreeControllerV3.PositionState.Comply || controller.currentRotationState == FreeControllerV3.RotationState.Comply)
                controller.PauseComply();
        }

        #endregion
    }
}
