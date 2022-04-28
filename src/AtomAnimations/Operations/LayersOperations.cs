using System;
using System.Collections.Generic;
using System.Linq;

namespace VamTimeline
{
    public class LayersOperations : ClipManagementOperationsBase
    {
        private readonly AtomAnimation _animation;
        private readonly AtomAnimationClip _clip;

        public LayersOperations(AtomAnimation animation, AtomAnimationClip clip)
            : base(animation, clip)
        {
            _animation = animation;
            _clip = clip;
        }

        public AtomAnimationClip Add()
        {
            return _animation.CreateClip(GetNewLayerName(), GetNewAnimationName(), _clip.animationSegment);
        }

        public void SplitLayer(List<IAtomAnimationTarget> targets)
        {
            var layerName = GetSplitLayerName(_clip.animationLayer, _animation.index.segments[_clip.animationSegment].layerNames);
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
