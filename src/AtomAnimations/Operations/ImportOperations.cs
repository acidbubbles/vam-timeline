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
            if (clips.Count == 0)
            {
                SuperController.LogError("Timeline: There was no clips to import.");
                return;
            }

            // Force use segments
            if (_animation.index.segmentIds.Count > 0 && _animation.index.segmentIds[0] == AtomAnimationClip.NoneAnimationSegmentId)
            {
                SuperController.LogError("Timeline: Segments are required for import. Go to Add Animations, Use Segments.");
                return;
            }

            // Convert to segments
            var nonSegmentAssignation = _animation.GetUniqueSegmentName(AtomAnimationClip.DefaultAnimationSegment);
            foreach (var clip in clips.Where(c => string.IsNullOrEmpty(c.animationSegment) || c.isOnNoneSegment))
            {
                clip.animationSegment = nonSegmentAssignation;
            }

            // Unique segments
            var segments = clips.GroupBy(c => c.animationSegment).ToList();

            List<ICurveAnimationTarget> sharedTargets;
            if (_animation.index.segmentIds.Contains(AtomAnimationClip.SharedAnimationSegmentId))
            {
                sharedTargets = _animation.index.segmentsById[AtomAnimationClip.SharedAnimationSegmentId].layers
                    .Select(l => l[0])
                    .SelectMany(c => c.GetAllCurveTargets())
                    .Distinct()
                    .ToList();
            }
            else
            {
                sharedTargets = new List<ICurveAnimationTarget>();
            }

            _animation.index.StartBulkUpdates();
            try
            {
                foreach (var segment in segments)
                {
                    var importedTargets = segment
                        .SelectMany(c => c.GetAllCurveTargets())
                        .Distinct()
                        .ToList();
                    if (importedTargets.Any(t => sharedTargets.Any(t.TargetsSameAs)))
                    {
                        SuperController.LogError("Timeline: Imported animations contain shared segments that conflicts with existing animations. Skipping.");
                        continue;
                    }

                    foreach (var clip in segment)
                    {
                        clip.Validate();
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
