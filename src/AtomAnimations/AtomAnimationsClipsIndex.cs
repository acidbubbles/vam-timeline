using System.Collections.Generic;

namespace VamTimeline
{
    public class AtomAnimationsClipsIndex
    {
        private readonly List<AtomAnimationClip> _clips;

        private readonly Dictionary<string, List<AtomAnimationClip>> _clipsByLayer = new Dictionary<string, List<AtomAnimationClip>>();
        private readonly Dictionary<FreeControllerV3, List<FreeControllerAnimationTarget>> _clipsByController = new Dictionary<FreeControllerV3, List<FreeControllerAnimationTarget>>();
        private readonly Dictionary<string, List<FloatParamAnimationTarget>> _clipsByFloatParam = new Dictionary<string, List<FloatParamAnimationTarget>>();

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
            if (_paused) return;

            _clipsByController.Clear();
            _clipsByFloatParam.Clear();
            _clipsByLayer.Clear();

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
                    if (!_clipsByController.TryGetValue(target.controller, out byController))
                    {
                        byController = new List<FreeControllerAnimationTarget>();
                        _clipsByController.Add(target.controller, byController);
                    }
                    byController.Add(target);
                }

                foreach (var target in clip.targetFloatParams)
                {
                    List<FloatParamAnimationTarget> byfloatParam;
                    if (!_clipsByFloatParam.TryGetValue(target.name, out byfloatParam))
                    {
                        byfloatParam = new List<FloatParamAnimationTarget>();
                        _clipsByFloatParam.Add(target.name, byfloatParam);
                    }
                    byfloatParam.Add(target);
                }
            }
            // TODO: This could be optimized but it would be more complex (e.g. adding/removing targets, renaming layers, etc.)
        }

        public IEnumerable<KeyValuePair<string, List<AtomAnimationClip>>> ByLayer()
        {
            return _clipsByLayer;
        }

        public IEnumerable<AtomAnimationClip> ByLayer(string layer)
        {
            return _clipsByLayer[layer];
        }
    }
}
