using System.Collections;
using System.Linq;

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

            _recordButton = prefabFactory.CreateButton("Start recording in 1, 2, 3...");
            _recordButton.button.onClick.AddListener(() => plugin.StartCoroutine(OnRecordCo()));

            animationEditContext.onTargetsSelectionChanged.AddListener(OnTargetsSelectionChanged);
            OnTargetsSelectionChanged();
        }

        private void OnTargetsSelectionChanged()
        {
            _recordButton.button.interactable = animationEditContext.GetSelectedTargets().Any();
        }

        public override void OnDestroy()
        {
            animationEditContext.onTargetsSelectionChanged.RemoveListener(OnTargetsSelectionChanged);
            base.OnDestroy();
        }

        private IEnumerator OnRecordCo()
        {
            // TODO: This enumerator should be registered as a "current operation" in AtomAnimationEditContext
            var targets = animationEditContext.GetSelectedTargets().ToList();
            var enumerator = operations.Record().StartRecording(
                targets.Count > 0 ? targets.OfType<FreeControllerAnimationTarget>().ToList() : current.targetControllers,
                targets.Count > 0 ? targets.OfType<FloatParamAnimationTarget>().ToList() : current.targetFloatParams
            );

            ChangeScreen(TargetsScreen.ScreenName);

            while (enumerator.MoveNext())
                yield return enumerator.Current;
        }
    }
}

