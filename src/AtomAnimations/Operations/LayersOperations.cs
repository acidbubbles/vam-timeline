using System;
using System.Collections.Generic;
using System.Linq;
using Leap.Unity;

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

        public AtomAnimationClip Add(string clipName, string layerName)
        {
            return _animation.CreateClip(clipName, layerName, _clip.animationSegment);
        }

        public List<AtomAnimationClip> AddAndCarry(string layerName)
        {
            return _animation.index.ByLayerQualified(_clip.animationLayerQualifiedId)
                .Select(c =>
                {
                    var r = _animation.CreateClip(c.animationName, layerName, c.animationSegment);
                    c.CopySettingsTo(r);
                    return r;
                })
                .ToList();
        }

        public List<AtomAnimationClip> SplitLayer(List<IAtomAnimationTarget> targets, string layerName = null)
        {
            var created = new List<AtomAnimationClip>();
            if (layerName == null)
                layerName = GetSplitLayerName(_clip.animationLayer, _animation.index.segmentsById[_clip.animationSegmentId].layerNames);
            foreach (var sourceClip in _animation.index.ByLayerQualified(_clip.animationLayerQualifiedId).ToList())
            {
                var newClip = _animation.CreateClip(sourceClip.animationName, layerName, _clip.animationSegment);
                sourceClip.CopySettingsTo(newClip);
                foreach (var t in sourceClip.GetAllTargets().Where(t => targets.Any(t.TargetsSameAs)).ToList())
                {
                    sourceClip.Remove(t);
                    newClip.Add(t);
                }
                created.Add(newClip);
            }
            return created;
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
