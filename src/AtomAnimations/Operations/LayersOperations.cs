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

        public AtomAnimationClip Add()
        {
            return _animation.CreateClip(GetNewLayerName());
        }

        public void SplitLayer(List<IAtomAnimationTarget> targets)
        {
            var newLayerName = GetNewLayerName();
            foreach (var clip in _animation.clips.Where(c => c.animationLayer == _clip.animationLayer).ToList())
            {
                var targetsToMove = clip.GetAllTargets().Where(t => targets.Any(t2 => t2.TargetsSameAs(t))).ToList();

                foreach (var t in targetsToMove)
                    clip.Remove(t);

                var newClip = _animation.CreateClip(newLayerName);
                newClip.animationLength = clip.animationLength;
                newClip.blendDuration = clip.blendDuration;
                newClip.nextAnimationName = clip.nextAnimationName;
                newClip.nextAnimationTime = clip.nextAnimationTime;
                newClip.animationName = GetSplitAnimationName(clip.animationName);

                foreach (var m in targetsToMove)
                    newClip.Add(m);
            }
        }

        private string GetNewLayerName()
        {
            var layers = new HashSet<string>(_animation.clips.Select(c => c.animationLayer));
            for (var i = 1; i < 999; i++)
            {
                var layerName = "Layer " + i;
                if (!layers.Contains(layerName)) return layerName;
            }
            return Guid.NewGuid().ToString();
        }

        private string GetSplitAnimationName(string animationName)
        {
            for (var i = 1; i < 999; i++)
            {
                var newName = $"{animationName} (Split {i})";
                if (!_animation.clips.Any(c => c.animationName == newName)) return newName;
            }
            return Guid.NewGuid().ToString();
        }
    }
}
