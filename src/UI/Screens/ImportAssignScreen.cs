using System;
using System.Collections.Generic;
using System.Linq;

namespace VamTimeline
{
    public class ImportAssignScreen : ScreenBase
    {
        public const string ScreenName = "Import (Assign)";

        public override string screenId => ScreenName;

        private readonly List<ImportOperationClip> _imported = new List<ImportOperationClip>();
        private readonly List<UIDynamicButton> _importBtns = new List<UIDynamicButton>();

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
            prefabFactory.CreateButton("Deselect all").button.onClick.AddListener(() =>
            {
                foreach (var imported in _imported)
                {
                    imported.includeJSON.val = false;
                }
            });
            prefabFactory.CreateButton("Select all").button.onClick.AddListener(() =>
            {
                foreach (var imported in _imported)
                {
                    imported.includeJSON.val = true;
                }
            });
            prefabFactory.CreateButton("Prefer existing segment").button.onClick.AddListener(() =>
            {
                foreach (var imported in _imported)
                {
                    var match = imported.segmentJSON.choices.FirstOrDefault(c => animation.index.segmentNames.Contains(c));
                    if(match != null) imported.segmentJSON.val = match;
                }
            });
            prefabFactory.CreateButton("Prefer new segment").button.onClick.AddListener(() =>
            {
                foreach (var imported in _imported)
                {
                    var match = imported.segmentJSON.choices.FirstOrDefault(c => !animation.index.segmentNames.Contains(c));
                    if(match != null) imported.segmentJSON.val = match;
                }
            });
            _importBtns.Add(InitImportUI());

            InitOverviewUI(clips);

            prefabFactory.CreateSpacer();
            prefabFactory.CreateHeader("Import", 1);

            _importBtns.Add(InitImportUI());

            OnUpdated();
        }

        public void InitOverviewUI(List<AtomAnimationClip> clips)
        {
            foreach (var clip in clips)
            {
                _imported.Add(InitClipUI(clip));
            }
        }

        private ImportOperationClip InitClipUI(AtomAnimationClip clip)
        {
            var imported = operations.Import().PrepareClip(clip);

            prefabFactory.CreateSpacer().height = 40f;
            prefabFactory.CreateHeader(clip.animationNameQualified, 1);
            prefabFactory.CreateToggle(imported.includeJSON);
            prefabFactory.CreateTextField(imported.statusJSON).height = 150;
            prefabFactory.CreateTextInput(imported.nameJSON);
            prefabFactory.CreatePopup(imported.segmentJSON, false, true);
            prefabFactory.CreatePopup(imported.layerJSON, false, true);
            prefabFactory.CreateToggle(imported.okJSON).toggle.interactable = false;
            imported.updated.AddListener(OnUpdated);

            return imported;
        }

        private void OnUpdated()
        {
            var btnName = $"Import ({_imported.Count(i => i.okJSON.val && i.includeJSON.val)})";
            foreach (var importBtn in _importBtns)
            {
                importBtn.label = btnName;
            }
        }

        public UIDynamicButton InitImportUI()
        {
            var btn = prefabFactory.CreateButton("Import");
            btn.button.onClick.AddListener(Import);
            return btn;
        }

        public void Import()
        {
            var clips = _imported.Where(i => i.okJSON.val && i.includeJSON.val).ToList();
            if (clips.Count == 0) return;
            animation.index.StartBulkUpdates();
            try
            {
                foreach (var imported in _imported)
                {
                    imported.ImportClip();
                }
            }
            catch (Exception exc)
            {
                SuperController.LogError($"Timeline: Something went wrong during the import: {exc}");
            }
            finally
            {
                animation.index.EndBulkUpdates();
            }
            plugin.serializer.RestoreMissingTriggers(animation);
            animation.index.Rebuild();
            animationEditContext.SelectAnimation(clips[0].clip);
            ChangeScreen(TargetsScreen.ScreenName);
        }

        public override void OnDestroy()
        {
            animation.animatables.locked = false;
            foreach (var imported in _imported)
            {
                imported.updated.RemoveAllListeners();
            }
            base.OnDestroy();
        }
    }
}

