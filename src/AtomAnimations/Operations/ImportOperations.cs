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
            foreach (var clip in clips.SelectMany(c => ImportClip(c)))
            {
                _animation.AddClip(clip);
                importedClips.Add(clip);
            }

            foreach (var clip in importedClips)
            {
                if (clip.autoPlay && _animation.clips.Any(c => c.animationLayer == clip.animationLayer && c.autoPlay))
                {
                    clip.autoPlay = false;
                }
                if (clip.nextAnimationName != null && !_animation.clips.Any(c => c.animationLayer == clip.animationLayer && c.animationName == clip.nextAnimationName))
                {
                    clip.nextAnimationName = null;
                    clip.nextAnimationTime = 0f;
                }
            }

            _animation.Initialize();
            _animation.RebuildAnimationNow();
        }

        public List<AtomAnimationClip> ImportClip(AtomAnimationClip clip)
        {
            var clips = new List<AtomAnimationClip>();

            foreach (var controller in clip.targetControllers.Select(t => t.controller))
            {
                if (_animation.clips.Where(c => c.animationLayer != clip.animationLayer).Any(c => c.targetControllers.Any(t => t.controller == controller)))
                {
                    if (!_silent) SuperController.LogError($"Timeline: Imported animation contains controller {controller.name} in layer {clip.animationLayer}, but that controller is already used elsewhere in your animation.");
                    return clips;
                }
            }

            foreach (var floatParam in clip.targetFloatParams.Select(t => t.name))
            {
                if (_animation.clips.Where(c => c.animationLayer != clip.animationLayer).Any(c => c.targetFloatParams.Any(t => t.name == floatParam)))
                {
                    if (!_silent) SuperController.LogError($"Timeline: Imported animation contains storable float {floatParam} in layer {clip.animationLayer}, but that storable is already used elsewhere in your animation.");
                    return clips;
                }
            }

            var existingClip = _animation.GetClip(clip.animationName);
            if (existingClip != null)
            {
                if (existingClip.IsEmpty())
                {
                    var clipToRemove = _animation.GetClip(clip.animationName);
                    _animation.clips.Remove(clipToRemove);
                    clipToRemove.Dispose();
                }
                else
                {
                    var newAnimationName = GenerateUniqueAnimationName(clip.animationName);
                    if (!_silent) SuperController.LogError($"Timeline: Imported clip '{clip.animationName}' already exists and will be imported with the name {newAnimationName}");
                    clip.animationName = newAnimationName;
                }
            }

            clips.Add(clip);
            return clips;
        }

        private string GenerateUniqueAnimationName(string animationName)
        {
            var i = 1;
            while (true)
            {
                var newAnimationName = $"{animationName} ({i})";
                if (!_animation.clips.Any(c => c.animationName == newAnimationName))
                    return newAnimationName;
                i++;
            }
        }
    }
}
