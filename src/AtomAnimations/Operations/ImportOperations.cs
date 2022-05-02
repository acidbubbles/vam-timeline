using System.Collections.Generic;
using System.Linq;

namespace VamTimeline
{
    public class ImportOperations : ClipManagementOperationsBase
    {
        private readonly AtomAnimation _animation;
        private readonly bool _silent;

        public ImportOperations(AtomAnimation animation, bool silent = false)
            : base(animation)
        {
            _animation = animation;
            _silent = silent;
        }

        public void ImportClips(IList<AtomAnimationClip> clips)
        {
            // Unique segments
            var segments = clips.GroupBy(c => c.animationSegment).ToList();

            _animation.index.StartBulkUpdates();
            try
            {
                foreach (var segment in segments)
                {
                    string segmentName;
                    if (segment.Key == AtomAnimationClip.SharedAnimationSegment)
                        segmentName = GetNewSegmentName();
                    if (_animation.index.segmentNames.Contains(segment.Key))
                        segmentName = GetNewSegmentName(segment.Key);
                    else
                        segmentName = segment.Key;

                    foreach (var clip in segment)
                    {
                        clip.Validate();
                        clip.animationSegment = segmentName;
                        _animation.AddClip(clip);
                    }
                }
            }
            finally
            {
                _animation.index.EndBulkUpdates();
            }

            foreach (var clip in clips)
            {
                if (clip.autoPlay && _animation.index.ByLayer(clip.animationLayerQualified).Any(c => c.autoPlay))
                {
                    clip.autoPlay = false;
                }
            }

            _animation.RebuildAnimationNow();
        }

        private string GenerateUniqueAnimationName(string animationLayerQualified, string animationName)
        {
            var i = 1;
            var layerClips = _animation.index.ByLayer(animationLayerQualified);
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
