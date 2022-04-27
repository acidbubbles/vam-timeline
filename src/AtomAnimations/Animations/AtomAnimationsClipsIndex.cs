using System.Collections.Generic;
using System.Linq;

namespace VamTimeline
{
    public class AtomAnimationsClipsIndex
    {
        public IEnumerable<string> clipNames => _clipsByName.Keys;
        public string mainLayer { get; private set; }

        private readonly List<AtomAnimationClip> _clips;

        private readonly IList<List<AtomAnimationClip>> _clipsByLayer = new List<List<AtomAnimationClip>>();
        private readonly Dictionary<string, List<AtomAnimationClip>> _clipsByLayerName = new Dictionary<string, List<AtomAnimationClip>>();
        private readonly Dictionary<string, List<AtomAnimationClip>> _clipsByName = new Dictionary<string, List<AtomAnimationClip>>();
        private readonly Dictionary<string, List<AtomAnimationClip>> _clipsBySet = new Dictionary<string, List<AtomAnimationClip>>();
        private readonly Dictionary<string, List<AtomAnimationClip>> _clipsBySetByLayer = new Dictionary<string, List<AtomAnimationClip>>();
        private readonly Dictionary<FreeControllerV3Ref, List<FreeControllerV3AnimationTarget>> _clipsByController = new Dictionary<FreeControllerV3Ref, List<FreeControllerV3AnimationTarget>>();
        private readonly Dictionary<JSONStorableFloatRef, List<JSONStorableFloatAnimationTarget>> _clipsByFloatParam = new Dictionary<JSONStorableFloatRef, List<JSONStorableFloatAnimationTarget>>();
        private readonly List<AtomAnimationClip> _emptyClipList = new List<AtomAnimationClip>();
        public readonly List<string> sequences = new List<string>();
        private bool _paused;

        public AtomAnimationsClipsIndex(List<AtomAnimationClip> clips)
        {
            _clips = clips;
        }

        public void StartBulkUpdates()
        {
            _paused = true;
        }

        public void EndBulkUpdates()
        {
            _paused = false;
            Rebuild();
        }

        public void Rebuild()
        {
            _clipsByLayer.Clear();
            _clipsByLayerName.Clear();
            _clipsByName.Clear();
            _clipsBySet.Clear();
            _clipsBySetByLayer.Clear();
            _clipsByController.Clear();
            _clipsByFloatParam.Clear();
            sequences.Clear();
            sequences.Add(AtomAnimationClip.DefaultAnimationSequence);

            if (_clips == null || _clips.Count == 0) return;

            mainLayer = _clips[0].animationLayer;

            foreach (var clip in _clips)
            {
                if (!sequences.Contains(clip.animationSequence))
                {
                    sequences.Add(clip.animationSequence);
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

                if(clip.animationSet != null)
                {
                    List<AtomAnimationClip> setClips;
                    if (!_clipsBySet.TryGetValue(clip.animationSet, out setClips))
                    {
                        setClips = new List<AtomAnimationClip>();
                        _clipsBySet.Add(clip.animationSet, setClips);
                    }
                    setClips.Add(clip);

                    List<AtomAnimationClip> setByLayerClips;
                    if (!_clipsBySetByLayer.TryGetValue(clip.animationSet, out setByLayerClips))
                    {
                        setByLayerClips = new List<AtomAnimationClip>();
                        _clipsBySetByLayer.Add(clip.animationSet, setByLayerClips);
                    }
                    if (setByLayerClips.All(c => c.animationLayer != clip.animationLayer))
                        setByLayerClips.Add(clip);
                }
            }

            // TODO: Why?
            if (_paused) return;

            foreach (var clip in _clips)
            {
                {
                    List<AtomAnimationClip> layerClips;
                    if (!_clipsByLayerName.TryGetValue(clip.animationLayer, out layerClips))
                    {
                        layerClips = new List<AtomAnimationClip>();
                        _clipsByLayer.Add(layerClips);
                        _clipsByLayerName.Add(clip.animationLayer, layerClips);
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
        }

        public IList<List<AtomAnimationClip>> ByLayer()
        {
            return _clipsByLayer;
        }

        public IList<AtomAnimationClip> ByLayer(string layer)
        {
            List<AtomAnimationClip> clips;
            return _clipsByLayerName.TryGetValue(layer, out clips) ? clips : _emptyClipList;
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

        public IList<AtomAnimationClip> BySet(string set)
        {
            List<AtomAnimationClip> clips;
            return _clipsBySet.TryGetValue(set, out clips) ? clips : _emptyClipList;
        }

        public IList<AtomAnimationClip> GetSiblingsByLayer(AtomAnimationClip clip)
        {
            List<AtomAnimationClip> clips;
            var result = clip.animationSet != null
                ? _clipsBySetByLayer.TryGetValue(clip.animationSet, out clips)
                    ? clips
                    : _emptyClipList
                : ByName(clip.animationName);
            for (var i = 0; i < result.Count; i++)
            {
                if (result[i].animationLayer != clip.animationLayer) continue;
                result[i] = clip;
                break;
            }
            return result;
        }
    }
}
