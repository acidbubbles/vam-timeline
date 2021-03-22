using System.Collections;
using System.Linq;

namespace VamTimeline
{
    public class RecordScreen : ScreenBase
    {
        public const string ScreenName = "Record";

        public override string screenId => ScreenName;


        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            CreateChangeScreenButton("<b><</b> <i>Back</i>", MoreScreen.ScreenName);

            prefabFactory.CreateButton("Start recording in 1, 2, 3...").button.onClick.AddListener(() => plugin.StartCoroutine(OnRecordCo()));
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

