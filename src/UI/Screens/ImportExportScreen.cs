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
        private const string _saveFolder = "Saves\\animations";
        private const string _poseAndAllAnimations = "Pose & all animations";
        private const string _allAnimations = "All animations";

        public const string ScreenName = "Import / Export";

        public override string screenId => ScreenName;

        private JSONStorableStringChooser _exportAnimationsJSON;

        public ImportExportScreen()
            : base()
        {
        }

        public override void Init(IAtomPlugin plugin)
        {
            base.Init(plugin);

            CreateChangeScreenButton("<b><</b> <i>Back</i>", MoreScreen.ScreenName);

            prefabFactory.CreateSpacer();

            InitImportUI();

            prefabFactory.CreateSpacer();

            InitExportUI();
        }

        private void InitImportUI()
        {
            var importUI = prefabFactory.CreateButton("Import animation(s)");
            importUI.button.onClick.AddListener(() => Import());
        }

        private void InitExportUI()
        {
            _exportAnimationsJSON = new JSONStorableStringChooser("Animation to export", new List<string> { _poseAndAllAnimations, _allAnimations }.Concat(animation.clips.Where(c => !c.transition).Select(c => c.animationName)).ToList(), _poseAndAllAnimations, "Animation to export")
            {
                isStorable = false
            };
            var exportAnimationsUI = prefabFactory.CreateScrollablePopup(_exportAnimationsJSON);

            var exportUI = prefabFactory.CreateButton("Export animation");
            exportUI.button.onClick.AddListener(() => Export());
        }

        private void Export()
        {
            try
            {
                FileManagerSecure.CreateDirectory(_saveFolder);
                var fileBrowserUI = SuperController.singleton.fileBrowserUI;
                fileBrowserUI.SetTitle("Save animation");
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
                    ["Clips"] = GetClipsJson(),
                    ["AtomType"] = plugin.containingAtom.type,
                    ["ControllersState"] = GetAtomStateJson()
                };
                SuperController.singleton.SaveJSON(jc, path);
                SuperController.singleton.DoSaveScreenshot(path);
            }
            catch (Exception exc)
            {
                SuperController.LogError($"Timeline: Failed to export animation: {exc}");
            }
        }

        private JSONArray GetClipsJson()
        {
            var all = _exportAnimationsJSON.val == _poseAndAllAnimations || _exportAnimationsJSON.val == _allAnimations;
            IEnumerable<AtomAnimationClip> clips;
            if (all)
                clips = animation.clips;
            else
                clips = animation.clips.Where(c => c.animationName == _exportAnimationsJSON.val);
            var clipsJSON = new JSONArray();

            foreach (var clip in clips)
            {
                clipsJSON.Add(plugin.serializer.SerializeClip(clip));
            }

            return clipsJSON;
        }

        private JSONClass GetAtomStateJson()
        {
            var atomState = new JSONClass();
            IEnumerable<FreeControllerV3> controllers;
            if (_exportAnimationsJSON.val == _poseAndAllAnimations)
                controllers = plugin.containingAtom.freeControllers;
            else if (_exportAnimationsJSON.val == _allAnimations)
                controllers = animation.clips
                      .SelectMany(c => c.targetControllers)
                      .Select(t => t.controller)
                      .Distinct();
            else
                controllers = animation.clips
                    .First(c => c.animationName == _exportAnimationsJSON.val)
                    .targetControllers
                    .Select(t => t.controller);
            foreach (var fc in controllers)
            {
                if (fc.name == "control") continue;
                if (!fc.name.EndsWith("Control")) continue;
                atomState[fc.name] = new JSONClass
                    {
                        {"currentPositionState", ((int)fc.currentPositionState).ToString()},
                        {"localPosition", AtomAnimationSerializer.SerializeVector3(fc.transform.localPosition)},
                        {"currentRotationState", ((int)fc.currentRotationState).ToString()},
                        {"localRotation", AtomAnimationSerializer.SerializeQuaternion(fc.transform.localRotation)}
                    };
            }
            return atomState;
        }

        private void Import()
        {
            try
            {
                FileManagerSecure.CreateDirectory(_saveFolder);
                var shortcuts = FileManagerSecure.GetShortCutsForDirectory(_saveFolder);
                SuperController.singleton.GetMediaPathDialog(ImportFileSelected, _saveExt, _saveFolder, false, true, false, null, false, shortcuts);
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

                var lastAnimation = animation.clips.Select(c => c.animationName).LastOrDefault();
                // NOTE: Because the animation instance changes, we'll end up with the _old_ "current" not being updated.
                if (lastAnimation != animation.current.animationName)
                    plugin.ChangeAnimation(lastAnimation);
                else
                    animation.SelectAnimation(lastAnimation);
                animation.Sample();
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AdvancedKeyframeToolsScreen)}.{nameof(ImportFileSelected)}: Failed to import animation: {exc}");
            }
        }

        private bool ImportClips(JSONClass jc)
        {
            var clipsJSON = jc["Clips"].AsArray;
            if (clipsJSON == null || clipsJSON.Count == 0)
            {
                SuperController.LogError($"Timeline: Imported file does not contain any animations. Are you trying to load a scene file?");
                return false;
            }
            var importedClips = new List<AtomAnimationClip>();
            foreach (JSONClass clipJSON in clipsJSON)
            {
                var clip = ImportClip(clipJSON);
                if (clip == null) continue;
                animation.AddClip(clip);
                importedClips.Add(clip);
            }
            foreach (var clip in importedClips)
            {
                if (clip.autoPlay && animation.clips.Any(c => c.animationLayer == clip.animationLayer && c.autoPlay))
                {
                    clip.autoPlay = false;
                }
                if (clip.nextAnimationName != null && !animation.clips.Any(c => c.animationLayer == clip.animationLayer && c.animationName == clip.nextAnimationName))
                {
                    clip.nextAnimationName = null;
                    clip.nextAnimationTime = 0f;
                }
            }
            animation.Initialize();
            animation.RebuildAnimationNow();
            return true;
        }

        private AtomAnimationClip ImportClip(JSONClass clipJSON)
        {
            var clip = plugin.serializer.DeserializeClip(clipJSON);

            foreach (var controller in clip.targetControllers.Select(t => t.controller))
            {
                if (animation.clips.Where(c => c.animationLayer != clip.animationLayer).Any(c => c.targetControllers.Any(t => t.controller == controller)))
                {
                    SuperController.LogError($"Timeline: Imported animation contains controller {controller.name} in layer {clip.animationLayer}, but that controller is already used elsewhere in your animation.");
                    return null;
                }
            }

            foreach (var floatParam in clip.targetFloatParams.Select(t => t.name))
            {
                if (animation.clips.Where(c => c.animationLayer != clip.animationLayer).Any(c => c.targetFloatParams.Any(t => t.name == floatParam)))
                {
                    SuperController.LogError($"Timeline: Imported animation contains storable float {floatParam} in layer {clip.animationLayer}, but that storable is already used elsewhere in your animation.");
                    return null;
                }
            }

            var existingClip = animation.GetClip(clip.animationName);
            if (existingClip != null)
            {
                if (existingClip.IsEmpty())
                {
                    var clipToRemove = animation.GetClip(clip.animationName);
                    animation.clips.Remove(clipToRemove);
                    clipToRemove.Dispose();
                }
                else
                {
                    var newAnimationName = GenerateUniqueAnimationName(animation, clip.animationName);
                    SuperController.LogError($"Timeline: Imported clip '{clip.animationName}' already exists and will be imported with the name {newAnimationName}");
                    clip.animationName = newAnimationName;
                }
            }

            return clip;
        }

        private static string GenerateUniqueAnimationName(AtomAnimation animation, string animationName)
        {
            var i = 1;
            while (true)
            {
                var newAnimationName = $"{animationName} ({i})";
                if (!animation.clips.Any(c => c.animationName == newAnimationName))
                    return newAnimationName;
                i++;
            }
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
    }
}

