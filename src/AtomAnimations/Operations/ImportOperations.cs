using System.Collections.Generic;
using System.Linq;

namespace VamTimeline
{
    public class ImportOperations
    {
        private readonly AtomAnimation _animation;
        private readonly bool _silent;

        public ImportOperations(AtomAnimation animation, bool silent = false)
        {
            _animation = animation;
            _silent = silent;
        }

        public void ImportClips(IEnumerable<AtomAnimationClip> clips)
        {
            var importedClips = new List<AtomAnimationClip>();
            _animation.index.StartBulkUpdates();
            try
            {
                foreach (var clip in clips.SelectMany(ImportClip))
                {
                    clip.Validate();
                    _animation.AddClip(clip);
                    importedClips.Add(clip);
                }
            }
            finally
            {
                _animation.index.EndBulkUpdates();
            }

            foreach (var clip in importedClips)
            {
                if (clip.autoPlay && _animation.index.ByLayer(clip.animationLayer).Any(c => c.autoPlay))
                {
                    clip.autoPlay = false;
                }
            }

            _animation.RebuildAnimationNow();
        }

        public List<AtomAnimationClip> ImportClip(AtomAnimationClip clip)
        {
            var clips = new List<AtomAnimationClip>();

            var matchingLayer = _animation.clips.FirstOrDefault(c =>
            {
                // We only need to match float params and controllers, triggers can be in any layers
                if (!clip.targetFloatParams.All(t => c.targetFloatParams.Any(t.TargetsSameAs))) return false;
                if (!clip.targetControllers.All(t => c.targetControllers.Any(t.TargetsSameAs))) return false;
                return true;
            });

            if (matchingLayer != null)
            {
                clip.animationLayer = matchingLayer.animationLayer;

                // Add missing targets
                foreach (var target in matchingLayer.targetFloatParams)
                {
                    if (!clip.targetFloatParams.Any(t => target.TargetsSameAs(t)))
                    {
                        var missing = new FloatParamAnimationTarget(target);
                        missing.AddEdgeFramesIfMissing(clip.animationLength);
                        clip.Add(missing);
                    }
                }
                foreach (var target in matchingLayer.targetControllers)
                {
                    if (!clip.targetControllers.Any(t => target.TargetsSameAs(t)))
                    {
                        var missing = new FreeControllerAnimationTarget(target.controllerRef);
                        missing.AddEdgeFramesIfMissing(clip.animationLength);
                        clip.Add(missing);
                    }
                }
            }
            else if (_animation.index.ByLayer(clip.animationLayer).Any())
            {
                clip.animationLayer = new LayersOperations(_animation, clip).GetNewLayerName();
            }

            foreach (var controller in clip.targetControllers.Select(t => t.controllerRef))
            {
                if (_animation.clips.Where(c => c.animationLayer != clip.animationLayer).Any(c => c.targetControllers.Any(t => t.controllerRef == controller)))
                {
                    if (!_silent) SuperController.LogError($"Timeline: Imported animation contains controller {controller.name} in layer {clip.animationLayer}, but that controller is already used elsewhere in your animation. To import, a layer is needed with targets: {string.Join(", ", clip.GetAllCurveTargets().Select(c => c.name).ToArray())}");
                    return clips;
                }
            }

            foreach (var floatParam in clip.targetFloatParams.Select(t => t.name))
            {
                if (_animation.clips.Where(c => c.animationLayer != clip.animationLayer).Any(c => c.targetFloatParams.Any(t => t.name == floatParam)))
                {
                    if (!_silent) SuperController.LogError($"Timeline: Imported animation contains storable float {floatParam} in layer {clip.animationLayer}, but that storable is already used elsewhere in your animation. To import, a layer is needed with targets: {string.Join(", ", clip.GetAllCurveTargets().Select(c => c.name).ToArray())}");
                    return clips;
                }
            }

            var existingClip = _animation.GetClip(clip.animationLayer, clip.animationName);
            if (existingClip != null)
            {
                if (existingClip.IsEmpty())
                {
                    _animation.clips.Remove(existingClip);
                    existingClip.Dispose();
                }
                else
                {
                    var newAnimationName = GenerateUniqueAnimationName(clip.animationLayer, clip.animationName);
                    if (!_silent) SuperController.LogError($"Timeline: Imported clip '{clip.animationNameQualified}' already exists and will be imported with the name {newAnimationName}");
                    clip.animationName = newAnimationName;
                }
            }

            clips.Add(clip);
            return clips;
        }

        private string GenerateUniqueAnimationName(string animationLayer, string animationName)
        {
            var i = 1;
            var layerClips = _animation.clips.Where(c => c.animationLayer == animationLayer).ToList();
            while (true)
            {
                var newAnimationName = $"{animationName} ({i})";
                if (layerClips.All(c => c.animationName != newAnimationName))
                    return newAnimationName;
                i++;
            }
        }
    }
}
