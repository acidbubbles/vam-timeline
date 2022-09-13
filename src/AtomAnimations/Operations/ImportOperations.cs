using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VamTimeline
{
    public class ImportOperations
    {
        private readonly AtomAnimation _animation;

        public ImportOperations(AtomAnimation animation)
        {
            _animation = animation;
        }

        public ImportOperationClip PrepareClip(AtomAnimationClip clip)
        {
            return new ImportOperationClip(_animation, clip);
        }
    }

    public class ImportOperationClip
    {
        private readonly AtomAnimation _animation;
        public readonly AtomAnimationClip clip;
        public readonly JSONStorableBool okJSON;
        public readonly JSONStorableStringChooser segmentJSON;
        public readonly JSONStorableStringChooser layerJSON;
        public readonly JSONStorableString nameJSON;
        public readonly JSONStorableString statusJSON;
        public readonly JSONStorableBool includeJSON;

        public ImportOperationClip(AtomAnimation animation, AtomAnimationClip clip)
        {
            _animation = animation;
            this.clip = clip;
            statusJSON = new JSONStorableString("Status", "");
            nameJSON = new JSONStorableString("Name", clip.animationName, (string _) => PopulateValidChoices());
            layerJSON = new JSONStorableStringChooser("Layer", new List<string>(), clip.animationLayer, "Layer", (string _) => PopulateValidChoices());
            segmentJSON = new JSONStorableStringChooser("Segment", new List<string>(), clip.animationSegment, "Segment", (string _) => PopulateValidChoices());
            okJSON = new JSONStorableBool("Valid for import", false);
            includeJSON = new JSONStorableBool("Selected for import", true);
            PopulateValidChoices();
        }

        public void PopulateValidChoices()
        {
            List<ICurveAnimationTarget> sharedTargets;
            if (!clip.isOnSharedSegment && _animation.index.segmentIds.Contains(AtomAnimationClip.SharedAnimationSegmentId))
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
                statusJSON.valNoCallback = "Targets reserved by shared segment.";
                PopulateTargetsInStatus();
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
            {
                targetSegments.Add(clip.animationSegment);
            }
            else if(!clip.isOnSharedSegment)
            {
                var newSegmentName = _animation.GetUniqueSegmentName(clip.animationSegment);
                targetSegments.Add(newSegmentName);
            }

            segmentJSON.choices = targetSegments;
            if (!targetSegments.Contains(segmentJSON.val)) segmentJSON.valNoCallback = targetSegments.FirstOrDefault() ?? "";
            AtomAnimationsClipsIndex.IndexedSegment selectedSegment;
            var existingSegment = _animation.index.segmentsById.TryGetValue(segmentJSON.val.ToId(), out selectedSegment);
            List<string> animationsOnLayer;

            if (existingSegment)
            {
                var validExistingSegmentLayers = validExistingLayers.Where(l => l.animationSegment == segmentJSON.val).ToList();
                var targetLayers = validExistingSegmentLayers.Select(l => l.animationLayer).ToList();
                layerJSON.choices = targetLayers;
                if (!targetLayers.Contains(layerJSON.val)) layerJSON.valNoCallback = targetLayers.FirstOrDefault() ?? "";
                animationsOnLayer = selectedSegment.layersMapById[layerJSON.val.ToId()].Select(c => c.animationName).ToList();
            }
            else
            {
                layerJSON.choices = new List<string>(new[] { clip.animationLayer });
                layerJSON.valNoCallback = clip.animationLayer;
                animationsOnLayer = new List<string>();
            }

            var animNameAvailable = !animationsOnLayer.Contains(nameJSON.val);
            if (!animNameAvailable)
            {
                okJSON.val = false;
                statusJSON.val = "Animation name not available on layer.";
                return;
            }

            if (layerJSON.val == "")
            {
                okJSON.val = false;
                statusJSON.val = "No compatible layer available on this segment.";
                PopulateTargetsInStatus();
                return;
            }

            okJSON.val = true;
            statusJSON.valNoCallback = "";
        }

        private void PopulateTargetsInStatus()
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            foreach (var target in clip.GetAllTargets())
            {
                if(target is FreeControllerV3AnimationTarget)
                    sb.Append("Control: ");
                else if ((target as JSONStorableFloatAnimationTarget)?.animatableRef.IsMorph() ?? false)
                    sb.Append("Morph: ");
                else if (target is JSONStorableFloatAnimationTarget)
                    sb.Append("Float Param: ");
                else if (target is TriggersTrackAnimationTarget)
                    sb.Append("Triggers: ");
                else
                    sb.Append("Unknown: ");

                sb.AppendLine(target.GetFullName());
            }
            statusJSON.valNoCallback += sb.ToString();
        }

        public void ImportClip()
        {
            if (!okJSON.val || !includeJSON.val) return;
            clip.animationSegment = segmentJSON.val;
            clip.animationLayer = layerJSON.val;
            clip.animationName = nameJSON.val;
            if (clip.autoPlay && _animation.index.ByLayerQualified(clip.animationLayerQualifiedId).Any(c => c.autoPlay))
            {
                clip.autoPlay = false;
            }
            clip.Validate();
            _animation.AddClip(clip);
        }
    }
}
