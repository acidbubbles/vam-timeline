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

        private static readonly Regex _lastDigitsRegex = new Regex(@"^(?<name>.+)(?<index>[0-9]+)$", RegexOptions.Compiled);

        public const string RandomizeAnimationName = "(Randomize)";
        public const string SlaveAnimationName = "(Slave)";
        public const string RandomizeGroupSuffix = "/*";

        public readonly UnityEvent onAnimationSettingsChanged = new UnityEvent();
        public readonly UnityEvent onSpeedChanged = new UnityEvent();
        public readonly UnityEvent onClipsListChanged = new UnityEvent();
        public readonly UnityEvent onAnimationRebuilt = new UnityEvent();
        public readonly UnityEvent onPausedChanged = new UnityEvent();
        public readonly IsPlayingEvent onIsPlayingChanged = new IsPlayingEvent();
        public readonly IsPlayingEvent onClipIsPlayingChanged = new IsPlayingEvent();

        public Logger logger;

        public IFadeManager fadeManager;
        private float _scheduleFadeIn = float.MaxValue;

        public List<AtomAnimationClip> clips { get; } = new List<AtomAnimationClip>();
        public bool isPlaying { get; private set; }
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

        public int timeMode { get; set; }

        public bool master { get; set; }

        public bool simulationFrozen;

        public float playTime { get; private set; }

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
                for (var i = 0; i < clips.Count; i++)
                {
                    var clip = clips[i];
                    if (clip.animationPattern != null)
                        clip.animationPattern.SetFloatParamValue("speed", value);
                }

                onSpeedChanged.Invoke();
            }
        }

        public bool sequencing { get; private set; }

        private bool _animationRebuildRequestPending;
        private bool _animationRebuildInProgress;

        public AtomAnimationsClipsIndex index { get; }
        public AnimatablesRegistry animatables { get; }

        public bool syncSubsceneOnly { get; set; }
        public bool syncWithPeers { get; set; } = true;

        public AtomAnimation()
        {
            index = new AtomAnimationsClipsIndex(clips);
            animatables = new AnimatablesRegistry();
        }

        public AtomAnimationClip GetDefaultClip()
        {
            return index.ByLayer(clips[0].animationLayer).FirstOrDefault(c => c.autoPlay) ?? clips[0];
        }

        public bool IsEmpty()
        {
            if (clips.Count == 0) return true;
            return clips.Count == 1 && clips[0].IsEmpty();
        }

        #region Clips

        public AtomAnimationClip GetClip(string animationLayer, string animationName)
        {
            return clips.FirstOrDefault(c => c.animationLayer == animationLayer && c.animationName == animationName);
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
            var lastIndexOfLayer = clips.FindLastIndex(c => c.animationLayer == clip.animationLayer);
            AddClipAt(clip, lastIndexOfLayer == -1 ? clips.Count : lastIndexOfLayer + 1);
            clip.onAnimationSettingsChanged.AddListener(OnAnimationSettingsChanged);
            clip.onAnimationKeyframesDirty.AddListener(OnAnimationKeyframesDirty);
            clip.onTargetsListChanged.AddListener(OnTargetsListChanged);
            index.Rebuild();
            onClipsListChanged.Invoke();
            if (clip.IsDirty()) clip.onAnimationKeyframesDirty.Invoke();
            return clip;
        }

        private AtomAnimationClip AddClipAt(AtomAnimationClip clip, int i)
        {
            if (i == -1 || i > clips.Count) throw new ArgumentOutOfRangeException($"Tried to add clip {clip.animationNameQualified} at position {i} but there are {clips.Count} clips");
            clips.Insert(i, clip);
            clip.onAnimationSettingsChanged.AddListener(OnAnimationSettingsChanged);
            clip.onAnimationKeyframesDirty.AddListener(OnAnimationKeyframesDirty);
            clip.onTargetsListChanged.AddListener(OnTargetsListChanged);
            index.Rebuild();
            onClipsListChanged.Invoke();
            if (clip.IsDirty()) clip.onAnimationKeyframesDirty.Invoke();
            return clip;
        }

        public AtomAnimationClip CreateClip([NotNull] string animationLayer, [NotNull] string animationName, int position = -1)
        {
            if (animationLayer == null) throw new ArgumentNullException(nameof(animationLayer));
            if (animationName == null) throw new ArgumentNullException(nameof(animationName));

            if (clips.Any(c => c.animationLayer == animationLayer && c.animationName == animationName))
                throw new InvalidOperationException($"Animation '{animationLayer}::{animationName}' already exists");
            var clip = new AtomAnimationClip(animationName, animationLayer);
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

        public string GetNewAnimationName(AtomAnimationClip source)
        {
            var match = _lastDigitsRegex.Match(source.animationName);
            string animationNameBeforeInt;
            int animationNameInt;
            if (!match.Success)
            {
                animationNameBeforeInt = $"{source.animationName.TrimEnd()} ";
                animationNameInt = 1;
            }
            else
            {
                animationNameBeforeInt = match.Groups["name"].Value;
                animationNameInt = int.Parse(match.Groups["index"].Value);
            }
            for (var i = animationNameInt + 1; i < 999; i++)
            {
                var animationName = animationNameBeforeInt + i;
                if (index.ByLayer(source.animationLayer).All(c => c.animationName != animationName))
                    return animationName;
            }
            return Guid.NewGuid().ToString();
        }

        public IEnumerable<string> EnumerateLayers()
        {
            switch (clips.Count)
            {
                case 0:
                    yield break;
                case 1:
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

        public void Clear()
        {
            var list = clips.ToList();
            for (var i = 0; i < list.Count; i++)
            {
                var clip = list[i];
                RemoveClip(clip);
            }

            speed = 1f;
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

        public void PlayClipBySet(string animationName, string animationSet, bool seq)
        {
            var siblings = GetMainAndBestSiblingPerLayer(animationName, animationSet);
            for (var i = 0; i < siblings.Count; i++)
            {
                var clip = siblings[i];
                if (clip.target == null) continue;
                if(isPlaying && clip.main != null)
                    PlayClip(clip.main, clip.target, seq, true);
                else
                    PlayClip(null, clip.target, seq, true);
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
            if (clip.playbackMainInLayer) return;

            PlayClip(
                isPlaying
                    ? GetMainClipPerLayer(index.ByLayer(clip.animationLayer))
                    : null,
                clip,
                seq,
                allowPreserveLoops
            );
        }

        #endregion

        #region Playback (Core)

        public void PlayClip(AtomAnimationClip previous, AtomAnimationClip next, bool seq, bool allowPreserveLoops)
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
            }

            if (sequencing && !next.playbackEnabled)
                next.clipTime = 0;

            if (previous != null && previous.uninterruptible)
                return;

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

                // Blend immediately, but unlike TransitionClips, recording will ignore blending
                var blendInDuration = next.recording ? 0f : next.blendInDuration;
                BlendOut(previous, blendInDuration);
                previous.playbackMainInLayer = false;
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
            PlaySiblings(clip.animationName, clip.animationSet);
        }

        private void PlaySiblings(string animationName, string animationSet)
        {
            var clipsByName = index.ByName(animationName);

            PlaySiblingsByName(clipsByName);

            if (animationSet == null) return;

            var layers = index.ByLayer();
            for (var i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];
                if (LayerContainsClip(clipsByName, layer[0].animationLayer)) continue;
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
                    GetMainClipInLayer(index.ByLayer(clip.animationLayer)),
                    clip);
            }
        }

        public void StopClip(AtomAnimationClip clip)
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
                    isPlaying = false;
                    sequencing = false;
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

        private IList<TransitionTarget> GetMainAndBestSiblingPerLayer(string animationName, string animationSet)
        {
            var layers = index.ByLayer();
            var result = new TransitionTarget[layers.Count];
            for (var i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];
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
                result[i] = new TransitionTarget
                {
                    main = main,
                    target = bestSibling
                };
            }
            return result;
        }

        private static bool LayerContainsClip(IList<AtomAnimationClip> clipsByName, string animationLayer)
        {
            for (var j = 0; j < clipsByName.Count; j++)
            {
                if (clipsByName[j].animationLayer == animationLayer)
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

        public static AtomAnimationClip GetPrincipalClipInLayer(IList<AtomAnimationClip> layer, string animationName, string animationSet)
        {
            #warning Optimize and move to a Layer object
            var clip = (animationSet != null ? layer.FirstOrDefault(c => c.animationSet == animationSet) : null) ??
                       layer.FirstOrDefault(c => c.playbackMainInLayer) ??
                       layer.FirstOrDefault(c => c.animationName == animationName) ??
                       layer.FirstOrDefault(c => c.autoPlay) ??
                       layer[0];

            // This is to prevent playing on the main layer, starting a set on another layer, which will then override the clip you just played on the main layer
            if (clip.animationSet != null && clip.animationSet != animationSet)
                clip = null;

            return clip;
        }

        public void PlayOneAndOtherMainsInLayers(AtomAnimationClip selected, bool sequencing = true)
        {
            foreach (var clip in GetMainClipPerLayer())
            {
                PlayClip(
                    clip.animationLayer == selected.animationLayer ? selected : clip,
                    sequencing);
            }
        }

        private IEnumerable<AtomAnimationClip> GetMainClipPerLayer()
        {
            return clips
                .GroupBy(c => c.animationLayer)
                .Select(g =>
                {
                    return g.FirstOrDefault(c => c.playbackMainInLayer) ?? g.FirstOrDefault(c => c.autoPlay) ?? g.First();
                });
        }

        private AtomAnimationClip GetMainClipPerLayer(IList<AtomAnimationClip> layer)
        {
            for (var i = 0; i < layer.Count; i++)
            {
                var clip = layer[i];
                if (clip.playbackMainInLayer) return clip;
            }
            return null;
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

            var layers = index.ByLayer();
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
                        var smoothBlendWeight = Mathf.SmoothStep(0f, 1, clip.playbackBlendWeight);
                        weightedClipSpeedSum += clip.speed * smoothBlendWeight;
                        totalBlendWeights += smoothBlendWeight;
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
                    clip.clipTime += clipDelta;
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
                            if (logger.general) logger.Log(logger.generalCategory, $"Leave '{clip.animationNameQualified}' (blend out complete)");
                            clip.Leave();
                            clip.Reset(true);
                            onClipIsPlayingChanged.Invoke(clip);
                        }
                    }
                }
            }
        }

        private void BlendIn(AtomAnimationClip clip, float blendDuration)
        {
            if (clip.applyPoseOnTransition)
            {
                clip.pose?.Apply();
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
                clip.playbackBlendRate = 1f / blendDuration;
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
                clip.playbackBlendRate = -1f / blendDuration;
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

            from.playbackScheduledNextAnimationName = null;
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
                        to.clipTime = from.clipTime;
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
            if (source.nextAnimationName == RandomizeAnimationName)
            {
                var candidates = index
                    .ByLayer(source.animationLayer)
                    .Where(c => c.animationName != source.animationName)
                    .ToList();
                if (candidates.Count == 0) return;
                next = SelectRandomClip(candidates);
            }
            else if (TryGetRandomizedGroup(source.nextAnimationName, out group))
            {
                var candidates = index
                    .ByLayer(source.animationLayer)
                    .Where(c => c.animationName != source.animationName)
                    .Where(c => c.animationNameGroup == group)
                    .ToList();
                if (candidates.Count == 0) return;
                next = SelectRandomClip(candidates);
            }
            else
            {
                next = index.ByLayer(source.animationLayer).FirstOrDefault(c => c.animationName == source.nextAnimationName);
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
            source.playbackScheduledNextAnimationName = next.animationName;
            source.playbackScheduledNextTimeLeft = nextTime;
            source.playbackScheduledFadeOutAtRemaining = float.NaN;

            if (logger.sequencing) logger.Log(logger.sequencingCategory, $"Schedule transition '{source.animationNameQualified}' -> '{next.animationName}' in {nextTime:0.000}s)");

            if (next.fadeOnTransition && next.animationLayer == index.mainLayer && fadeManager != null)
            {
                source.playbackScheduledFadeOutAtRemaining = (fadeManager.fadeOutTime + fadeManager.halfBlackTime) * source.speed * speed;
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

        private void SampleTriggers()
        {
            for (var clipIndex = 0; clipIndex < clips.Count; clipIndex++)
            {
                var clip = clips[clipIndex];
                if (!clip.playbackEnabled) continue;
                for (var triggerIndex = 0; triggerIndex < clip.targetTriggers.Count; triggerIndex++)
                {
                    var target = clip.targetTriggers[triggerIndex];
                    target.Sample(clip.clipTime);
                }
            }
        }

        [MethodImpl(256)]
        private void SampleFloatParams()
        {
            if (simulationFrozen) return;
            foreach (var x in index.ByFloatParam())
            {
                if (!x.Value[0].animatableRef.EnsureAvailable()) continue;
                SampleFloatParam(x.Value[0].animatableRef, x.Value);
            }
        }

        [MethodImpl(256)]
        private static void SampleFloatParam(JSONStorableFloatRef floatParamRef, List<JSONStorableFloatAnimationTarget> targets)
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
                var weight = clip.temporarilyEnabled ? 1f : clip.scaledWeight;
                if (weight < float.Epsilon) continue;

                var value = target.value.Evaluate(clip.clipTime);
                var smoothBlendWeight = Mathf.SmoothStep(0f, 1f, clip.temporarilyEnabled ? 1f : clip.playbackBlendWeight);
                weightedSum += value * smoothBlendWeight;
                totalBlendWeights += smoothBlendWeight;
            }

            if (totalBlendWeights > minimumDelta)
            {
                var val = weightedSum / totalBlendWeights;
                if(Mathf.Abs(val - floatParamRef.val) > minimumDelta)
                {
                    floatParamRef.val = val;
                }
            }
        }

        [MethodImpl(256)]
        private void SampleControllers(bool force = false)
        {
            if (simulationFrozen) return;
            foreach (var x in index.ByController())
            {
                SampleController(x.Key.controller, x.Value, force);
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
                var weight = clip.temporarilyEnabled ? 1f : clip.scaledWeight * target.scaledWeight;
                if (weight < float.Epsilon) continue;

                if (!target.EnsureParentAvailable()) return;

                var smoothBlendWeight = Mathf.SmoothStep(0f, 1f, clip.temporarilyEnabled ? 1f : clip.playbackBlendWeight);

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

                    _rotationBlendWeights[rotationCount] = smoothBlendWeight;
                    totalRotationBlendWeights += smoothBlendWeight;
                    totalRotationControlWeights += weight * smoothBlendWeight;
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

                    weightedPositionSum += targetPosition * smoothBlendWeight;
                    totalPositionBlendWeights += smoothBlendWeight;
                    totalPositionControlWeights += weight * smoothBlendWeight;
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
                var rotation = Quaternion.Slerp(control.rotation, targetRotation, totalRotationControlWeights / totalRotationBlendWeights);
                control.rotation = rotation;
            }

            if (totalPositionBlendWeights > float.Epsilon && controller.currentPositionState != FreeControllerV3.PositionState.Off)
            {
                var targetPosition = weightedPositionSum / totalPositionBlendWeights;
                var position = Vector3.Lerp(control.position, targetPosition, totalPositionControlWeights / totalPositionBlendWeights);
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
            foreach (var layer in index.ByLayer())
            {
                AtomAnimationClip last = null;
                foreach (var clip in layer)
                {
                    clip.Validate();
                    RebuildClip(clip, last);
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
                var next = GetClip(clip.animationLayer, clip.nextAnimationName);
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
                    currentTarget.SetKeyframe(clipTime, currentParent.TransformPoint(position), Quaternion.Inverse(currentParent.rotation) * rotation, CurveTypeValues.Linear, false);
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

        private static void RebuildClip(AtomAnimationClip clip, AtomAnimationClip previous)
        {
            foreach (var target in clip.targetControllers)
            {
                if (!target.dirty) continue;

                if (clip.loop)
                    target.SetCurveSnapshot(clip.animationLength, target.GetCurveSnapshot(0f), false);

                target.ComputeCurves();

                if (clip.ensureQuaternionContinuity)
                {
                    var lastMatching = previous?.targetControllers.FirstOrDefault(t => t.TargetsSameAs(target));
                    var q = lastMatching?.GetRotationAtKeyframe(lastMatching.rotX.length - 1) ?? target.GetRotationAtKeyframe(target.rotX.length - 1);
                    UnitySpecific.EnsureQuaternionContinuityAndRecalculateSlope(
                        target.rotX,
                        target.rotY,
                        target.rotZ,
                        target.rotW,
                        q);
                }

                foreach (var curve in target.GetCurves())
                    curve.ComputeCurves();
            }

            foreach (var target in clip.targetFloatParams)
            {
                if (!target.dirty) continue;

                if (clip.loop)
                    target.SetCurveSnapshot(clip.animationLength, target.GetCurveSnapshot(0), false);

                target.value.ComputeCurves();
            }

            foreach (var target in clip.targetTriggers)
            {
                if (!target.dirty) continue;

                target.RebuildKeyframes(clip.animationLength);
            }
        }

        #endregion

        #region Event Handlers

        private void OnAnimationSettingsChanged(string param)
        {
            index.Rebuild();
            onAnimationSettingsChanged.Invoke();
            if (param == nameof(AtomAnimationClip.animationName) || param == nameof(AtomAnimationClip.animationLayer))
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
            if (!allowAnimationProcessing || paused) return;

            SampleFloatParams();
            SampleTriggers();
            ProcessAnimationSequence(GetDeltaTime() * speed);

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

                if (clip.playbackScheduledNextAnimationName != null)
                    clipsQueued++;

                if (!clip.loop && clip.playbackEnabled && clip.clipTime >= clip.animationLength && !clip.infinite)
                {
                    if (logger.general) logger.Log(logger.generalCategory, $"Leave '{clip.animationNameQualified}' (non-looping complete)");
                    clip.Leave();
                    clip.Reset(true);
                    onClipIsPlayingChanged.Invoke(clip);
                    continue;
                }

                if (!clip.playbackMainInLayer || clip.playbackScheduledNextAnimationName == null)
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

                var nextAnimationName = clip.playbackScheduledNextAnimationName;
                clip.playbackScheduledNextAnimationName = null;
                clip.playbackScheduledNextTimeLeft = float.NaN;
                var nextClip = index.ByLayer(clip.animationLayer).FirstOrDefault(c => c.animationName == nextAnimationName);
                if (nextClip == null)
                {
                    SuperController.LogError($"Timeline: Cannot sequence from animation '{clip.animationName}' to '{nextAnimationName}' because the target animation does not exist.");
                    continue;
                }

                TransitionClips(clip, nextClip);
                PlaySiblings(nextClip);

                if (nextClip.fadeOnTransition && fadeManager?.black == true && clip.animationLayer == index.mainLayer)
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

            var delta = GetDeltaTime() * _speed;
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
