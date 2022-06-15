using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Random = UnityEngine.Random;

namespace VamTimeline
{
    public partial class AtomAnimation
    {
        #region Get Clip

        public AtomAnimationClip GetDefaultClip()
        {
            return index.ByLayerQualified(clips[0].animationLayerQualifiedId).FirstOrDefault(c => c.autoPlay) ?? clips[0];
        }

        public bool IsEmpty()
        {
            if (clips.Count == 0) return true;
            return clips.Count == 1 && clips[0].IsEmpty();
        }

        public AtomAnimationClip GetClip(string animationSegment, string animationLayer, string animationName)
        {
            return index.ByName(animationSegment, animationName).FirstOrDefault(c => c.animationLayer == animationLayer);
        }

        #endregion

        #region Add/Remove Clips

        public AtomAnimationClip AddClip(AtomAnimationClip clip)
        {
            if (_playingAnimationSegment == null) playingAnimationSegment = clip.animationSegment;
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
            if(playingAnimationSegment == null)
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
            return GetUniqueAnimationName(source.animationSegmentId, source.animationName);
        }

        public string GetUniqueAnimationNameInLayer(AtomAnimationClip source)
        {
            return GetUniqueName(source.animationName, clips.Where(c => c.isOnSharedSegment != source.isOnSharedSegment || c.animationLayerQualified == source.animationLayerQualified).Select(c => c.animationName).ToList());
        }

        public string GetUniqueAnimationName(int segmentId, string sourceName)
        {
            return GetUniqueName(sourceName, clips.Where(c => c.animationSegmentId == segmentId || c.isOnSharedSegment).Select(c => c.animationName).ToList());
        }

        public string GetUniqueLayerName(AtomAnimationClip source, string baseName = null)
        {
            return GetUniqueName(baseName ?? source.animationLayer, index.segmentsById[source.animationSegmentId].layerNames);
        }

        public string GetUniqueSegmentName(AtomAnimationClip source)
        {
            return GetUniqueSegmentName(!source.isOnNoneSegment && !source.isOnSharedSegment ? source.animationSegment : "Segment 1");
        }

        public string GetUniqueSegmentName(string sourceSegmentName)
        {
            return GetUniqueName(sourceSegmentName, index.segmentNames);
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

        private TransitionTarget[] _mainAndBestSiblingPerLayerCache = new TransitionTarget[0];
        private IList<TransitionTarget> GetMainAndBestSiblingPerLayer(int animationSegmentId, int animationNameId, int animationSetId)
        {
            AtomAnimationsClipsIndex.IndexedSegment sharedLayers;
            if (!index.segmentsById.TryGetValue(AtomAnimationClip.SharedAnimationSegmentId, out sharedLayers))
            {
                sharedLayers = index.emptySegment;
            }

            AtomAnimationsClipsIndex.IndexedSegment segmentLayers;
            if (animationSegmentId == AtomAnimationClip.SharedAnimationSegmentId || !index.segmentsById.TryGetValue(animationSegmentId, out segmentLayers))
            {
                segmentLayers = index.emptySegment;
            }

            var length = sharedLayers.layers.Count + segmentLayers.layers.Count;
            if (_mainAndBestSiblingPerLayerCache.Length != length) _mainAndBestSiblingPerLayerCache = new TransitionTarget[length];
            if (length == 0) return _mainAndBestSiblingPerLayerCache;

            for (var i = 0; i < sharedLayers.layers.Count; i++)
            {
                var layer = sharedLayers.layers[i];
                _mainAndBestSiblingPerLayerCache[i] = GetMainAndBestSiblingInLayer(layer, animationNameId, animationSetId, -1);
            }

            for (var i = 0; i < segmentLayers.layers.Count; i++)
            {
                var layer = segmentLayers.layers[i];
                _mainAndBestSiblingPerLayerCache[sharedLayers.layers.Count + i] = GetMainAndBestSiblingInLayer(layer, animationNameId, animationSetId, animationSegmentId);
            }

            return _mainAndBestSiblingPerLayerCache;
        }

        private static TransitionTarget GetMainAndBestSiblingInLayer(IList<AtomAnimationClip> layer, int animationNameId, int animationSetId, int animationSegmentId)
        {
            var main = GetMainClipInLayer(layer);
            AtomAnimationClip bestSibling = null;
            for (var j = 0; j < layer.Count; j++)
            {
                var clip = layer[j];
                if (clip.animationNameId == animationNameId)
                {
                    bestSibling = clip;
                    break;
                }

                if ((animationSetId != -1 && clip.animationSetId == animationSetId) || (!clip.isOnNoneSegment && !clip.isOnSharedSegment && clip.animationSegmentId == animationSegmentId))
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

        public IList<AtomAnimationClip> GetDefaultClipsPerLayer(AtomAnimationClip source, bool includeShared = true)
        {
            if (!sequencing && focusOnLayer) return new[] { source };

            AtomAnimationsClipsIndex.IndexedSegment sharedLayers;
            if (!includeShared)
                sharedLayers = index.emptySegment;
            else if (!index.segmentsById.TryGetValue(AtomAnimationClip.SharedAnimationSegmentId, out sharedLayers))
                sharedLayers = index.emptySegment;

            AtomAnimationsClipsIndex.IndexedSegment segmentLayers;
            if (!source.isOnSharedSegment && index.segmentsById.TryGetValue(source.animationSegmentId, out segmentLayers))
                segmentLayers = index.segmentsById[source.animationSegmentId];
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
                var clip = layer.FirstOrDefault(c => c.animationSetId == source.animationSetId);
                // This is to prevent playing on the main layer, starting a set on another layer, which will then override the clip you just played on the main layer
                if (clip?.animationSet != null && clip.animationSetId != source.animationSetId)
                    clip = null;
                if (clip != null)
                    return clip;
            }

            return layer.FirstOrDefault(c => c.playbackMainInLayer) ??
                   layer.FirstOrDefault(c => c.animationNameId == source.animationNameId) ??
                   layer.FirstOrDefault(c => c.autoPlay) ??
                   layer[0];
        }

        private static AtomAnimationClip SelectRandomClip(IList<AtomAnimationClip> candidates)
        {
            if (candidates.Count == 0) return null;
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
