using System;
using System.Collections.Generic;
using System.Linq;
using MVR.FileManagementSecure;
using SimpleJSON;

namespace VamTimeline
{
    public class ImportExportScreen : ScreenBase
    {
        private const string _saveExt = "json";
        private const string _saveFolder = "Saves\\PluginData\\animations";
        private const string _exportCurrentAnimation = "Current animation";
        private const string _exportCurrentSegment = "Current segment";
        private const string _exportAll = "All segments";

        public const string ScreenName = "Import / Export";

        public override string screenId => ScreenName;

        private JSONStorableStringChooser _exportIncludeJSON;
        private JSONStorableBool _exportPoseJSON;

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            CreateChangeScreenButton("<b><</b> <i>Back</i>", MoreScreen.ScreenName);

            prefabFactory.CreateSpacer();

            prefabFactory.CreateHeader("Import", 1);

            InitImportUI();

            prefabFactory.CreateSpacer();

            prefabFactory.CreateHeader("Export", 1);

            InitExportUI();
        }

        private void InitImportUI()
        {
            var importUI = prefabFactory.CreateButton("Import animation(s)");
            importUI.button.onClick.AddListener(Import);
        }

        private void InitExportUI()
        {
            _exportIncludeJSON = new JSONStorableStringChooser(
                "Include",
                new List<string>
                {
                    _exportCurrentAnimation,
                    _exportCurrentSegment,
                    _exportAll
                },
                _exportCurrentSegment,
                "Include", (string _) => SyncExportPose()
            )
            {
                isStorable = false
            };
            prefabFactory.CreatePopup(_exportIncludeJSON, true, true, 800f);

            _exportPoseJSON = new JSONStorableBool("Include Pose", true);
            prefabFactory.CreateToggle(_exportPoseJSON);

            var exportUI = prefabFactory.CreateButton("Export");
            exportUI.button.onClick.AddListener(Export);
        }

        private void Export()
        {
            try
            {
                #if (VAM_GT_1_20)
                FileManagerSecure.CreateDirectory(_saveFolder);
                #endif
                var fileBrowserUI = SuperController.singleton.fileBrowserUI;
                fileBrowserUI.SetTitle("Export animation");
                fileBrowserUI.fileRemovePrefix = null;
                fileBrowserUI.hideExtension = false;
                fileBrowserUI.keepOpen = false;
                fileBrowserUI.fileFormat = _saveExt;
                fileBrowserUI.defaultPath = _saveFolder;
                fileBrowserUI.showDirs = true;
                fileBrowserUI.shortCuts = null;
                fileBrowserUI.browseVarFilesAsDirectories = false;
                fileBrowserUI.SetTextEntry(true);
                fileBrowserUI.Show(ExportFileSelected);
                fileBrowserUI.ActivateFileNameField();
            }
            catch (Exception exc)
            {
                SuperController.LogError($"Timeline: Failed to save file dialog: {exc}");
            }
        }

        private void ExportFileSelected(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (!path.ToLower().EndsWith($".{_saveExt}")) path += $".{_saveExt}";

            try
            {
                var jc = new JSONClass
                {
                    ["Clips"] = GetExportClipsJson(),
                    ["AtomType"] = plugin.containingAtom.type,
                };
                SuperController.singleton.SaveJSON(jc, path);
                SuperController.singleton.DoSaveScreenshot(path);
            }
            catch (Exception exc)
            {
                SuperController.LogError($"Timeline: Failed to export animation: {exc}");
            }
        }

        private JSONArray GetExportClipsJson()
        {
            var clips = GetExportClips();

            var temporaryPose = _exportPoseJSON.val && clips[0].pose == null;
            if (temporaryPose)
                clips[0].pose = AtomPose.FromAtom(plugin.containingAtom, false, true, false, false);

            var clipsJSON = new JSONArray();
            foreach (var clip in clips)
            {
                clipsJSON.Add(plugin.serializer.SerializeClip(clip));
            }

            if (temporaryPose)
                clips[0].pose = null;

            return clipsJSON;
        }

        private List<AtomAnimationClip> GetExportClips()
        {
            List<AtomAnimationClip> clips;
            switch (_exportIncludeJSON.val)
            {
                case _exportCurrentAnimation:
                    clips = new List<AtomAnimationClip>(new[] { animationEditContext.current });
                    break;
                case _exportCurrentSegment:
                    clips = animationEditContext.currentSegment.layers.SelectMany(l => l).ToList();
                    break;
                case _exportAll:
                    clips = animation.clips;
                    break;
                default:
                    throw new InvalidOperationException($"Unknown export mode: {_exportIncludeJSON.val}");
            }

            return clips;
        }

        private void Import()
        {
            try
            {
                #if (VAM_GT_1_20)
                FileManagerSecure.CreateDirectory(_saveFolder);
                var shortcuts = FileManagerSecure.GetShortCutsForDirectory(_saveFolder);
                SuperController.singleton.GetMediaPathDialog(ImportFileSelected, _saveExt, _saveFolder, false, true, false, null, false, shortcuts);
                #else
                SuperController.singleton.GetMediaPathDialog(ImportFileSelected, _saveExt, _saveFolder);
                #endif
            }
            catch (Exception exc)
            {
                SuperController.LogError($"Timeline: Failed to open file dialog: {exc}");
            }
        }

        private void ImportFileSelected(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                var json = SuperController.singleton.LoadJSON(path);
                if (json["AtomType"]?.Value != plugin.containingAtom.type)
                {
                    SuperController.LogError($"Timeline: Loaded animation for {json["AtomType"]} but current atom type is {plugin.containingAtom.type}");
                    return;
                }

                var jc = json.AsObject;
                if (!ImportClips(jc)) return;
                ImportControllerStates(jc);

                var lastAnimation = animation.clips.Select(c => c.animationNameQualified).LastOrDefault();
                animationEditContext.SelectAnimation(lastAnimation);
            }
            catch (Exception exc)
            {
                SuperController.LogError($"Timeline.{nameof(AdvancedKeyframeToolsScreen)}.{nameof(ImportFileSelected)}: Failed to import animation: {exc}");
            }
        }

        private bool ImportClips(JSONClass jc)
        {
            var clipsJSON = jc["Clips"].AsArray;
            if (clipsJSON == null || clipsJSON.Count == 0)
            {
                SuperController.LogError("Timeline: Imported file does not contain any animations. Are you trying to load a scene file?");
                return false;
            }

            if (animation.clips.Count == 1 && animation.clips[0].IsEmpty())
                animation.RemoveClip(animation.clips[0]);

            var imported = new List<AtomAnimationClip>();
            foreach (JSONClass clipJSON in clipsJSON)
            {
                imported.Add(plugin.serializer.DeserializeClip(clipJSON, animation.animatables));
            }

            operations.Import().ImportClips(imported);

            if (imported.Count > 0) animationEditContext.SelectAnimation(imported.FirstOrDefault());
            else SuperController.LogError("Timeline: No animations were imported.");

            return true;
        }

        private void ImportControllerStates(JSONClass jc)
        {
            if (jc.HasKey("ControllersState"))
            {
                var controllersState = jc["ControllersState"].AsObject;
                foreach (var k in controllersState.Keys)
                {
                    var fc = plugin.containingAtom.freeControllers.FirstOrDefault(x => x.name == k);
                    if (fc == null)
                    {
                        SuperController.LogError($"Timeline: Loaded animation had state for controller {k} but no such controller were found on this atom.");
                        continue;
                    }
                    var state = controllersState[k];
                    fc.currentPositionState = (FreeControllerV3.PositionState)state["currentPositionState"].AsInt;
                    fc.transform.localPosition = AtomAnimationSerializer.DeserializeVector3(state["localPosition"].AsObject);
                    fc.currentRotationState = (FreeControllerV3.RotationState)state["currentRotationState"].AsInt;
                    fc.transform.localRotation = AtomAnimationSerializer.DeserializeQuaternion(state["localRotation"].AsObject);
                }
            }
        }

        protected override void OnCurrentAnimationChanged(AtomAnimationEditContext.CurrentAnimationChangedEventArgs args)
        {
            base.OnCurrentAnimationChanged(args);

            SyncExportPose();
        }

        private void SyncExportPose()
        {
            _exportPoseJSON.toggle.interactable = GetExportClips().GroupBy(c => c.animationLayer).Select(l => l.First()).All(c => c.pose == null);
            if (!_exportPoseJSON.toggle.interactable) _exportPoseJSON.valNoCallback = true;
        }
    }
}

