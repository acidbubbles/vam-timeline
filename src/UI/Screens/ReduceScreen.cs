using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class ReduceScreen : ScreenBase
    {
        public const string ScreenName = "Reduce";

        private JSONStorableFloat _maxFramesPerSecondJSON;
        private JSONStorableBool _roundJSON;
        private JSONStorableBool _removeFlatSectionsKeyframes;
        private JSONStorableBool _simplifyKeyframes;
        private JSONStorableFloat _minDistanceJSON;
        private JSONStorableFloat _minRotationJSON;
        private JSONStorableFloat _minFloatParamRangeRatioJSON;

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
            _reduceUI.buttonColor = Color.green;

            CreateChangeScreenButton("<i>Go to <b>record</b> screen...</i>", RecordScreen.ScreenName);

            _restoreUI.button.interactable = HasBackup();
            if (HasBackup()) _restoreUI.label = $"Restore [{AtomAnimationBackup.singleton.backupTime}]";

            animationEditContext.animation.animatables.onTargetsSelectionChanged.AddListener(OnTargetsSelectionChanged);
            OnTargetsSelectionChanged();
        }

        private void CreateReduceSettingsUI()
        {
            _roundJSON = new JSONStorableBool("Round key time to fps", false);
            _maxFramesPerSecondJSON = new JSONStorableFloat("Max frames per second", 10f, 1f, 50f);
            _maxFramesPerSecondJSON.setCallbackFunction = val => _maxFramesPerSecondJSON.valNoCallback = Mathf.Round(val);
            _removeFlatSectionsKeyframes = new JSONStorableBool("Remove flat sections", true);
            _simplifyKeyframes = new JSONStorableBool("Simplify keyframes", true);
            _minDistanceJSON = new JSONStorableFloat("Minimum meaningful distance", 0.1f, 0f, 1f, false);
            _minRotationJSON = new JSONStorableFloat("Minimum meaningful rotation (dot)", 0.001f, 0f, 1f);
            _minFloatParamRangeRatioJSON = new JSONStorableFloat("Minimum meaningful float range ratio", 0.01f, 0f, 1f);
            prefabFactory.CreateToggle(_removeFlatSectionsKeyframes);
            prefabFactory.CreateSpacer();
            prefabFactory.CreateToggle(_simplifyKeyframes);
            prefabFactory.CreateSlider(_minDistanceJSON).valueFormat = "F3";
            prefabFactory.CreateSlider(_minRotationJSON).valueFormat = "F4";
            prefabFactory.CreateSlider(_minFloatParamRangeRatioJSON).valueFormat = "F3";
            prefabFactory.CreateSpacer();
            prefabFactory.CreateSlider(_maxFramesPerSecondJSON).valueFormat = "F1";
            prefabFactory.CreateToggle(_roundJSON);
            prefabFactory.CreateSpacer();
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
                fps = (int)_maxFramesPerSecondJSON.val,
                round = _roundJSON.val,
                removeFlats = _removeFlatSectionsKeyframes.val,
                simplify = _simplifyKeyframes.val,
                minMeaningfulDistance = _minDistanceJSON.val,
                minMeaningfulRotation = _minRotationJSON.val,
                minMeaningfulFloatParamRangeRatio = _minFloatParamRangeRatioJSON.val,
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

