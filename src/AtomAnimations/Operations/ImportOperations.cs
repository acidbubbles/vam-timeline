using System.Collections.Generic;
using System.Linq;

namespace VamTimeline
{
    public class ImportOperations
    {
        private readonly AtomAnimation _animation;

        public ImportOperations(AtomAnimation animation)
        {
            _animation = animation;
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
                        segmentName = _animation.GetUniqueSegmentName("Segment 1");
                    else if (_animation.index.segmentNames.Contains(segment.Key))
                        segmentName = _animation.GetUniqueSegmentName(segment.Key);
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
                if (clip.autoPlay && _animation.index.ByLayerQualified(clip.animationLayerQualifiedId).Any(c => c.autoPlay))
                {
                    clip.autoPlay = false;
                }
            }

            _animation.RebuildAnimationNow();
        }
    }
}
