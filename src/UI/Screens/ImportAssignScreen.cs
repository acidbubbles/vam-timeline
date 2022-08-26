using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VamTimeline
{
    public class ImportAssignScreen : ScreenBase
    {
        public const string ScreenName = "Import (Assign)";

        public override string screenId => ScreenName;

        private List<ImportedAnimationPanel> _imported = new List<ImportedAnimationPanel>();

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
            operations.Import().PopulateValidChoices(clip, statusJSON, nameJSON, layerJSON, segmentJSON, okJSON);

            var sb = new StringBuilder();
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

