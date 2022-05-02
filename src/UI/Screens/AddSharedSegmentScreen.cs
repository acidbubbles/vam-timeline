using System.Linq;

namespace VamTimeline
{
    public class AddSharedSegmentScreen : AddScreenBase
    {
        public const string ScreenName = "Add shared segment";

        public override string screenId => ScreenName;

        #region Init

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            prefabFactory.CreateSpacer();
            prefabFactory.CreateHeader("Create", 1);

            InitNewClipNameUI();
            InitNewLayerNameUI();
            InitCreateSharedSegmentUI();

            RefreshUI();
        }

        public void InitCreateSharedSegmentUI()
        {
            var createSharedSegmentUI = prefabFactory.CreateButton("Create shared segment");
            createSharedSegmentUI.button.onClick.AddListener(AddSharedSegment);
        }

        #endregion

        #region Callbacks

        private void AddSharedSegment()
        {
            if (animation.index.segmentNames.Contains(AtomAnimationClip.SharedAnimationSegment))
            {
                animationEditContext.SelectAnimation(animation.clips.First(c => c.animationSegment == AtomAnimationClip.SharedAnimationSegment));
                return;
            }

            var clip = operations.Segments().AddShared(clipNameJSON.val);
            animationEditContext.SelectAnimation(clip);
            ChangeScreen(EditAnimationScreen.ScreenName);
        }

        #endregion

        protected override void RefreshUI()
        {
            base.RefreshUI();

            clipNameJSON.valNoCallback = operations.Segments().GetNewAnimationName("Shared");
        }
    }
}
