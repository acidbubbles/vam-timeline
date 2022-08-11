using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VamTimeline
{
    public class ImportAssignScreen : ScreenBase
    {
        public const string ScreenName = "Import (Assign)";
        private const string _newSegmentValue = "[NEW SEGMENT]";

        public override string screenId => ScreenName;

        private List<ImportedAnimationPanel> _imported = new List<ImportedAnimationPanel>();
        private List<ICurveAnimationTarget> _sharedTargets;

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            CreateChangeScreenButton("<b><</b> <i>Cancel</i>", ImportExportScreen.ScreenName);

            List<AtomAnimationClip> clips;
            if (!(arg is List<AtomAnimationClip>))
            {
                prefabFactory.CreateTextField(new JSONStorableString("Error", "No animations to import"));
                return;
            }

            clips = (List<AtomAnimationClip>)arg;

            InitSharedTargets();

            prefabFactory.CreateSpacer();
            prefabFactory.CreateHeader("Animations", 1);
            prefabFactory.CreateButton("Deselect All").button.onClick.AddListener(() =>
            {
                foreach (var imported in _imported)
                {
                    imported.includeJSON.val = false;
                }
            });
            prefabFactory.CreateButton("Select All").button.onClick.AddListener(() =>
            {
                foreach (var imported in _imported)
                {
                    imported.includeJSON.val = false;
                }
            });

            InitOverviewUI(clips);

            prefabFactory.CreateSpacer();
            prefabFactory.CreateHeader("Import", 1);

            InitImportUI();
        }

        private void InitSharedTargets()
        {
            List<string> sharedLayers;
            if (animation.index.segmentIds.Contains(AtomAnimationClip.SharedAnimationSegmentId))
            {
                _sharedTargets = animation.index.segmentsById[AtomAnimationClip.SharedAnimationSegmentId].layers
                    .Select(l => l[0])
                    .SelectMany(c => c.GetAllCurveTargets())
                    .Distinct()
                    .ToList();
                sharedLayers = animation.index.segmentsById[AtomAnimationClip.SharedAnimationSegmentId].layerNames;
            }
            else
            {
                _sharedTargets = new List<ICurveAnimationTarget>();
                sharedLayers = new List<string>();
            }
        }


        public void InitOverviewUI(List<AtomAnimationClip> clips)
        {
            foreach (var clip in clips)
            {
                _imported.Add(InitClipUI(clip));
            }
        }

        private ImportedAnimationPanel InitClipUI(AtomAnimationClip clip)
        {
            var statusJSON = new JSONStorableString("Status", "Status: OK");
            var nameJSON = new JSONStorableString("Animation name", clip.animationName);
            var layerJSON = new JSONStorableStringChooser("Layer", new List<string>(), clip.animationLayer, "Layer");
            var segmentJSON = new JSONStorableStringChooser("Segment", new List<string>(), clip.animationSegment, "Segment");
            var includeJSON = new JSONStorableBool("Selected for import", true);
            var okJSON = new JSONStorableBool("Valid", false);

            nameJSON.setCallbackFunction = val =>
            {
                clip.animationName = val;
                PopulateValidChoices(clip, statusJSON, nameJSON, layerJSON, segmentJSON, okJSON);
            };
            layerJSON.setCallbackFunction = val =>
            {
                clip.animationLayer = val;
                PopulateValidChoices(clip, statusJSON, nameJSON, layerJSON, segmentJSON, okJSON);
            };
            segmentJSON.setCallbackFunction = val =>
            {
                clip.animationSegment = val;
                PopulateValidChoices(clip, statusJSON, nameJSON, layerJSON, segmentJSON, okJSON);
            };
            PopulateValidChoices(clip, statusJSON, nameJSON, layerJSON, segmentJSON, okJSON);

            prefabFactory.CreateSpacer();
            prefabFactory.CreateHeader(clip.animationNameQualified, 1);
            prefabFactory.CreateToggle(includeJSON);
            prefabFactory.CreateTextField(statusJSON).height = 150;
            prefabFactory.CreateTextInput(nameJSON);
            prefabFactory.CreatePopup(segmentJSON, false, true);
            prefabFactory.CreatePopup(layerJSON, false, true);
            prefabFactory.CreateToggle(okJSON).toggle.interactable = false;

            return new ImportedAnimationPanel
            {
                okJSON = okJSON,
                includeJSON = includeJSON,
                clip = clip
            };
        }

        private void PopulateValidChoices(AtomAnimationClip clip, JSONStorableString statusJSON, JSONStorableString nameJSON, JSONStorableStringChooser layerJSON, JSONStorableStringChooser segmentJSON, JSONStorableBool okJSON)
        {
            var sb = new StringBuilder();

            var layers = clip.GetAllCurveTargets().ToList();

            if (layers.Any(l => _sharedTargets.Any(c => c.TargetsSameAs(l))))
            {
                okJSON.val = false;
                sb.AppendLine("Targets reserved by shared segment");
                return;
            }

            var validExistingLayers = animation.index.clipsGroupedByLayer
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
            if (!animation.index.segmentNames.Contains(clip.animationSegment))
                targetSegments.Add(clip.animationSegment);
            else
                targetSegments.Add(_newSegmentValue);
            segmentJSON.choices = targetSegments;
            if (!targetSegments.Contains(segmentJSON.val)) segmentJSON.valNoCallback = targetSegments.FirstOrDefault() ?? "";
            AtomAnimationsClipsIndex.IndexedSegment selectedSegment;
            var existingSegment = animation.index.segmentsById.TryGetValue(segmentJSON.val.ToId(), out selectedSegment);

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

            okJSON.val = segmentJSON.val == _newSegmentValue || layerJSON.val != "";

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

            statusJSON.valNoCallback = sb.ToString();
            sb.Length = 0;
        }

        public void InitImportUI()
        {
            var btn = prefabFactory.CreateButton("Import");
            btn.button.onClick.AddListener(Import);
        }

        public void Import()
        {
            var clips = _imported.Where(i => i.okJSON.val && i.includeJSON.val).Select(i => i.clip).ToList();
            if (clips.Count == 0) return;
            operations.Import().ImportClips(clips);
            plugin.serializer.RestoreMissingTriggers(animation);
            animation.index.Rebuild();
            animationEditContext.SelectAnimation(clips[0]);
            ChangeScreen(TargetsScreen.ScreenName);
        }

        public override void OnDestroy()
        {
            animation.animatables.locked = false;
            base.OnDestroy();
        }

        private class ImportedAnimationPanel
        {
            public JSONStorableBool okJSON;
            public JSONStorableBool includeJSON;
            public AtomAnimationClip clip;
        }
    }
}

