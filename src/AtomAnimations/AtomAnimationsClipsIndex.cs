using System.Collections.Generic;

namespace VamTimeline
{
    public class AtomAnimationsClipsIndex
    {
        private readonly List<AtomAnimationClip> _clips;

        private readonly Dictionary<string, List<AtomAnimationClip>> _clipsByLayer = new Dictionary<string, List<AtomAnimationClip>>();
        private readonly Dictionary<FreeControllerV3, List<FreeControllerAnimationTarget>> _clipsByController = new Dictionary<FreeControllerV3, List<FreeControllerAnimationTarget>>();
        private readonly Dictionary<string, List<FloatParamAnimationTarget>> _clipsByFloatParam = new Dictionary<string, List<FloatParamAnimationTarget>>();
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

            foreach (var list in _clipsByController)
            {
                list.Value.Sort(new FreeControllerAnimationTargetParentedLastComparer());
            }
        }

        public IEnumerable<KeyValuePair<string, List<AtomAnimationClip>>> ByLayer()
        {
            return _clipsByLayer;
        }

        public IEnumerable<AtomAnimationClip> ByLayer(string layer)
        {
            List<AtomAnimationClip> clip;
            return _clipsByLayer.TryGetValue(layer, out clip) ? clip : _emptyClipList;
        }

        public IEnumerable<KeyValuePair<FreeControllerV3, List<FreeControllerAnimationTarget>>> ByController()
        {
            return _clipsByController;
        }

        public IEnumerable<KeyValuePair<string, List<FloatParamAnimationTarget>>> ByFloatParam()
        {
            return _clipsByFloatParam;
        }

        private class FreeControllerAnimationTargetParentedLastComparer : IComparer<FreeControllerAnimationTarget>
        {
            public int Compare(FreeControllerAnimationTarget x, FreeControllerAnimationTarget y)
            {
                var xHasParent = x.parentRigidbodyId != null;
                var yHasParent = y.parentRigidbodyId != null;
                if (xHasParent & !yHasParent) return 1;
                if (!xHasParent & yHasParent) return -1;
                return 0;
            }
        }
    }
}
