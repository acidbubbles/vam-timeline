using System.Collections.Generic;

namespace VamTimeline
{
    public class AtomAnimationsClipsIndex
    {
        public IEnumerable<string> clipNames => _clipsByName.Keys;

        private readonly List<AtomAnimationClip> _clips;

        private readonly Dictionary<string, List<AtomAnimationClip>> _clipsByLayer = new Dictionary<string, List<AtomAnimationClip>>();
        private readonly Dictionary<string, List<AtomAnimationClip>> _clipsByName = new Dictionary<string, List<AtomAnimationClip>>();
        private readonly Dictionary<FreeControllerV3Ref, List<FreeControllerAnimationTarget>> _clipsByController = new Dictionary<FreeControllerV3Ref, List<FreeControllerAnimationTarget>>();
        private readonly Dictionary<StorableFloatParamRef, List<FloatParamAnimationTarget>> _clipsByFloatParam = new Dictionary<StorableFloatParamRef, List<FloatParamAnimationTarget>>();
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
                    List<FreeControllerAnimationTarget> byController;
                    if (!_clipsByController.TryGetValue(target.controllerRef, out byController))
                    {
                        byController = new List<FreeControllerAnimationTarget>();
                        _clipsByController.Add(target.controllerRef, byController);
                    }
                    byController.Add(target);
                }

                foreach (var target in clip.targetFloatParams)
                {
                    List<FloatParamAnimationTarget> byFloatParam;
                    if (!_clipsByFloatParam.TryGetValue(target.floatParamRef, out byFloatParam))
                    {
                        byFloatParam = new List<FloatParamAnimationTarget>();
                        _clipsByFloatParam.Add(target.floatParamRef, byFloatParam);
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

        public IEnumerable<KeyValuePair<FreeControllerV3Ref, List<FreeControllerAnimationTarget>>> ByController()
        {
            return _clipsByController;
        }

        public IEnumerable<KeyValuePair<StorableFloatParamRef, List<FloatParamAnimationTarget>>> ByFloatParam()
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
