using System.Collections.Generic;

namespace VamTimeline
{
    public class AtomAnimationsClipsIndex
    {
        public IEnumerable<string> clipNames => _clipsByName.Keys;

        private readonly List<AtomAnimationClip> _clips;

        private readonly Dictionary<string, List<AtomAnimationClip>> _clipsByLayer = new Dictionary<string, List<AtomAnimationClip>>();
        private readonly Dictionary<string, List<AtomAnimationClip>> _clipsByName = new Dictionary<string, List<AtomAnimationClip>>();
        private readonly Dictionary<FreeControllerV3Ref, List<FreeControllerV3AnimationTarget>> _clipsByController = new Dictionary<FreeControllerV3Ref, List<FreeControllerV3AnimationTarget>>();
        private readonly Dictionary<JSONStorableFloatRef, List<JSONStorableFloatAnimationTarget>> _clipsByFloatParam = new Dictionary<JSONStorableFloatRef, List<JSONStorableFloatAnimationTarget>>();
        private readonly List<AtomAnimationClip> _emptyClipList = new List<AtomAnimationClip>();
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
            _clipsByName.Clear();
            _clipsByLayer.Clear();
            _clipsByController.Clear();
            _clipsByFloatParam.Clear();

            foreach (var clip in _clips)
            {
                {
                    List<AtomAnimationClip> nameClips;
                    if (!_clipsByName.TryGetValue(clip.animationName, out nameClips))
                    {
                        nameClips = new List<AtomAnimationClip>();
                        _clipsByName.Add(clip.animationName, nameClips);
                    }
                    nameClips.Add(clip);
                }
            }

            if (_paused) return;

            foreach (var clip in _clips)
            {
                {
                    List<AtomAnimationClip> layerClips;
                    if (!_clipsByLayer.TryGetValue(clip.animationLayer, out layerClips))
                    {
                        layerClips = new List<AtomAnimationClip>();
                        _clipsByLayer.Add(clip.animationLayer, layerClips);
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
            // TODO: This could be optimized but it would be more complex (e.g. adding/removing targets, renaming layers, etc.)
        }

        public IEnumerable<KeyValuePair<string, List<AtomAnimationClip>>> ByLayer()
        {
            return _clipsByLayer;
        }

        public IList<AtomAnimationClip> ByLayer(string layer)
        {
            List<AtomAnimationClip> clips;
            return _clipsByLayer.TryGetValue(layer, out clips) ? clips : _emptyClipList;
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
    }
}
