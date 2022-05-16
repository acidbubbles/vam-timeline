using System.Collections.Generic;
using System.Linq;

namespace VamTimeline
{
    public class AtomAnimationsClipsIndex
    {
        public class IndexedSegment
        {
            public readonly Dictionary<int, List<AtomAnimationClip>> layersMapById = new Dictionary<int, List<AtomAnimationClip>>();
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
            }
        }

        public IEnumerable<string> clipNames => _clipsByName.Keys;

        private readonly List<AtomAnimationClip> _clips;

        public readonly IndexedSegment emptySegment = new IndexedSegment();
        public readonly Dictionary<int, IndexedSegment> segmentsById = new Dictionary<int, IndexedSegment>();
        public bool useSegment;
        public readonly List<int> segmentIds = new List<int>();
        public readonly List<string> segmentNames = new List<string>();
        public readonly IList<List<AtomAnimationClip>> clipsGroupedByLayer = new List<List<AtomAnimationClip>>();
        private readonly Dictionary<string, List<AtomAnimationClip>> _clipsByLayerNameQualified = new Dictionary<string, List<AtomAnimationClip>>();
        private readonly Dictionary<string, List<AtomAnimationClip>> _clipsByName = new Dictionary<string, List<AtomAnimationClip>>();
        private readonly Dictionary<string, List<AtomAnimationClip>> _firstClipOfLayerBySetQualified = new Dictionary<string, List<AtomAnimationClip>>();
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
            _clipsByLayerNameQualified.Clear();
            _clipsByName.Clear();
            _firstClipOfLayerBySetQualified.Clear();
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
                    if (!_clipsByName.TryGetValue(clip.animationName, out nameClips))
                    {
                        nameClips = new List<AtomAnimationClip>();
                        _clipsByName.Add(clip.animationName, nameClips);
                    }
                    nameClips.Add(clip);
                }

                if(clip.animationSetQualified != null)
                {
                    List<AtomAnimationClip> setClips;
                    if (!_firstClipOfLayerBySetQualified.TryGetValue(clip.animationSetQualified, out setClips))
                    {
                        setClips = new List<AtomAnimationClip>();
                        _firstClipOfLayerBySetQualified.Add(clip.animationSetQualified, setClips);
                    }
                    if (setClips.All(c => c.animationLayerQualified != clip.animationLayerQualified))
                        setClips.Add(clip);
                }

                {
                    List<AtomAnimationClip> layerClips;
                    if (!_clipsByLayerNameQualified.TryGetValue(clip.animationLayerQualified, out layerClips))
                    {
                        layerClips = new List<AtomAnimationClip>();
                        clipsGroupedByLayer.Add(layerClips);
                        _clipsByLayerNameQualified.Add(clip.animationLayerQualified, layerClips);
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

        public IList<AtomAnimationClip> ByLayer(string layerNameQualified)
        {
            List<AtomAnimationClip> clips;
            return _clipsByLayerNameQualified.TryGetValue(layerNameQualified, out clips) ? clips : _emptyClipList;
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
            List<AtomAnimationClip> clips;
            return _clipsByName.TryGetValue(name, out clips) ? clips : _emptyClipList;
        }

        public IList<AtomAnimationClip> GetSiblingsByLayer(AtomAnimationClip clip)
        {
            List<AtomAnimationClip> clips;
            var result = clip.animationSetQualified != null
                ? _firstClipOfLayerBySetQualified.TryGetValue(clip.animationSetQualified, out clips)
                    ? clips
                    : _emptyClipList
                : ByName(clip.animationName);
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
