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
        private static JSONStorableStringChooser _useCameraRaycast;
        private static JSONStorableBool _recordExtendsLength;

        public override string screenId => ScreenName;

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            CreateChangeScreenButton("<b><</b> <i>Back</i>", MoreScreen.ScreenName);

            prefabFactory.CreateSpacer();

            prefabFactory.CreateHeader("Record options", 1);

            _recordExtendsLength = new JSONStorableBool("Record extends length", false);
            prefabFactory.CreateToggle(_recordExtendsLength);

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

            FreeControllerV3AnimationTarget raycastTarget = null;
            _useCameraRaycast = new JSONStorableStringChooser("Use camera raycast on", new List<string>(), "", "Use camera raycast on");
            _useCameraRaycast.setCallbackFunction = val =>
            {
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
            _recordButton.button.onClick.AddListener(() => SuperController.singleton.StartCoroutine(OnRecordCo(_recordExtendsLength.val, raycastTarget)));

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
            _useCameraRaycast.valNoCallback = "";
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

        private IEnumerator OnRecordCo(bool recordExtendsLength, FreeControllerV3AnimationTarget raycastTarget)
        {
            if (raycastTarget != null && !raycastTarget.selected)
            {
                SuperController.LogError($"Cannot record raycast target {raycastTarget.name} because it is not selected. Either select it or remove it from the raycast target list.");
                yield break;
            }

            _recordButton.button.interactable = false;

            // TODO: This enumerator should be registered as a "current operation" in AtomAnimationEditContext
            var targets = animationEditContext.GetSelectedTargets().ToList();
            var targetControllers = targets.Count > 0 ? targets.OfType<FreeControllerV3AnimationTarget>().ToList() : current.targetControllers;
            var targetFloatParams = targets.Count > 0 ? targets.OfType<JSONStorableFloatAnimationTarget>().ToList() : current.targetFloatParams;

            var enumerator = operations.Record().StartRecording(
                recordExtendsLength,
                animationEditContext.startRecordIn,
                targetControllers.Cast<ICurveAnimationTarget>().Concat(targetFloatParams).ToList(),
                raycastTarget
            );

            if (targetControllers.Count == 0 && targetFloatParams.Count > 0)
                ChangeScreen(TargetsScreen.ScreenName);

            while (enumerator.MoveNext())
                yield return enumerator.Current;

            _recordButton.button.interactable = true;
            animationEditContext.Stop();
            animationEditContext.ResetScrubberRange();
            animationEditContext.clipTime = 0f;

            // This is a hack, not sure why it's necessary to update the keyframes
            yield return 0;
            current.DirtyAll();
        }
    }
}

