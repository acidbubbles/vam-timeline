using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
using Random = UnityEngine.Random;

namespace VamTimeline
{
    public partial class AtomAnimation
    {
        #region Get Clip

        public AtomAnimationClip GetDefaultClip()
        {
            return index.ByLayer(clips[0].animationLayerQualified).FirstOrDefault(c => c.autoPlay) ?? clips[0];
        }

        public bool IsEmpty()
        {
            if (clips.Count == 0) return true;
            return clips.Count == 1 && clips[0].IsEmpty();
        }

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

        #endregion

        #region Add/Remove Clips

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
            if (i == -1 || i > clips.Count)
                throw new ArgumentOutOfRangeException($"Tried to add clip {clip.animationNameQualified} at position {i} but there are {clips.Count} clips");
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

        public AtomAnimationClip CreateClip([NotNull] string animationName, [NotNull] string animationLayer, string animationSegment, int position = -1)
        {
            if (animationLayer == null) throw new ArgumentNullException(nameof(animationLayer));
            if (animationName == null) throw new ArgumentNullException(nameof(animationName));

            if (clips.Any(c => c.animationSegment == animationSegment && c.animationLayer == animationLayer && c.animationName == animationName))
                throw new InvalidOperationException($"Animation '{animationSegment}::{animationLayer}::{animationName}' already exists");
            var clip = new AtomAnimationClip(animationName, animationLayer, animationSegment, logger);
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

        #region Naming

        public string GetUniqueAnimationName(AtomAnimationClip source)
        {
            return GetUniqueAnimationName(source.animationName);
        }

        public string GetUniqueAnimationNameInLayer(AtomAnimationClip source)
        {
            return GetUniqueName(source.animationName, clips.Where(c => c.animationSegment != source.animationSegment || c.animationLayerQualified == source.animationLayerQualified).Select(c => c.animationName).ToList());
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
            if (animationSegment == AtomAnimationClip.SharedAnimationSegment || !index.segments.TryGetValue(animationSegment, out segmentLayers))
            {
                segmentLayers = index.emptySegment;
            }

            #warning Called often; index and reuse
            var result = new TransitionTarget[sharedLayers.layers.Count + segmentLayers.layers.Count];
            for (var i = 0; i < sharedLayers.layers.Count; i++)
            {
                var layer = sharedLayers.layers[i];
                result[i] = GetMainAndBestSiblingInLayer(layer, animationName, animationSet, null);
            }
            for (var i = 0; i < segmentLayers.layers.Count; i++)
            {
                var layer = segmentLayers.layers[i];
                result[sharedLayers.layers.Count + i] = GetMainAndBestSiblingInLayer(layer, animationName, animationSet, animationSegment);
            }
            return result;
        }

        private static TransitionTarget GetMainAndBestSiblingInLayer(IList<AtomAnimationClip> layer, string animationName, string animationSet, string animationSegment)
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

                #warning Called often; expensive string comparison (instead make an animation segment ID)
                if ((animationSet != null && clip.animationSet == animationSet) || (!clip.isOnNoneSegment && !clip.isOnSharedSegment && clip.animationSegment == animationSegment))
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
            if (!sequencing && focusOnLayer) return new[] { source };

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
    }
}
