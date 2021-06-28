using System.Collections;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
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
            recordInJSON.setCallbackFunction = val =>
            {
                recordInJSON.valNoCallback = Mathf.Round(val);
                _recordButton.label = $"Start recording in {recordInJSON.valNoCallback:0}...";
            };
            prefabFactory.CreateSlider(recordInJSON);

            prefabFactory.CreateSpacer();
            prefabFactory.CreateHeader("Record", 1);
            prefabFactory.CreateHeader("Note: Select targets to record", 2);
            _recordButton = prefabFactory.CreateButton($"Start recording in {recordInJSON.valNoCallback:0}...");
            _recordButton.button.onClick.AddListener(() => plugin.StartCoroutine(OnRecordCo()));

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

        private IEnumerator OnRecordCo()
        {
            // TODO: This enumerator should be registered as a "current operation" in AtomAnimationEditContext
            var targets = animationEditContext.GetSelectedTargets().ToList();
            var enumerator = operations.Record().StartRecording(
                targets.Count > 0 ? targets.OfType<FreeControllerV3AnimationTarget>().ToList() : current.targetControllers,
                targets.Count > 0 ? targets.OfType<JSONStorableFloatAnimationTarget>().ToList() : current.targetFloatParams
            );

            ChangeScreen(TargetsScreen.ScreenName);

            while (enumerator.MoveNext())
                yield return enumerator.Current;
        }
    }
}

