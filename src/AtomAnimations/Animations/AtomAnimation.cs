using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Events;
using Random = UnityEngine.Random;

namespace VamTimeline
{
    public class AtomAnimation : MonoBehaviour
    {
        public class IsPlayingEvent : UnityEvent<AtomAnimationClip> { }

        private static readonly Regex _lastDigitsRegex = new Regex(@"[0-9]+$");

        public const string RandomizeAnimationName = "(Randomize)";
        public const string SlaveAnimationName = "(Slave)";
        public const string RandomizeGroupSuffix = "/*";
        public const string NextAnimationSegmentPrefix = "Segment: ";

        public readonly UnityEvent onAnimationSettingsChanged = new UnityEvent();
        public readonly UnityEvent onSpeedChanged = new UnityEvent();
        public readonly UnityEvent onWeightChanged = new UnityEvent();
        public readonly UnityEvent onClipsListChanged = new UnityEvent();
        public readonly UnityEvent onAnimationRebuilt = new UnityEvent();
        public readonly UnityEvent onPausedChanged = new UnityEvent();
        public readonly IsPlayingEvent onIsPlayingChanged = new IsPlayingEvent();
        public readonly IsPlayingEvent onClipIsPlayingChanged = new IsPlayingEvent();

        public Logger logger;

        public bool recording;

        public IFadeManager fadeManager;
        private float _scheduleFadeIn = float.MaxValue;

        public List<AtomAnimationClip> clips { get; } = new List<AtomAnimationClip>();
        public bool isPlaying { get; private set; }
        public string playingAnimationSegment;
        public float autoStop;
        private bool _paused;
        public bool paused
        {
            get
            {
                return _paused;
            }
            set
            {
                var dispatch = value != _paused;
                _paused = value;
                if (dispatch)
                    onPausedChanged.Invoke();
            }
        }
        private bool allowAnimationProcessing => isPlaying && !SuperController.singleton.freezeAnimation;

        public int timeMode { get; set; } = TimeModes.RealTime;

        public bool liveParenting { get; set; } = true;

        public bool master { get; set; }

        public bool simulationFrozen;

        public float playTime { get; private set; }

        private float _globalSpeed = 1f;
        public float globalSpeed
        {
            get
            {
                return _globalSpeed;
            }

            set
            {
                _globalSpeed = value;
                for (var i = 0; i < clips.Count; i++)
                {
                    var clip = clips[i];
                    if (clip.animationPattern != null)
                        clip.animationPattern.SetFloatParamValue("speed", value);
                }

                onSpeedChanged.Invoke();
            }
        }

        private float _globalWeight = 1f;
        private float _globalScaledWeight = 1f;
        public float globalWeight
        {
            get
            {
                return _globalWeight;
            }

            set
            {
                _globalWeight = Mathf.Clamp01(value);
                _globalScaledWeight = value.ExponentialScale(0.1f, 1f);
                onWeightChanged.Invoke();
            }
        }

        public bool sequencing { get; private set; }

        private bool _animationRebuildRequestPending;
        private bool _animationRebuildInProgress;

        public AtomAnimationsClipsIndex index { get; }
        public AnimatablesRegistry animatables { get; }

        public bool syncSubsceneOnly { get; set; }
        public bool syncWithPeers { get; set; } = true;
        public bool forceBlendTime { get; set; }

        public AtomAnimation()
        {
            index = new AtomAnimationsClipsIndex(clips);
            animatables = new AnimatablesRegistry();
        }

        public AtomAnimationClip GetDefaultClip()
        {
            return index.ByLayer(clips[0].animationLayerQualified).FirstOrDefault(c => c.autoPlay) ?? clips[0];
        }

        public bool IsEmpty()
        {
            if (clips.Count == 0) return true;
            return clips.Count == 1 && clips[0].IsEmpty();
        }

        #region Clips

        public AtomAnimationClip GetClip(string animationSegment, string animationLayer, string animationName)
        {
            return clips.FirstOrDefault(c => c.animationSegment == animationSegment && c.animationLayer == animationLayer && c.animationName == animationName);
        }

        public IList<AtomAnimationClip> GetClips(string animationName)
        {
            return index.ByName(animationName);
        }

        public AtomAnimationClip GetClipQualified(string animationNameQualified)
        {
            return clips.FirstOrDefault(c => c.animationNameQualified == animationNameQualified);
        }

        public AtomAnimationClip AddClip(AtomAnimationClip clip)
        {
            var lastIndexOfSequence = clips.FindLastIndex(c => c.animationSegment == clip.animationSegment);
            var lastIndexOfLayer = clips.FindLastIndex(c => c.animationLayerQualified == clip.animationLayerQualified);
            int addIndex;
            if (lastIndexOfLayer > -1)
                addIndex = lastIndexOfLayer + 1;
            else if (lastIndexOfSequence > -1)
                addIndex = lastIndexOfSequence + 1;
            else
                addIndex = clips.Count;
            AddClipAt(clip, addIndex);
            clip.onAnimationSettingsChanged.AddListener(OnAnimationSettingsChanged);
            clip.onAnimationKeyframesDirty.AddListener(OnAnimationKeyframesDirty);
            clip.onTargetsListChanged.AddListener(OnTargetsListChanged);
            index.Rebuild();
            onClipsListChanged.Invoke();
            if (clip.IsDirty()) clip.onAnimationKeyframesDirty.Invoke();
            return clip;
        }

        public AtomAnimationClip AddClipAt(AtomAnimationClip clip, int i)
        {
            if (i == -1 || i > clips.Count) throw new ArgumentOutOfRangeException($"Tried to add clip {clip.animationNameQualified} at position {i} but there are {clips.Count} clips");
            clips.Insert(i, clip);
            if (playingAnimationSegment == null && clip.animationSegment != AtomAnimationClip.SharedAnimationSegment)
                playingAnimationSegment = clip.animationSegment;
            clip.onAnimationSettingsChanged.AddListener(OnAnimationSettingsChanged);
            clip.onAnimationKeyframesDirty.AddListener(OnAnimationKeyframesDirty);
            clip.onTargetsListChanged.AddListener(OnTargetsListChanged);
            index.Rebuild();
            onClipsListChanged.Invoke();
            if (clip.IsDirty()) clip.onAnimationKeyframesDirty.Invoke();
            return clip;
        }

        public AtomAnimationClip CreateClip([NotNull] string animationLayer, [NotNull] string animationName, string animationSegment, int position = -1)
        {
            if (animationLayer == null) throw new ArgumentNullException(nameof(animationLayer));
            if (animationName == null) throw new ArgumentNullException(nameof(animationName));

            if (clips.Any(c => c.animationSegment == animationSegment && c.animationLayer == animationLayer && c.animationName == animationName))
                throw new InvalidOperationException($"Animation '{animationSegment}::{animationLayer}::{animationName}' already exists");
            var clip = new AtomAnimationClip(animationName, animationLayer, animationSegment);
            if (position == -1)
                AddClip(clip);
            else
                AddClipAt(clip, position);
            return clip;
        }

        public void RemoveClip(AtomAnimationClip clip)
        {
            clips.Remove(clip);
            clip.Dispose();
            index.Rebuild();
            onClipsListChanged.Invoke();
            OnAnimationKeyframesDirty();
        }

        public string GetUniqueAnimationName(AtomAnimationClip source)
        {
            return GetUniqueAnimationName(source.animationName);
        }

        public string GetUniqueAnimationName(string sourceName)
        {
            return GetUniqueName(sourceName, clips.Select(c => c.animationName).ToList());
        }

        public string GetUniqueLayerName(AtomAnimationClip source, string baseName = null)
        {
            return GetUniqueName(baseName ?? source.animationLayer, index.segments[source.animationSegment].layerNames);
        }

        public string GetUniqueSegmentName(AtomAnimationClip source)
        {
            return GetUniqueSegmentName(source.animationSegment);
        }

        public string GetUniqueSegmentName(string sourceSegmentName)
        {
            return GetUniqueName(sourceSegmentName, index.segmentNames.Where(s => s != AtomAnimationClip.SharedAnimationSegment).ToList());
        }

        public string GetUniqueName(string sourceName, IList<string> existingNames)
        {
            if (!existingNames.Contains(sourceName))
                return sourceName;

            var match = _lastDigitsRegex.Match(sourceName);
            string itemNameBeforeInt;
            int itemNameInt;
            if (!match.Success)
            {
                itemNameBeforeInt = $"{sourceName.TrimEnd()} ";
                itemNameInt = 1;
            }
            else
            {
                itemNameBeforeInt = sourceName.Substring(0, match.Index);
                itemNameInt = int.Parse(match.Value);
            }
            for (var i = itemNameInt + 1; i < 999; i++)
            {
                var itemName = itemNameBeforeInt + i;
                if (existingNames.All(n => n != itemName))
                    return itemName;
            }
            return Guid.NewGuid().ToString();
        }

        public IEnumerable<string> EnumerateLayers(string animationSegment)
        {
            switch (clips.Count)
            {
                case 0:
                    yield break;
                case 1:
                    if (clips[0].animationSegment != animationSegment) throw new InvalidOperationException($"EnumerateLayers('{animationSegment}') but only sequence is '{clips[0].animationSegment}'");
                    yield return clips[0].animationLayer;
                    yield break;
            }

            string lastLayer = null;
            var clipIndex = 0;
            for (; clipIndex < clips.Count; clipIndex++)
            {
                var clip = clips[clipIndex];
                if (clip.animationSegment == animationSegment)
                    lastLayer = clip.animationLayer;
            }

            if (lastLayer == null) throw new InvalidOperationException($"No layers in animation sequence '{animationSegment}'");

            yield return lastLayer;
            for (clipIndex++; clipIndex < clips.Count; clipIndex++)
            {
                var clip = clips[clipIndex];
                if (clip.animationLayer == lastLayer) continue;
                if (clip.animationSegment != animationSegment) yield break;
                yield return lastLayer = clip.animationLayer;
            }
        }

        public void Clear()
        {
            var list = clips.ToList();
            for (var i = 0; i < list.Count; i++)
            {
                var clip = list[i];
                RemoveClip(clip);
            }

            globalSpeed = 1f;
            playTime = 0f;
        }

        #endregion

        #region Playback (API)

        public void PlayClipByName(string animationName, bool seq)
        {
            var clipsByName = index.ByName(animationName);
            if (clipsByName.Count == 0) return;
            PlayClip(clipsByName[0], seq);
        }

        public void PlayClipBySet(string animationName, string animationSet, string animationSegment, bool seq)
        {
            if (!index.segmentNames.Contains(animationSegment))
                return;

            var siblings = GetMainAndBestSiblingPerLayer(animationSegment, animationName, animationSet);

            if (animationSegment != playingAnimationSegment && animationSegment != AtomAnimationClip.SharedAnimationSegment)
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
                    PlayClipCore(clip.main, clip.target, seq, true);
                else
                    PlayClipCore(null, clip.target, seq, true);
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
                    ? GetMainClipInLayer(index.ByLayer(clip.animationLayerQualified))
                    : null,
                clip,
                seq,
                allowPreserveLoops
            );
        }

        public void PlaySegment(AtomAnimationClip source)
        {
            var clipsToPlay = GetDefaultClipsPerLayer(source);

            if (source.animationSegment != AtomAnimationClip.SharedAnimationSegment && source.animationSegment != playingAnimationSegment)
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
                    var blendOutTime = clipsToPlay.Min(c => c.blendInDuration);
                    foreach (var clip in clips.Where(c => c.playbackMainInLayer && c.animationSegment != AtomAnimationClip.SharedAnimationSegment))
                    {
                        SoftStopClip(clip, blendOutTime);
                    }
                }
            }

            foreach (var clip in clipsToPlay)
            {
                if (clip == null) continue;
                PlayClip(clip, true);
            }
        }

        #endregion

        #region Playback (Core)

        private void PlayClipCore(AtomAnimationClip previous, AtomAnimationClip next, bool seq, bool allowPreserveLoops)
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
                sequencing = sequencing || seq;
                fadeManager?.SyncFadeTime();
                if (next.animationSegment != AtomAnimationClip.SharedAnimationSegment)
                    playingAnimationSegment = next.animationSegment;
            }

            if (sequencing && !next.playbackEnabled)
                next.clipTime = 0;

            if (next.animationSegment != AtomAnimationClip.SharedAnimationSegment && playingAnimationSegment != next.animationSegment)
            {
                PlaySegment(next);
                return;
            }

            if (previous != null)
            {
                if (previous.uninterruptible)
                    return;

                // Wait for the loop to sync
                if (previous.loop && previous.preserveLoops && next.loop && next.preserveLoops && allowPreserveLoops)
                {
                    ScheduleNextAnimation(
                        previous,
                        next,
                        previous.loop && previous.preserveLoops && next.loop && next.preserveLoops
                            ? previous.animationLength - next.blendInDuration / 2f - previous.clipTime
                            : 0f);

                    return;
                }

                previous.playbackMainInLayer = false;
                previous.playbackScheduledNextAnimation = null;
                previous.playbackScheduledNextTimeLeft = float.NaN;

                // Blend immediately, but unlike TransitionClips, recording will ignore blending
                var blendInDuration = next.recording ? 0f : next.blendInDuration;
                BlendOut(previous, blendInDuration);
                if (next.clipTime >= next.animationLength) next.clipTime = 0f;
                BlendIn(next, blendInDuration);
                next.playbackMainInLayer = true;
            }
            else
            {
                // Blend immediately (first animation to play on that layer)
                var blendInDuration = next.recording ? 0f : next.blendInDuration;
                if (next.clipTime >= next.animationLength) next.clipTime = 0f;
                BlendIn(next, blendInDuration);
                next.playbackMainInLayer = true;
            }

            if (next.animationPattern)
            {
                next.animationPattern.SetBoolParamValue("loopOnce", false);
                next.animationPattern.ResetAndPlay();
            }

            if (isPlayingChanged)
                onIsPlayingChanged.Invoke(next);

            if (seq)
                AssignNextAnimation(next);

            PlaySiblings(next);
        }

        private void PlaySiblings(AtomAnimationClip clip)
        {
            PlaySiblings(clip.animationSegment, clip.animationName, clip.animationSet);
        }

        private void PlaySiblings(string animationSegment, string animationName, string animationSet)
        {
            var clipsByName = index.ByName(animationName);

            PlaySiblingsByName(clipsByName);

            if (animationSet == null) return;

            var layers = index.segments[animationSegment].layers;
            for (var i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];
                if (LayerContainsClip(clipsByName, layer[0].animationLayerQualified)) continue;
                var sibling = GetSiblingInLayer(layer, animationSet);
                if (sibling == null) continue;
                var main = GetMainClipInLayer(layer);
                TransitionClips(main, sibling);
            }
        }

        private void PlaySiblingsByName(IList<AtomAnimationClip> clipsByName)
        {
            if (clipsByName.Count == 1) return;
            for (var i = 0; i < clipsByName.Count; i++)
            {
                var clip = clipsByName[i];
                if (clip.playbackMainInLayer) continue;
                TransitionClips(
                    GetMainClipInLayer(index.ByLayer(clip.animationLayerQualified)),
                    clip);
            }
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

        #region Clips Listing

        public struct TransitionTarget
        {
            public AtomAnimationClip main;
            public AtomAnimationClip target;
        }

        private static AtomAnimationClip GetMainClipInLayer(IList<AtomAnimationClip> layer)
        {
            for (var i = 0; i < layer.Count; i++)
            {
                var layerClip = layer[i];
                if (layerClip.playbackMainInLayer) return layerClip;
            }
            return null;
        }

        private IList<TransitionTarget> GetMainAndBestSiblingPerLayer(string animationSegment, string animationName, string animationSet)
        {
            AtomAnimationsClipsIndex.IndexedSegment sharedLayers;
            if (!index.segments.TryGetValue(AtomAnimationClip.SharedAnimationSegment, out sharedLayers))
            {
                sharedLayers = index.emptySegment;
            }

            AtomAnimationsClipsIndex.IndexedSegment segmentLayers;
            if (animationSegment != AtomAnimationClip.SharedAnimationSegment)
                segmentLayers = index.segments[animationSegment];
            else
                segmentLayers = index.emptySegment;

            var result = new TransitionTarget[sharedLayers.layers.Count + segmentLayers.layers.Count];
            for (var i = 0; i < sharedLayers.layers.Count; i++)
            {
                var layer = sharedLayers.layers[i];
                result[i] = GetMainAndBestSiblingInLayer(layer, animationName, animationSet);
            }
            for (var i = 0; i < segmentLayers.layers.Count; i++)
            {
                var layer = segmentLayers.layers[i];
                result[sharedLayers.layers.Count + i] = GetMainAndBestSiblingInLayer(layer, animationName, animationSet);
            }
            return result;
        }

        private static TransitionTarget GetMainAndBestSiblingInLayer(IList<AtomAnimationClip> layer, string animationName, string animationSet)
        {
            var main = GetMainClipInLayer(layer);
            AtomAnimationClip bestSibling = null;
            for (var j = 0; j < layer.Count; j++)
            {
                var clip = layer[j];
                if (clip.animationName == animationName)
                {
                    bestSibling = clip;
                    break;
                }

                if (animationSet != null && clip.animationSet == animationSet)
                {
                    if (bestSibling == null || clip.playbackMainInLayer)
                        bestSibling = clip;
                }
            }

            return new TransitionTarget
            {
                main = main,
                target = bestSibling
            };
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

        private static AtomAnimationClip GetSiblingInLayer(IList<AtomAnimationClip> layer, string animationSet)
        {
            AtomAnimationClip sibling = null;
            for (var j = 0; j < layer.Count; j++)
            {
                var clip = layer[j];
                if (clip.playbackMainInLayer)
                {
                    if (clip.animationSet == animationSet)
                    {
                        sibling = clip;
                        break;
                    }

                    continue;
                }

                if (clip.animationSet == animationSet)
                {
                    sibling = clip;
                }
            }

            return sibling;
        }

        public IList<AtomAnimationClip> GetDefaultClipsPerLayer(AtomAnimationClip source)
        {
            AtomAnimationsClipsIndex.IndexedSegment sharedLayers;
            if (!index.segments.TryGetValue(AtomAnimationClip.SharedAnimationSegment, out sharedLayers))
            {
                sharedLayers = index.emptySegment;
            }

            AtomAnimationsClipsIndex.IndexedSegment segmentLayers;
            if (source.animationSegment != AtomAnimationClip.SharedAnimationSegment)
                segmentLayers = index.segments[source.animationSegment];
            else
                segmentLayers = index.emptySegment;

            var list = new AtomAnimationClip[sharedLayers.layers.Count + segmentLayers.layers.Count];

            for (var i = 0; i < sharedLayers.layers.Count; i++)
            {
                list[i] = GetDefaultClipInLayer(sharedLayers.layers[i], source);
            }
            for (var i = 0; i < segmentLayers.layers.Count; i++)
            {
                list[sharedLayers.layers.Count + i] = GetDefaultClipInLayer(segmentLayers.layers[i], source);
            }

            // Always start with the selected clip to avoid animation sets starting another animation on the currently shown layer
            var currentIdx = Array.IndexOf(list, source);
            if (currentIdx > -1)
            {
                list[currentIdx] = list[0];
                list[0] = source;
            }

            return list;
        }

        private static AtomAnimationClip GetDefaultClipInLayer(IList<AtomAnimationClip> layer, AtomAnimationClip source)
        {
            if (layer[0].animationLayerQualified == source.animationLayerQualified)
                return source;

            if (source.animationSet != null)
            {
                var clip = layer.FirstOrDefault(c => c.animationSet == source.animationSet);
                // This is to prevent playing on the main layer, starting a set on another layer, which will then override the clip you just played on the main layer
                if (clip?.animationSet != null && clip.animationSet != source.animationSet)
                    clip = null;
                if (clip != null)
                    return clip;
            }

            return layer.FirstOrDefault(c => c.playbackMainInLayer) ??
                   layer.FirstOrDefault(c => c.animationName == source.animationName) ??
                   layer.FirstOrDefault(c => c.autoPlay) ??
                   layer[0];
        }

        private static AtomAnimationClip SelectRandomClip(IList<AtomAnimationClip> candidates)
        {
            if (candidates.Count == 1) return candidates[0];
            var weightSum = candidates.Sum(c => c.nextAnimationRandomizeWeight);
            var val = Random.Range(0f, weightSum);
            var cumulativeWeight = 0f;
            for (var i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];
                cumulativeWeight += c.nextAnimationRandomizeWeight;
                if (val < cumulativeWeight)
                    return c;
            }
            return candidates[candidates.Count - 1];
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
                if (!clip.recording)
                {
                    if (logger.sequencing)
                        logger.Log(logger.sequencingCategory, $"Applying pose '{clip.animationNameQualified}'");
                    clip.pose?.Apply();
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

            if (blendDuration == 0)
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

        #region Transitions and sequencing

        private void TransitionClips(AtomAnimationClip from, AtomAnimationClip to)
        {
            if (to == null) throw new ArgumentNullException(nameof(to));

            if (from == null)
            {
                BlendIn(to, to.blendInDuration);
                to.playbackMainInLayer = true;
                if (!ReferenceEquals(to.animationPattern, null))
                {
                    to.animationPattern.SetBoolParamValue("loopOnce", false);
                    to.animationPattern.ResetAndPlay();
                }
                return;
            }

            from.playbackScheduledNextAnimation = null;
            from.playbackScheduledNextTimeLeft = float.NaN;
            from.playbackScheduledFadeOutAtRemaining = float.NaN;

            if (!to.playbackEnabled)
            {
                to.clipTime = 0f;
                if (to.loop)
                {
                    if (!from.loop)
                        to.clipTime = Mathf.Abs(to.animationLength - (from.animationLength - from.clipTime));
                    else if (to.preserveLoops)
                        to.clipTime = (to.animationLength - (from.animationLength - from.clipTime)).Modulo(to.animationLength);
                }
            }

            // if(!from.loop && to.blendInDuration > from.animationLength - from.clipTime)
            //     SuperController.LogError($"Timeline: Transition from '{from.animationName}' to '{to.animationName}' will stop the former animation after it ends, because the blend-in time of the latter is too long for the sequenced time.");
            BlendOut(from, to.blendInDuration);
            from.playbackMainInLayer = false;
            BlendIn(to, to.blendInDuration);
            to.playbackMainInLayer = true;

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

        private void AssignNextAnimation(AtomAnimationClip source)
        {
            if (source.nextAnimationName == null) return;
            if (clips.Count == 1) return;

            if (source.nextAnimationTime <= 0)
                return;

            if (source.nextAnimationName == SlaveAnimationName)
                return;

            AtomAnimationClip next;

            string group;
            if (source.nextAnimationName.StartsWith(NextAnimationSegmentPrefix))
            {
                var segmentName = source.nextAnimationName.Substring(NextAnimationSegmentPrefix.Length);
                AtomAnimationsClipsIndex.IndexedSegment segment;
                if (index.segments.TryGetValue(segmentName, out segment))
                {
                    next = segment.layers[0][0];
                }
                else
                {
                    next = null;
                    SuperController.LogError($"Timeline: Animation '{source.animationNameQualified}' could not transition to segment '{segmentName}' because it did not exist");
                }
            }
            else if (source.nextAnimationName == RandomizeAnimationName)
            {
                var candidates = index
                    .ByLayer(source.animationLayerQualified)
                    .Where(c => c.animationName != source.animationName)
                    .ToList();
                if (candidates.Count == 0) return;
                next = SelectRandomClip(candidates);
            }
            else if (TryGetRandomizedGroup(source.nextAnimationName, out group))
            {
                var candidates = index
                    .ByLayer(source.animationLayerQualified)
                    .Where(c => c.animationName != source.animationName)
                    .Where(c => c.animationNameGroup == group)
                    .ToList();
                if (candidates.Count == 0) return;
                next = SelectRandomClip(candidates);
            }
            else
            {
                next = index.ByLayer(source.animationLayerQualified).FirstOrDefault(c => c.animationName == source.nextAnimationName);
            }

            if (next == null) return;

            ScheduleNextAnimation(source, next);
        }

        public static bool TryGetRandomizedGroup(string animationName, out string groupName)
        {
            if (!animationName.EndsWith(RandomizeGroupSuffix))
            {
                groupName = null;
                return false;
            }

            groupName = animationName.Substring(0, animationName.Length - RandomizeGroupSuffix.Length);
            return true;
        }

        private void ScheduleNextAnimation(AtomAnimationClip source, AtomAnimationClip next)
        {
            var nextTime = source.nextAnimationTime;
            if (source.loop)
            {
                if (source.preserveLoops && next.preserveLoops)
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
                nextTime = Mathf.Min(nextTime, source.animationLength - next.blendInDuration);
            }

            if (nextTime < float.Epsilon)
            {
                // SuperController.LogError($"Timeline: Blending from animation {source.animationNameQualified} to {next.animationNameQualified} with blend time {next.blendInDuration} results in negative value: {nextTime}. Transition will be skipped.");
                nextTime = 0f;
            }

            ScheduleNextAnimation(source, next, nextTime);
        }

        private void ScheduleNextAnimation(AtomAnimationClip source, AtomAnimationClip next, float nextTime)
        {
            source.playbackScheduledNextAnimation = next;
            source.playbackScheduledNextTimeLeft = nextTime;
            source.playbackScheduledFadeOutAtRemaining = float.NaN;

            if (logger.sequencing) logger.Log(logger.sequencingCategory, $"Schedule transition '{source.animationNameQualified}' -> '{next.animationName}' in {nextTime:0.000}s");

            if (next.fadeOnTransition && next.animationLayerQualified == index.segments[next.animationSegment].layerNames[0] && fadeManager != null)
            {
                source.playbackScheduledFadeOutAtRemaining = (fadeManager.fadeOutTime + fadeManager.halfBlackTime) * source.speed * globalSpeed;
                if (source.playbackScheduledNextTimeLeft < source.playbackScheduledFadeOutAtRemaining)
                {
                    fadeManager.FadeOutInstant();
                    source.playbackScheduledFadeOutAtRemaining = float.NaN;
                }
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

        private void SyncTriggers()
        {
            for (var clipIndex = 0; clipIndex < clips.Count; clipIndex++)
            {
                var clip = clips[clipIndex];
                for (var triggerIndex = 0; triggerIndex < clip.targetTriggers.Count; triggerIndex++)
                {
                    var target = clip.targetTriggers[triggerIndex];
                    if (clip.playbackEnabled)
                    {
                        target.Sync(clip.clipTime);
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
                SampleController(x.Key.controller, x.Value, force);
            }
        }

        public void SampleParentedControllers(AtomAnimationClip source)
        {
            if (simulationFrozen) return;
            if (_globalScaledWeight <= 0) return;
            // TODO: Index keep track if there is any parenting
            var layers = GetMainAndBestSiblingPerLayer(playingAnimationSegment, source.animationName, source.animationSet);
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
        private void SampleController(FreeControllerV3 controller, IList<FreeControllerV3AnimationTarget> targets, bool force)
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
                var rotation = Quaternion.Slerp(control.rotation, targetRotation, (totalRotationControlWeights / totalRotationBlendWeights) * _globalScaledWeight);
                control.rotation = rotation;
            }

            if (totalPositionBlendWeights > float.Epsilon && controller.currentPositionState != FreeControllerV3.PositionState.Off)
            {
                var targetPosition = weightedPositionSum / totalPositionBlendWeights;
                var position = Vector3.Lerp(control.position, targetPosition, (totalPositionControlWeights / totalPositionBlendWeights) * _globalScaledWeight);
                control.position = position;
            }

            if (force && controller.currentPositionState == FreeControllerV3.PositionState.Comply || controller.currentRotationState == FreeControllerV3.RotationState.Comply)
                controller.PauseComply();
        }

        #endregion

        #region Animation Rebuilding

        private IEnumerator RebuildDeferred()
        {
            yield return new WaitForEndOfFrame();
            while (isPlaying)
                yield return 0;
            RebuildAnimationNow();
        }

        public void RebuildAnimationNow()
        {
            if (_animationRebuildInProgress) throw new InvalidOperationException("A rebuild is already in progress. This is usually caused by by RebuildAnimation triggering dirty (internal error).");
            _animationRebuildRequestPending = false;
            _animationRebuildInProgress = true;
            try
            {
                RebuildAnimationNowImpl();
            }
            catch (Exception exc)
            {
                SuperController.LogError($"Timeline.{nameof(AtomAnimation)}.{nameof(RebuildAnimationNow)}: " + exc);
            }
            finally
            {
                _animationRebuildInProgress = false;
            }

            onAnimationRebuilt.Invoke();
        }

        private void RebuildAnimationNowImpl()
        {
            var sw = Stopwatch.StartNew();
            foreach (var layer in index.clipsGroupedByLayer)
            {
                AtomAnimationClip last = null;
                foreach (var clip in layer)
                {
                    clip.Validate();
                    clip.Rebuild(last);
                    last = clip;
                }
            }
            foreach (var clip in clips)
            {
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
                SuperController.LogError($"Timeline.{nameof(RebuildAnimationNowImpl)}: Suspiciously long animation rebuild ({sw.Elapsed})");
            }
        }

        private void RebuildTransition(AtomAnimationClip clip)
        {
            if (clip.autoTransitionPrevious)
            {
                var previous = clips.FirstOrDefault(c => c.nextAnimationName == clip.animationName);
                if (previous != null && (previous.IsDirty() || clip.IsDirty()))
                {
                    CopySourceFrameToClip(previous, previous.animationLength, clip, 0f);
                }
            }
            if (clip.autoTransitionNext)
            {
                var next = GetClip(clip.animationSegment, clip.animationLayer, clip.nextAnimationName);
                if (next != null && (next.IsDirty() || clip.IsDirty()))
                {
                    CopySourceFrameToClip(next, 0f, clip, clip.animationLength);
                }
            }
        }

        private static void CopySourceFrameToClip(AtomAnimationClip source, float sourceTime, AtomAnimationClip clip, float clipTime)
        {
            foreach (var sourceTarget in source.targetControllers)
            {
                if (!sourceTarget.EnsureParentAvailable()) continue;
                var currentTarget = clip.targetControllers.FirstOrDefault(t => t.TargetsSameAs(sourceTarget));
                if (currentTarget == null) continue;
                if (!currentTarget.EnsureParentAvailable()) continue;
                // TODO: If there's a parent for position but not rotation or vice versa there will be problems
                // ReSharper disable Unity.NoNullCoalescing
                var sourceParent = sourceTarget.GetPositionParentRB()?.transform ?? sourceTarget.animatableRef.controller.control.parent;
                var currentParent = currentTarget.GetPositionParentRB()?.transform ?? currentTarget.animatableRef.controller.control.parent;
                // ReSharper restore Unity.NoNullCoalescing
                if (sourceParent == currentParent)
                {
                    currentTarget.SetCurveSnapshot(clipTime, sourceTarget.GetCurveSnapshot(sourceTime), false);
                    currentTarget.ChangeCurveByTime(clipTime, CurveTypeValues.Linear, false);
                }
                else
                {
                    var position = sourceParent.TransformPoint(sourceTarget.EvaluatePosition(sourceTime));
                    var rotation = Quaternion.Inverse(sourceParent.rotation) * sourceTarget.EvaluateRotation(sourceTime);
                    currentTarget.SetKeyframeByTime(clipTime, currentParent.TransformPoint(position), Quaternion.Inverse(currentParent.rotation) * rotation, CurveTypeValues.Linear, false);
                }
            }
            foreach (var sourceTarget in source.targetFloatParams)
            {
                var currentTarget = clip.targetFloatParams.FirstOrDefault(t => t.TargetsSameAs(sourceTarget));
                if (currentTarget == null) continue;
                currentTarget.value.SetKeySnapshot(clipTime, sourceTarget.value.GetKeyframeAt(sourceTime));
                currentTarget.ChangeCurveByTime(clipTime, CurveTypeValues.Linear, false);
            }
        }

        #endregion

        #region Event Handlers

        private void OnAnimationSettingsChanged(string param)
        {
            index.Rebuild();
            onAnimationSettingsChanged.Invoke();
            if (param == nameof(AtomAnimationClip.animationName) || param == nameof(AtomAnimationClip.animationLayer) || param == nameof(AtomAnimationClip.animationSegment))
                onClipsListChanged.Invoke();
        }

        private void OnAnimationKeyframesDirty()
        {
            if (_animationRebuildInProgress) throw new InvalidOperationException("A rebuild is already in progress. This is usually caused by by RebuildAnimation triggering dirty (internal error).");
            if (_animationRebuildRequestPending) return;
            _animationRebuildRequestPending = true;
            StartCoroutine(RebuildDeferred());
        }

        private void OnTargetsListChanged()
        {
            index.Rebuild();
            OnAnimationKeyframesDirty();
        }

        #endregion

        #region Unity Lifecycle

        public void Update()
        {
            SyncTriggers();

            if (!allowAnimationProcessing || paused) return;

            SampleFloatParams();
            ProcessAnimationSequence(GetDeltaTime() * globalSpeed);

            if (fadeManager?.black == true && playTime > _scheduleFadeIn && !simulationFrozen)
            {
                _scheduleFadeIn = float.MaxValue;
                fadeManager.FadeIn();
            }
        }

        [MethodImpl(256)]
        private float GetDeltaTime()
        {
            switch (timeMode)
            {
                case TimeModes.UnityTime:
                    return Time.deltaTime;
                case TimeModes.RealTime:
                    return Time.unscaledDeltaTime * Time.timeScale;
                default:
                    return Time.deltaTime;
            }
        }

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

                if (!clip.loop && clip.playbackEnabled && clip.clipTime >= clip.animationLength && float.IsNaN(clip.playbackScheduledNextTimeLeft) && !clip.infinite)
                {
                    if (logger.general) logger.Log(logger.generalCategory, $"Leave '{clip.animationNameQualified}' (non-looping complete)");
                    clip.Leave();
                    clip.Reset(true);
                    onClipIsPlayingChanged.Invoke(clip);
                    continue;
                }

                if (!clip.playbackMainInLayer || clip.playbackScheduledNextAnimation == null)
                    continue;

                var adjustedDeltaTime = deltaTime * clip.speed;
                clip.playbackScheduledNextTimeLeft -= adjustedDeltaTime;

                if (clip.playbackScheduledNextTimeLeft <= clip.playbackScheduledFadeOutAtRemaining)
                {
                    _scheduleFadeIn = float.MaxValue;
                    clip.playbackScheduledFadeOutAtRemaining = float.NaN;
                    if (fadeManager?.black == false)
                        fadeManager.FadeOut();
                }

                if (clip.playbackScheduledNextTimeLeft > 0)
                    continue;

                var nextClip = clip.playbackScheduledNextAnimation;
                clip.playbackScheduledNextAnimation = null;
                clip.playbackScheduledNextTimeLeft = float.NaN;

                if (nextClip.animationSegment != AtomAnimationClip.SharedAnimationSegment && playingAnimationSegment != nextClip.animationSegment)
                {
                    PlaySegment(nextClip);
                }
                else
                {
                    TransitionClips(clip, nextClip);
                    PlaySiblings(nextClip);
                }

                if (nextClip.fadeOnTransition && fadeManager?.black == true && nextClip.animationLayerQualified == index.segments[nextClip.animationSegment].layerNames[0])
                {
                    _scheduleFadeIn = playTime + fadeManager.halfBlackTime;
                }
            }

            if (clipsPlaying == 0 && clipsQueued == 0)
            {
                StopAll();
            }
        }

        public void FixedUpdate()
        {
            if (!allowAnimationProcessing || paused) return;

            var delta = GetDeltaTime() * _globalSpeed;
            playTime += delta;

            if (autoStop > 0f && playTime >= autoStop)
            {
                StopAll();
                return;
            }

            AdvanceClipsTime(delta);
            SampleControllers();
        }

        public void OnDestroy()
        {
            onAnimationSettingsChanged.RemoveAllListeners();
            onIsPlayingChanged.RemoveAllListeners();
            onClipIsPlayingChanged.RemoveAllListeners();
            onSpeedChanged.RemoveAllListeners();
            onWeightChanged.RemoveAllListeners();
            onClipsListChanged.RemoveAllListeners();
            onAnimationRebuilt.RemoveAllListeners();
            foreach (var clip in clips)
            {
                clip.Dispose();
            }
        }

        #endregion

        private int _restoreTimeMode;
        public void SetTemporaryTimeMode(int temporaryTimeMode)
        {
            _restoreTimeMode = timeMode;
            timeMode = temporaryTimeMode;
        }

        public void RestoreTemporaryTimeMode()
        {
            timeMode = _restoreTimeMode;
            _restoreTimeMode = 0;
        }

        public void CleanupAnimatables()
        {
            for (var i = animatables.storableFloats.Count - 1; i >= 0; i--)
            {
                var a = animatables.storableFloats[i];
                if(!clips.Any(c => c.targetFloatParams.Any(t => t.animatableRef == a)))
                    animatables.RemoveStorableFloat(a);
            }

            for (var i = animatables.controllers.Count - 1; i >= 0; i--)
            {
                var a = animatables.controllers[i];
                if(!clips.Any(c => c.targetControllers.Any(t => t.animatableRef == a)))
                    animatables.RemoveController(a);
            }

            for (var i = animatables.triggers.Count - 1; i >= 0; i--)
            {
                var a = animatables.triggers[i];
                if(!clips.Any(c => c.targetTriggers.Any(t => t.animatableRef == a)))
                    animatables.RemoveTriggerTrack(a);
            }
        }
    }
}
