using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class RecordScreen : ScreenBase
    {
        public const string ScreenName = "Record";

        private static UIDynamicButton _recordButton;
        private static string _previousUseCameraRaycast;
        private static bool _previousHideMenuDuringRecording;

        private JSONStorableStringChooser _useCameraRaycast;
        private JSONStorableBool _recordExtendsLength;
        private JSONStorableBool _hideMenuDuringRecording;

        public override string screenId => ScreenName;

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            CreateChangeScreenButton("<b><</b> <i>Back</i>", MoreScreen.ScreenName);

            prefabFactory.CreateSpacer();

            prefabFactory.CreateHeader("Record options", 1);

            _recordExtendsLength = new JSONStorableBool("Record extends length", false);
            prefabFactory.CreateToggle(_recordExtendsLength);

            var recordTimeModeJSON = new JSONStorableStringChooser(
                "Time mode",
                new List<string> { TimeModes.RealTime.ToString(), TimeModes.UnityTime.ToString() },
                TimeModes.RealTime.ToString(),
                "Time mode"
            )
            {
                displayChoices = new List<string> { "Real time (better timing)", "Game time (better sync with anim. patterns)" }
            };
            prefabFactory.CreatePopup(recordTimeModeJSON, false, false);

            var recordInJSON = new JSONStorableFloat("Record delay timer", 5f, 0f, 30f, false)
            {
                valNoCallback = animationEditContext.startRecordIn
            };
            recordInJSON.setCallbackFunction = val =>
            {
                animationEditContext.startRecordIn = (int) Mathf.Round(val);
                recordInJSON.valNoCallback = animationEditContext.startRecordIn;
                _recordButton.label = $"Start recording in {animationEditContext.startRecordIn}...";
            };
            prefabFactory.CreateSlider(recordInJSON);

            _hideMenuDuringRecording = new JSONStorableBool("Hide menu during recording", false)
            {
                valNoCallback = _previousHideMenuDuringRecording,
                setCallbackFunction = val => _previousHideMenuDuringRecording = val
            };
            prefabFactory.CreateToggle(_hideMenuDuringRecording);

            FreeControllerV3AnimationTarget raycastTarget = null;
            _useCameraRaycast = new JSONStorableStringChooser("Use camera raycast on", new List<string>(), "", "Use camera raycast on")
            {
                valNoCallback = _previousUseCameraRaycast
            };
            _useCameraRaycast.setCallbackFunction = val =>
            {
                _previousUseCameraRaycast = val;
                if (string.IsNullOrEmpty(val))
                {
                    raycastTarget = null;
                    return;
                }
                raycastTarget = current.targetControllers.FirstOrDefault(t => t.animatableRef.name == val);
                if (raycastTarget == null)
                {
                    SuperController.LogError($"Timeline: Target '{val}' was not in the list of targets");
                    _useCameraRaycast.valNoCallback = "";
                    return;
                }
                raycastTarget.animatableRef.selected = true;
            };
            prefabFactory.CreatePopup(_useCameraRaycast, true, false);

            prefabFactory.CreateSpacer();
            prefabFactory.CreateHeader("Record", 1);
            prefabFactory.CreateHeader("Note: Select targets to record", 2);
            _recordButton = prefabFactory.CreateButton($"Start recording in {animationEditContext.startRecordIn}...");
            _recordButton.button.onClick.AddListener(() => SuperController.singleton.StartCoroutine(OnRecordCo(int.Parse(recordTimeModeJSON.val), _recordExtendsLength.val, raycastTarget)));

            prefabFactory.CreateSpacer();

            CreateChangeScreenButton("<i>Go to <b>reduce</b> screen...</i>", ReduceScreen.ScreenName);

            animationEditContext.animation.animatables.onTargetsSelectionChanged.AddListener(OnTargetsSelectionChanged);
            SyncAnimationFields();
            SyncRaycastTargets();
        }

        private void OnTargetsSelectionChanged()
        {
            SyncAnimationFields();
        }

        protected override void OnCurrentAnimationChanged(AtomAnimationEditContext.CurrentAnimationChangedEventArgs args)
        {
            base.OnCurrentAnimationChanged(args);
            SyncAnimationFields();
            SyncRaycastTargets();
        }

        private void SyncRaycastTargets()
        {
            _useCameraRaycast.choices = new[] {""}.Concat(current.targetControllers.Select(t => t.name)).ToList();
            _useCameraRaycast.valNoCallback = _useCameraRaycast.choices.Contains(_previousUseCameraRaycast) ? _previousUseCameraRaycast : "";
        }

        private void SyncAnimationFields()
        {
            _recordButton.button.interactable = !current.recording && animationEditContext.GetSelectedTargets().Any();
            _recordExtendsLength.valNoCallback = current.GetAllCurveTargets().All(t => t.GetLeadCurve().length == 2);
        }

        public override void OnDestroy()
        {
            animationEditContext.animation.animatables.onTargetsSelectionChanged.RemoveListener(OnTargetsSelectionChanged);
            base.OnDestroy();
        }

        private IEnumerator OnRecordCo(int timeMode, bool recordExtendsLength, FreeControllerV3AnimationTarget raycastTarget)
        {
            if (raycastTarget != null && !raycastTarget.selected)
            {
                SuperController.LogError($"Cannot record raycast target {raycastTarget.name} because it is not selected. Either select it or remove it from the raycast target list.");
                yield break;
            }

            _recordButton.button.interactable = false;

            var hideMenuDuringRecording = _hideMenuDuringRecording.val;
            if (hideMenuDuringRecording)
            {
                SuperController.singleton.HideMainHUD();
            }

            // TODO: This enumerator should be registered as a "current operation" in AtomAnimationEditContext
            var targets = animationEditContext.GetSelectedTargets().OfType<ICurveAnimationTarget>().ToList();

            var enumerator = operations.Record().StartRecording(
                timeMode,
                recordExtendsLength,
                animationEditContext.startRecordIn,
                targets.ToList(),
                raycastTarget,
                hideMenuDuringRecording
            );

            if (!targets.OfType<FreeControllerV3AnimationTarget>().Any() && targets.OfType<JSONStorableFloatAnimationTarget>().Any())
                ChangeScreen(TargetsScreen.ScreenName);

            while (enumerator.MoveNext())
                yield return enumerator.Current;

            if (_recordButton != null)
                _recordButton.button.interactable = true;
            animationEditContext.Stop();

            // This is a hack, not sure why it's necessary to update the keyframes
            yield return 0;
            current.DirtyAll();
            yield return 0;
            animationEditContext.ResetScrubberRange();
            animationEditContext.clipTime = 0f;
            if (hideMenuDuringRecording)
                SuperController.singleton.ShowMainHUDAuto();
        }
    }
}

