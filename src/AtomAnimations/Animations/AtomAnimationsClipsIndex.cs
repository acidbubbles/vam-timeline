using System.Collections.Generic;
using System.Linq;

namespace VamTimeline
{
    public class AtomAnimationsClipsIndex
    {
        public class IndexedSegment
        {
            public readonly Dictionary<int, List<AtomAnimationClip>> layersMapById = new Dictionary<int, List<AtomAnimationClip>>();
            public readonly Dictionary<int, List<AtomAnimationClip>> clipMapByNameId = new Dictionary<int, List<AtomAnimationClip>>();
            public readonly List<List<AtomAnimationClip>> layers = new List<List<AtomAnimationClip>>();
            public readonly List<string> layerNames = new List<string>();
            public readonly List<int> layerIds = new List<int>();

            public void Add(AtomAnimationClip clip)
            {
                List<AtomAnimationClip> layer;
                if (!layersMapById.TryGetValue(clip.animationLayerId, out layer))
                {
                    layer = new List<AtomAnimationClip>();
                    layersMapById.Add(clip.animationLayerId, layer);
                    layers.Add(layer);
                    layerNames.Add(clip.animationLayer);
                    layerIds.Add(clip.animationLayerId);
                }
                layer.Add(clip);

                List<AtomAnimationClip> clips;
                if (!clipMapByNameId.TryGetValue(clip.animationNameId, out clips))
                {
                    clips = new List<AtomAnimationClip>();
                    clipMapByNameId.Add(clip.animationNameId, clips);
                }
                clips.Add(clip);
            }
        }

        public IEnumerable<string> clipNames => _clipsByName.Select(kvp => kvp.Value[0].animationName);

        private readonly List<AtomAnimationClip> _clips;

        public readonly IndexedSegment emptySegment = new IndexedSegment();
        public readonly Dictionary<int, IndexedSegment> segmentsById = new Dictionary<int, IndexedSegment>();
        public bool useSegment;
        public readonly List<int> segmentIds = new List<int>();
        public readonly List<string> segmentNames = new List<string>();
        public readonly IList<List<AtomAnimationClip>> clipsGroupedByLayer = new List<List<AtomAnimationClip>>();
        private readonly Dictionary<int, List<AtomAnimationClip>> _clipsByLayerNameQualifiedId = new Dictionary<int, List<AtomAnimationClip>>();
        private readonly Dictionary<int, List<AtomAnimationClip>> _clipsByName = new Dictionary<int, List<AtomAnimationClip>>();
        private readonly Dictionary<int, List<AtomAnimationClip>> _firstClipOfLayerBySetQualifiedId = new Dictionary<int, List<AtomAnimationClip>>();
        private readonly Dictionary<FreeControllerV3Ref, List<FreeControllerV3AnimationTarget>> _clipsByController = new Dictionary<FreeControllerV3Ref, List<FreeControllerV3AnimationTarget>>();
        private readonly Dictionary<JSONStorableFloatRef, List<JSONStorableFloatAnimationTarget>> _clipsByFloatParam = new Dictionary<JSONStorableFloatRef, List<JSONStorableFloatAnimationTarget>>();
        private readonly List<AtomAnimationClip> _emptyClipList = new List<AtomAnimationClip>();
        private bool _pendingBulkUpdate;

        public AtomAnimationsClipsIndex(List<AtomAnimationClip> clips)
        {
            _clips = clips;
        }

        public void StartBulkUpdates()
        {
            _pendingBulkUpdate = true;
        }

        public void EndBulkUpdates()
        {
            _pendingBulkUpdate = false;
            Rebuild();
        }

        public void Rebuild()
        {
            useSegment = false;
            segmentsById.Clear();
            clipsGroupedByLayer.Clear();
            _clipsByLayerNameQualifiedId.Clear();
            _clipsByName.Clear();
            _firstClipOfLayerBySetQualifiedId.Clear();
            _clipsByController.Clear();
            _clipsByFloatParam.Clear();
            segmentNames.Clear();
            segmentIds.Clear();

            if (_pendingBulkUpdate) return;

            if (_clips == null || _clips.Count == 0) return;

            foreach (var clip in _clips)
            {
                {
                    IndexedSegment sequence;
                    if (!segmentsById.TryGetValue(clip.animationSegmentId, out sequence))
                    {
                        sequence = new IndexedSegment();
                        segmentsById.Add(clip.animationSegmentId, sequence);
                        segmentNames.Add(clip.animationSegment);
                        segmentIds.Add(clip.animationSegmentId);
                    }
                    sequence.Add(clip);
                }

                {
                    List<AtomAnimationClip> nameClips;
                    if (!_clipsByName.TryGetValue(clip.animationNameId, out nameClips))
                    {
                        nameClips = new List<AtomAnimationClip>();
                        _clipsByName.Add(clip.animationNameId, nameClips);
                    }
                    nameClips.Add(clip);
                }

                if(clip.animationSetQualified != null)
                {
                    List<AtomAnimationClip> setClips;
                    if (!_firstClipOfLayerBySetQualifiedId.TryGetValue(clip.animationSetQualifiedId, out setClips))
                    {
                        setClips = new List<AtomAnimationClip>();
                        _firstClipOfLayerBySetQualifiedId.Add(clip.animationSetQualifiedId, setClips);
                    }
                    if (setClips.All(c => c.animationLayerQualified != clip.animationLayerQualified))
                        setClips.Add(clip);
                }

                {
                    List<AtomAnimationClip> layerClips;
                    if (!_clipsByLayerNameQualifiedId.TryGetValue(clip.animationLayerQualifiedId, out layerClips))
                    {
                        layerClips = new List<AtomAnimationClip>();
                        clipsGroupedByLayer.Add(layerClips);
                        _clipsByLayerNameQualifiedId.Add(clip.animationLayerQualifiedId, layerClips);
                    }
                    layerClips.Add(clip);
                }

                foreach (var target in clip.targetControllers)
                {
                    List<FreeControllerV3AnimationTarget> byController;
                    if (!_clipsByController.TryGetValue(target.animatableRef, out byController))
                    {
                        byController = new List<FreeControllerV3AnimationTarget>();
                        _clipsByController.Add(target.animatableRef, byController);
                    }
                    byController.Add(target);
                }

                foreach (var target in clip.targetFloatParams)
                {
                    List<JSONStorableFloatAnimationTarget> byFloatParam;
                    if (!_clipsByFloatParam.TryGetValue(target.animatableRef, out byFloatParam))
                    {
                        byFloatParam = new List<JSONStorableFloatAnimationTarget>();
                        _clipsByFloatParam.Add(target.animatableRef, byFloatParam);
                    }
                    byFloatParam.Add(target);
                }
            }

            useSegment = segmentNames.Count > 1 || segmentNames[0] != AtomAnimationClip.NoneAnimationSegment;
        }

        public IList<AtomAnimationClip> ByLayerQualified(int layerNameQualifiedId)
        {
            List<AtomAnimationClip> clips;
            return _clipsByLayerNameQualifiedId.TryGetValue(layerNameQualifiedId, out clips) ? clips : _emptyClipList;
        }

        public IEnumerable<KeyValuePair<FreeControllerV3Ref, List<FreeControllerV3AnimationTarget>>> ByController()
        {
            return _clipsByController;
        }

        public IEnumerable<KeyValuePair<JSONStorableFloatRef, List<JSONStorableFloatAnimationTarget>>> ByFloatParam()
        {
            return _clipsByFloatParam;
        }

        public IList<AtomAnimationClip> ByName(string name)
        {
            return ByName(name.ToId());
        }

        public IList<AtomAnimationClip> ByName(string animationSegment, string animationName)
        {
            return ByName(animationSegment.ToId(), animationName.ToId());
        }

        public IList<AtomAnimationClip> ByName(int animationSegmentId, int animationNameId)
        {
            IndexedSegment segment;
            if (!segmentsById.TryGetValue(animationSegmentId, out segment))
            {
                if (animationSegmentId == AtomAnimationClip.SharedAnimationSegmentId)
                    return _emptyClipList;
                if (!segmentsById.TryGetValue(AtomAnimationClip.SharedAnimationSegmentId, out segment))
                    return _emptyClipList;
            }
            List<AtomAnimationClip> clips;
            if (!segment.clipMapByNameId.TryGetValue(animationNameId, out clips))
                return _emptyClipList;
            return clips;
        }

        public IList<AtomAnimationClip> ByName(int id)
        {
            List<AtomAnimationClip> clips;
            return _clipsByName.TryGetValue(id, out clips) ? clips : _emptyClipList;
        }

        public IList<AtomAnimationClip> GetSiblingsByLayer(AtomAnimationClip clip)
        {
            List<AtomAnimationClip> clips;
            var result = clip.animationSetQualified != null
                ? _firstClipOfLayerBySetQualifiedId.TryGetValue(clip.animationSetQualifiedId, out clips)
                    ? clips
                    : _emptyClipList
                : ByName(clip.animationNameId);
            for (var i = 0; i < result.Count; i++)
            {
                if (result[i].animationLayerQualified != clip.animationLayerQualified) continue;
                result[i] = clip;
                break;
            }
            return result;
        }
    }
}
