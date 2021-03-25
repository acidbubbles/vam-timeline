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
        private static List<ICurveAnimationTarget> _backup;

        public override string screenId => ScreenName;

        private UIDynamicButton _backupUI;
        private UIDynamicButton _restoreUI;
        private UIDynamicButton _reduceUI;
        private JSONStorableFloat _reduceMinPosDistanceJSON;
        private JSONStorableFloat _reduceMinRotationJSON;
        private JSONStorableFloat _reduceMaxFramesPerSecondJSON;
        private float _lastReduceMinPosDistance;
        private float _lastReduceMinRotation;
        private float _lastReduceMaxFramesPerSecond;

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

            _reduceMinPosDistanceJSON = new JSONStorableFloat("Minimum distance between frames", 0.04f, val => _lastReduceMinPosDistance = val, 0.001f, 0.5f)
            {
                valNoCallback = _lastReduceMinPosDistance
            };
            prefabFactory.CreateSlider(_reduceMinPosDistanceJSON);

            _reduceMinRotationJSON = new JSONStorableFloat("Minimum rotation between frames", 10f, val => _lastReduceMinRotation = val, 0.1f, 90f)
            {
                valNoCallback = _lastReduceMinRotation
            };
            prefabFactory.CreateSlider(_reduceMinRotationJSON);

            _reduceMaxFramesPerSecondJSON = new JSONStorableFloat("Max frames per second", 5f, val => _reduceMaxFramesPerSecondJSON.valNoCallback = _lastReduceMaxFramesPerSecond = Mathf.Round(val), 1f, 10f)
            {
                valNoCallback = _lastReduceMaxFramesPerSecond
            };
            prefabFactory.CreateSlider(_reduceMaxFramesPerSecondJSON);

            _reduceUI = prefabFactory.CreateButton("Reduce");
            _reduceUI.button.onClick.AddListener(Reduce);

            animationEditContext.onTargetsSelectionChanged.AddListener(OnTargetsSelectionChanged);
            OnTargetsSelectionChanged();
            _restoreUI.button.interactable = _backup != null;
            if (_backup != null) _restoreUI.label = $"Restore [{_backupTime}]";
        }

        private void OnTargetsSelectionChanged()
        {
            var hasSelectedTargets = animationEditContext.GetSelectedTargets().Any();
            _backupUI.button.interactable = hasSelectedTargets;
            _reduceUI.button.interactable = hasSelectedTargets;
        }

        protected override void OnCurrentAnimationChanged(AtomAnimationEditContext.CurrentAnimationChangedEventArgs args)
        {
            base.OnCurrentAnimationChanged(args);
            _backup = null;
            _restoreUI.button.interactable = false;
        }

        public override void OnDestroy()
        {
            animationEditContext.onTargetsSelectionChanged.RemoveListener(OnTargetsSelectionChanged);
            base.OnDestroy();
        }

        private void TakeBackup()
        {
            _backup = animationEditContext.GetAllOrSelectedTargets().OfType<ICurveAnimationTarget>().Select(t => t.Clone(true)).ToList();
            _backupTime = DateTime.Now.ToShortTimeString();
            _restoreUI.label = $"Restore [{_backupTime}]";
            _restoreUI.button.interactable = true;
        }

        private void RestoreBackup()
        {
            if (_backup == null) return;
            var targets = animationEditContext.GetAllOrSelectedTargets().OfType<ICurveAnimationTarget>().ToList();
            foreach (var backup in _backup)
            {
                var target = targets.FirstOrDefault(t => t.TargetsSameAs(backup));
                target?.RestoreFrom(backup);
            }
        }

        private void Reduce()
        {
            if(_backup == null)
                TakeBackup();

            _reduceUI.button.interactable = false;
            _reduceUI.label = "Reducing...";
            StartCoroutine(operations.Reduce().ReduceKeyframes(
                animationEditContext.GetAllOrSelectedTargets().OfType<ICurveAnimationTarget>().ToList(),
            progress =>
                {
                    _reduceUI.label = $"{progress.stepsDone}/{progress.stepsTotal} ({progress.timeLeft:0}s left)";
                },
                () =>
                {
                    _reduceUI.button.interactable = true;
                    _reduceUI.label = "Reduce";
                }));
        }
    }
}

