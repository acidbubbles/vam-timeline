using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class RecordScreen : ScreenBase
    {
        private UIDynamicButton _recordButton;
        private JSONStorableStringChooser _useCameraRaycast;
        public const string ScreenName = "Record";

        public override string screenId => ScreenName;

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            CreateChangeScreenButton("<b><</b> <i>Back</i>", MoreScreen.ScreenName);

            prefabFactory.CreateSpacer();

            prefabFactory.CreateHeader("Record options", 1);
            var recordInJSON = new JSONStorableFloat("Record delay timer", 5f, 0f, 30f, false);
            recordInJSON.valNoCallback = animationEditContext.startRecordIn;
            recordInJSON.setCallbackFunction = val =>
            {
                animationEditContext.startRecordIn = (int) Mathf.Round(val);
                recordInJSON.valNoCallback = animationEditContext.startRecordIn;
                _recordButton.label = $"Start recording in {animationEditContext.startRecordIn}...";
            };
            prefabFactory.CreateSlider(recordInJSON);

            FreeControllerV3AnimationTarget raycastTarget = null;
            _useCameraRaycast = new JSONStorableStringChooser("Use camera raycast", new List<string>(), "", "Use camera raycast");
            _useCameraRaycast.setCallbackFunction = val =>
            {
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
            _recordButton.button.onClick.AddListener(() => plugin.StartCoroutine(OnRecordCo(raycastTarget)));

            prefabFactory.CreateSpacer();

            CreateChangeScreenButton("<i>Go to <b>reduce</b> screen...</i>", ReduceScreen.ScreenName);

            animationEditContext.animation.animatables.onTargetsSelectionChanged.AddListener(OnTargetsSelectionChanged);
            OnTargetsSelectionChanged();
        }

        private void OnTargetsSelectionChanged()
        {
            _recordButton.button.interactable = animationEditContext.GetSelectedTargets().Any();
        }

        public override void OnDestroy()
        {
            animationEditContext.animation.animatables.onTargetsSelectionChanged.RemoveListener(OnTargetsSelectionChanged);
            base.OnDestroy();
        }

        private IEnumerator OnRecordCo(FreeControllerV3AnimationTarget raycastTarget)
        {
            if (!raycastTarget.selected)
            {
                SuperController.LogError($"Cannot record raycast target {raycastTarget.name} because it is not selected. Either select it or remove it from the raycast target list.");
                yield break;
            }

            // TODO: This enumerator should be registered as a "current operation" in AtomAnimationEditContext
            var targets = animationEditContext.GetSelectedTargets().ToList();
            var targetControllers = targets.Count > 0 ? targets.OfType<FreeControllerV3AnimationTarget>().ToList() : current.targetControllers;
            var targetFloatParams = targets.Count > 0 ? targets.OfType<JSONStorableFloatAnimationTarget>().ToList() : current.targetFloatParams;

            var enumerator = operations.Record().StartRecording(
                animationEditContext.startRecordIn,
                targetControllers,
                targetFloatParams,
                raycastTarget
            );

            ChangeScreen(TargetsScreen.ScreenName);

            while (enumerator.MoveNext())
                yield return enumerator.Current;
        }
    }
}

