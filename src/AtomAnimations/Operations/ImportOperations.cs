using System.Collections.Generic;
using System.Linq;

namespace VamTimeline
{
    public class ImportOperations
    {
        public const string NewSegmentValue = "[NEW SEGMENT]";

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

        public void PopulateValidChoices(AtomAnimationClip clip, JSONStorableString statusJSON, JSONStorableString nameJSON, JSONStorableStringChooser layerJSON, JSONStorableStringChooser segmentJSON, JSONStorableBool okJSON)
        {
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

            var layers = clip.GetAllCurveTargets().ToList();

            if (layers.Any(l => sharedTargets.Any(c => c.TargetsSameAs(l))))
            {
                okJSON.val = false;
                statusJSON.valNoCallback = "Targets reserved by shared segment";
                return;
            }

            var validExistingLayers = _animation.index.clipsGroupedByLayer
                .Select(l => l[0])
                .Where(c =>
                {
                    var importedTargets = c.GetAllCurveTargets().ToList();
                    if (importedTargets.Count != layers.Count) return false;
                    return importedTargets.All(t => layers.Any(l => l.TargetsSameAs(t)));
                })
                .ToList();

            nameJSON.valNoCallback = clip.animationName;

            var targetSegments = validExistingLayers.Select(l => l.animationSegment).Distinct().ToList();
            if (!_animation.index.segmentNames.Contains(clip.animationSegment))
                targetSegments.Add(clip.animationSegment);
            else
                targetSegments.Add(NewSegmentValue);
            segmentJSON.choices = targetSegments;
            if (!targetSegments.Contains(segmentJSON.val)) segmentJSON.valNoCallback = targetSegments.FirstOrDefault() ?? "";
            AtomAnimationsClipsIndex.IndexedSegment selectedSegment;
            var existingSegment = _animation.index.segmentsById.TryGetValue(segmentJSON.val.ToId(), out selectedSegment);

            if (existingSegment)
            {
                var validExistingSegmentLayers = validExistingLayers.Where(l => l.animationSegment == segmentJSON.val).ToList();
                var targetLayers = validExistingSegmentLayers.Select(l => l.animationLayer).ToList();
                layerJSON.choices = targetLayers;
                if (!targetLayers.Contains(layerJSON.val)) layerJSON.valNoCallback = targetLayers.FirstOrDefault() ?? "";
            }
            else
            {
                layerJSON.choices = new List<string>(new[] { clip.animationLayer });
                layerJSON.valNoCallback = clip.animationLayer;
            }

            okJSON.val = segmentJSON.val == NewSegmentValue || layerJSON.val != "";
            statusJSON.valNoCallback = "";
        }
    }
}
