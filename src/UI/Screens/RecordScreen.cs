using System.Collections;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class RecordScreen : ScreenBase
    {
        private UIDynamicButton _recordButton;
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

            var recordEyeTargetControl = new JSONStorableBool("Use camera for eyeTarget", false);
            if (plugin.containingAtom.type == "Person")
            {
                recordEyeTargetControl.setCallbackFunction = (bool val) =>
                {
                    var eyeTargetControlTarget = current.targetControllers.FirstOrDefault(t => t.animatableRef.controller.name == "eyeTargetControl");
                    if (eyeTargetControlTarget == null)
                    {
                        SuperController.LogError("Timeline: To use eyeTargetControl using camera, you must add eyeTargetControl to the list");
                        recordEyeTargetControl.valNoCallback = false;
                        return;
                    }
                    eyeTargetControlTarget.animatableRef.selected = true;
                };
                prefabFactory.CreateToggle(recordEyeTargetControl);
            }

            prefabFactory.CreateSpacer();
            prefabFactory.CreateHeader("Record", 1);
            prefabFactory.CreateHeader("Note: Select targets to record", 2);
            _recordButton = prefabFactory.CreateButton($"Start recording in {animationEditContext.startRecordIn}...");
            _recordButton.button.onClick.AddListener(() => plugin.StartCoroutine(OnRecordCo(recordEyeTargetControl.val)));

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

        private IEnumerator OnRecordCo(bool recordEyeTargetControl)
        {
            // TODO: This enumerator should be registered as a "current operation" in AtomAnimationEditContext
            var targets = animationEditContext.GetSelectedTargets().ToList();
            var targetControllers = targets.Count > 0 ? targets.OfType<FreeControllerV3AnimationTarget>().ToList() : current.targetControllers;
            var targetFloatParams = targets.Count > 0 ? targets.OfType<JSONStorableFloatAnimationTarget>().ToList() : current.targetFloatParams;

            FreeControllerV3AnimationTarget eyeTargetControlTarget = null;
            if (recordEyeTargetControl)
            {
                eyeTargetControlTarget = targetControllers.FirstOrDefault(t => t.animatableRef.controller.name == "eyeTargetControl");
                if (eyeTargetControlTarget == null)
                {
                    SuperController.LogError("Timeline: To use eyeTargetControl using camera, you must add eyeTargetControl to the list");
                    yield break;
                }
            }

            var enumerator = operations.Record().StartRecording(
                animationEditContext.startRecordIn,
                targetControllers,
                targetFloatParams,
                eyeTargetControlTarget
            );

            ChangeScreen(TargetsScreen.ScreenName);

            while (enumerator.MoveNext())
                yield return enumerator.Current;
        }
    }
}

