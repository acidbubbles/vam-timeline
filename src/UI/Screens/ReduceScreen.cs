using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class ReduceScreen : ScreenBase
    {
        public const string ScreenName = "Reduce";

        private JSONStorableFloat _reduceMaxFramesPerSecondJSON;
        private JSONStorableBool _averageToSnapJSON;
        private JSONStorableBool _removeFlatSectionsKeyframes;
        private JSONStorableBool _simplifyKeyframes;
        private JSONStorableFloat _reduceMinDistanceJSON;
        private JSONStorableFloat _reduceMinRotationJSON;
        private JSONStorableFloat _reduceMinFloatParamRangeRatioJSON;

        public override string screenId => ScreenName;

        private UIDynamicButton _backupUI;
        private UIDynamicButton _restoreUI;
        private UIDynamicButton _reduceUI;

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            CreateChangeScreenButton("<b><</b> <i>Back</i>", MoreScreen.ScreenName);

            prefabFactory.CreateSpacer();

            _backupUI = prefabFactory.CreateButton("Backup");
            _backupUI.button.onClick.AddListener(TakeBackup);
            _restoreUI = prefabFactory.CreateButton("Restore");
            _restoreUI.button.onClick.AddListener(RestoreBackup);

            prefabFactory.CreateSpacer();

            CreateReduceSettingsUI();

            _reduceUI = prefabFactory.CreateButton("Reduce");
            _reduceUI.button.onClick.AddListener(Reduce);

            prefabFactory.CreateSpacer();

            CreateChangeScreenButton("<i>Go to <b>record</b> screen...</i>", RecordScreen.ScreenName);

            _restoreUI.button.interactable = HasBackup();
            if (HasBackup()) _restoreUI.label = $"Restore [{AtomAnimationBackup.singleton.backupTime}]";

            animationEditContext.animation.animatables.onTargetsSelectionChanged.AddListener(OnTargetsSelectionChanged);
            OnTargetsSelectionChanged();
        }

        private void CreateReduceSettingsUI()
        {
            _reduceMaxFramesPerSecondJSON = new JSONStorableFloat("Frames per second", 25f, 1f, 100f);
            _reduceMaxFramesPerSecondJSON.setCallbackFunction = val => _reduceMaxFramesPerSecondJSON.valNoCallback = Mathf.Round(val);
            _averageToSnapJSON = new JSONStorableBool("Average and snap to fps", true);
            _removeFlatSectionsKeyframes = new JSONStorableBool("Remove flat sections", true);
            _simplifyKeyframes = new JSONStorableBool("Simplify keyframes", true);
            _reduceMinDistanceJSON = new JSONStorableFloat("Minimum meaningful distance", 0.1f, 0f, 1f, false);
            _reduceMinRotationJSON = new JSONStorableFloat("Minimum meaningful rotation (dot)", 0.001f, 0f, 1f);
            _reduceMinFloatParamRangeRatioJSON = new JSONStorableFloat("Minimum meaningful float range ratio", 0.01f, 0f, 1f);
            prefabFactory.CreateSlider(_reduceMaxFramesPerSecondJSON).valueFormat = "F1";
            prefabFactory.CreateToggle(_averageToSnapJSON);
            prefabFactory.CreateToggle(_removeFlatSectionsKeyframes);
            prefabFactory.CreateToggle(_simplifyKeyframes);
            prefabFactory.CreateSlider(_reduceMinDistanceJSON).valueFormat = "F3";
            prefabFactory.CreateSlider(_reduceMinRotationJSON).valueFormat = "F4";
            prefabFactory.CreateSlider(_reduceMinFloatParamRangeRatioJSON).valueFormat = "F3";
        }

        protected override void OnCurrentAnimationChanged(AtomAnimationEditContext.CurrentAnimationChangedEventArgs args)
        {
            base.OnCurrentAnimationChanged(args);
            _restoreUI.button.interactable = false;
        }

        private void Reduce()
        {
            if (!HasBackup())
                TakeBackup();
            else
                RestoreBackup();

            _reduceUI.button.interactable = false;
            _reduceUI.label = "Please be patient...";
            var settings = new ReduceSettings
            {
                fps = (int)_reduceMaxFramesPerSecondJSON.val,
                avgToSnap = _averageToSnapJSON.val,
                removeFlats = _removeFlatSectionsKeyframes.val,
                simplify = _simplifyKeyframes.val,
                minMeaningfulDistance = _reduceMinDistanceJSON.val,
                minMeaningfulRotation = _reduceMinRotationJSON.val,
                minMeaningfulFloatParamRangeRatio = _reduceMinFloatParamRangeRatioJSON.val,
            };
            StartCoroutine(operations.Reduce(settings).ReduceKeyframes(
                animationEditContext.current.GetAllCurveTargets().Where(t => t.selected).ToList(),
                progress =>
                {
                    _reduceUI.label = $"{progress.stepsDone} of {progress.stepsTotal} ({progress.timeLeft:0}s left)";
                },
                () =>
                {
                    _reduceUI.button.interactable = true;
                    _reduceUI.label = "Reduce";
                }));
        }

        private void OnTargetsSelectionChanged()
        {
            _reduceUI.button.interactable = current.GetAllCurveTargets().Any(t => t.selected);
        }

        private bool HasBackup()
        {
            return AtomAnimationBackup.singleton.HasBackup(current);
        }

        private void TakeBackup()
        {
            AtomAnimationBackup.singleton.TakeBackup(current);
            _restoreUI.label = $"Restore [{AtomAnimationBackup.singleton.backupTime}]";
            _restoreUI.button.interactable = true;
        }

        private void RestoreBackup()
        {
            AtomAnimationBackup.singleton.RestoreBackup(current);
        }

        public override void OnDestroy()
        {
            animationEditContext.animation.animatables.onTargetsSelectionChanged.RemoveListener(OnTargetsSelectionChanged);
            base.OnDestroy();
        }
    }
}

