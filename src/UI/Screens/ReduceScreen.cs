using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class ReduceScreen : ScreenBase
    {
        public const string ScreenName = "Reduce";

        private static string _backupTime;
        private static string _backupFullyQualifiedAnimationName;
        private static List<ICurveAnimationTarget> _backup;
        private static readonly JSONStorableFloat _reduceMaxFramesPerSecondJSON;
        private static readonly JSONStorableBool _averageToSnapJSON;
        private static readonly JSONStorableBool _removeFlatSectionsKeyframes;
        private static readonly JSONStorableBool _simplifyKeyframes;
        private static readonly JSONStorableFloat _reduceMinDistanceJSON;
        private static readonly JSONStorableFloat _reduceMinRotationJSON;
        private static readonly JSONStorableFloat _reduceMinFloatParamRangeRatioJSON;

        public override string screenId => ScreenName;

        private UIDynamicButton _backupUI;
        private UIDynamicButton _restoreUI;
        private UIDynamicButton _reduceUI;

        static ReduceScreen()
        {
            _reduceMaxFramesPerSecondJSON = new JSONStorableFloat("Frames per second", 25f, 1f, 100f);
            _reduceMaxFramesPerSecondJSON.setCallbackFunction = val => _reduceMaxFramesPerSecondJSON.valNoCallback = Mathf.Round(val);
            _averageToSnapJSON = new JSONStorableBool("Average and snap to fps", true);
            _removeFlatSectionsKeyframes = new JSONStorableBool("Remove flat sections", true);
            _simplifyKeyframes = new JSONStorableBool("Simplify keyframes", true);
            _reduceMinDistanceJSON = new JSONStorableFloat("Minimum meaningful distance", 0.1f, 0f, 1f, false);
            _reduceMinRotationJSON = new JSONStorableFloat("Minimum meaningful rotation (dot)", 0.001f, 0f, 1f);
            _reduceMinFloatParamRangeRatioJSON = new JSONStorableFloat("Minimum meaningful float range ratio", 0.01f, 0f, 1f);
        }

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
            prefabFactory.CreateSlider(_reduceMaxFramesPerSecondJSON).valueFormat = "F1";
            prefabFactory.CreateToggle(_averageToSnapJSON);
            prefabFactory.CreateToggle(_removeFlatSectionsKeyframes);
            prefabFactory.CreateToggle(_simplifyKeyframes);
            prefabFactory.CreateSlider(_reduceMinDistanceJSON).valueFormat = "F3";
            prefabFactory.CreateSlider(_reduceMinRotationJSON).valueFormat = "F4";
            prefabFactory.CreateSlider(_reduceMinFloatParamRangeRatioJSON).valueFormat = "F3";

            _reduceUI = prefabFactory.CreateButton("Reduce");
            _reduceUI.button.onClick.AddListener(Reduce);

            _restoreUI.button.interactable = HasBackup();
            if (HasBackup()) _restoreUI.label = $"Restore [{_backupTime}]";
        }

        protected override void OnCurrentAnimationChanged(AtomAnimationEditContext.CurrentAnimationChangedEventArgs args)
        {
            base.OnCurrentAnimationChanged(args);
            _restoreUI.button.interactable = false;
        }

        private bool HasBackup()
        {
            return _backup != null && _backupFullyQualifiedAnimationName == current.animationNameQualified;
        }

        private void TakeBackup()
        {
            _backup = null;
            _backupFullyQualifiedAnimationName = current.animationNameQualified;
            _backup = animationEditContext.GetAllOrSelectedTargets().OfType<ICurveAnimationTarget>().Select(t => t.Clone(true)).ToList();
            _backupTime = DateTime.Now.ToShortTimeString();
            _restoreUI.label = $"Restore [{_backupTime}]";
            _restoreUI.button.interactable = true;
        }

        private void RestoreBackup()
        {
            if (!HasBackup()) return;
            var targets = animationEditContext.GetAllOrSelectedTargets().OfType<ICurveAnimationTarget>().ToList();
            foreach (var backup in _backup)
            {
                var target = targets.FirstOrDefault(t => t.TargetsSameAs(backup));
                target?.RestoreFrom(backup);
            }
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
            var targets = animationEditContext.GetAllOrSelectedTargets().OfType<ICurveAnimationTarget>().ToList();
            StartCoroutine(operations.Reduce(settings).ReduceKeyframes(
                targets,
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
    }
}

