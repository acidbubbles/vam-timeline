using System;
using System.Collections.Generic;
using System.Linq;

namespace VamTimeline
{
    public class LayersOperations
    {
        private readonly AtomAnimation _animation;
        private readonly AtomAnimationClip _clip;

        public LayersOperations(AtomAnimation animation, AtomAnimationClip clip)
        {
            _animation = animation;
            _clip = clip;
        }

        public AtomAnimationClip Add(string clipName = null, string layerName = null)
        {
            return _animation.CreateClip(layerName ?? _animation.GetUniqueLayerName(_clip), clipName ?? _animation.GetUniqueAnimationName(_clip), _clip.animationSegment);
        }

        public List<AtomAnimationClip> AddAndCarry(string layerName)
        {
            return _animation.index.ByLayer(_clip.animationLayerQualified)
                .Select(c =>
                {
                    var r = _animation.CreateClip(layerName, c.animationName, c.animationSegment);
                    c.CopySettingsTo(r);
                    return r;
                })
                .ToList();
        }

        public void SplitLayer(List<IAtomAnimationTarget> targets, string layerName = null)
        {
            if (layerName == null)
                layerName = GetSplitLayerName(_clip.animationLayer, _animation.index.segments[_clip.animationSegment].layerNames);
            foreach (var sourceClip in _animation.index.ByLayer(_clip.animationLayerQualified).ToList())
            {
                var newClip = _animation.CreateClip(layerName, sourceClip.animationName, _clip.animationSegment);
                sourceClip.CopySettingsTo(newClip);
                foreach (var t in sourceClip.GetAllTargets().Where(t => targets.Any(t.TargetsSameAs)).ToList())
                {
                    sourceClip.Remove(t);
                    newClip.Add(t);
                }
            }
        }

        private static string GetSplitLayerName(string sourceLayerName, IList<string> list)
        {
            for (var i = 1; i < 999; i++)
            {
                var animationName = $"{sourceLayerName} (Split {i})";
                if (list.All(n => n != animationName)) return animationName;
            }
            return Guid.NewGuid().ToString();
        }
    }
}
