using System;
using System.Collections.Generic;
using System.Linq;

namespace VamTimeline
{
    public abstract class ClipManagementOperationsBase
    {
        private readonly AtomAnimation _animation;

        protected ClipManagementOperationsBase(AtomAnimation animation)
        {
            _animation = animation;
        }

        public string GetNewSegmentName(string prefix = "Segment")
        {
            var layerNames = _animation.index.segmentNames;
            for (var i = 1; i < 999; i++)
            {
                var segmentName = $"{prefix} {i}";
                if (!layerNames.Contains(segmentName)) return segmentName;
            }
            return Guid.NewGuid().ToString();
        }

        public string GetNewLayerName(string segmentName)
        {
            List<string> layerNames;
            AtomAnimationsClipsIndex.IndexedSegment segment;
            if (_animation.index.segments.TryGetValue(segmentName, out segment))
                layerNames = segment.layerNames;
            else
                layerNames = new List<string>();

            for (var i = 1; i < 999; i++)
            {
                var layerName = "Layer " + i;
                if (!layerNames.Contains(layerName)) return layerName;
            }
            return Guid.NewGuid().ToString();
        }

        public string GetNewAnimationName(string name = "Anim")
        {
            for (var i = 1; i < 999; i++)
            {
                var animationName = $"{name} {i}";
                if (_animation.clips.All(c => c.animationName != animationName)) return animationName;
            }
            return Guid.NewGuid().ToString();
        }
    }
}
