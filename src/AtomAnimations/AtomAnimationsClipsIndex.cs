using System.Collections.Generic;

namespace VamTimeline
{
    public class AtomAnimationsClipsIndex
    {
        private readonly List<AtomAnimationClip> _clips;

        private readonly Dictionary<string, AtomAnimationClip> _clipsByLayer = new Dictionary<string, AtomAnimationClip>();
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
            // TODO: This could be optimized but it would be more complex (e.g. adding/removing targets, renaming layers, etc.)
        }
    }
}
