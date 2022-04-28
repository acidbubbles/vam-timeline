using System;
using System.Linq;

namespace VamTimeline
{
    public abstract class ClipManagementOperationsBase
    {
        private readonly AtomAnimation _animation;
        private readonly AtomAnimationClip _clip;

        protected ClipManagementOperationsBase(AtomAnimation animation, AtomAnimationClip clip)
        {
            _animation = animation;
            _clip = clip;
        }

        public string GetNewSegmentName()
        {
            var layerNames = _animation.index.segmentNames;
            for (var i = 1; i < 999; i++)
            {
                var segmentName = "Segment " + i;
                if (!layerNames.Contains(segmentName)) return segmentName;
            }
            return Guid.NewGuid().ToString();
        }

        public string GetNewLayerName()
        {
            var layerNames = _animation.index.segments[_clip.animationSegment].layerNames;
            for (var i = 1; i < 999; i++)
            {
                var layerName = "Layer " + i;
                if (!layerNames.Contains(layerName)) return layerName;
            }
            return Guid.NewGuid().ToString();
        }

        protected string GetNewAnimationName()
        {
            for (var i = _animation.clips.Count + 1; i < 999; i++)
            {
                var animationName = "Anim " + i;
                if (_animation.clips.All(c => c.animationName != animationName)) return animationName;
            }
            return Guid.NewGuid().ToString();
        }
    }
}
