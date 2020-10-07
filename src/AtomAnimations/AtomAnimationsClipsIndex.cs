using System.Collections.Generic;

namespace VamTimeline
{
    public class AtomAnimationsClipsIndex
    {
        private readonly List<AtomAnimationClip> _clips;

        private readonly Dictionary<string, List<AtomAnimationClip>> _clipsByLayer = new Dictionary<string, List<AtomAnimationClip>>();
        private readonly List<KeyValuePair<FreeControllerV3, FreeControllerAnimationTarget>> _clipsByController = new List<KeyValuePair<FreeControllerV3, FreeControllerAnimationTarget>>();
        private readonly List<KeyValuePair<JSONStorableFloat, FloatParamAnimationTarget>> _clipsByFloatParam = new List<KeyValuePair<JSONStorableFloat, FloatParamAnimationTarget>>();

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
                List<AtomAnimationClip> layerClips;
                if (!_clipsByLayer.TryGetValue(clip.animationLayer, out layerClips))
                {
                    layerClips = new List<AtomAnimationClip>();
                    _clipsByLayer.Add(clip.animationLayer, layerClips);
                }
                layerClips.Add(clip);
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
