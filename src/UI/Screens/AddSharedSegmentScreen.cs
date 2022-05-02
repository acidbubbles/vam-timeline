using System.Linq;

namespace VamTimeline
{
    public class AddSharedSegmentScreen : AddScreenBase
    {
        public const string ScreenName = "Add shared segment";

        public override string screenId => ScreenName;

        private UIDynamicButton _createSharedSegmentUI;

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
            _createSharedSegmentUI = prefabFactory.CreateButton("Create shared segment");
            _createSharedSegmentUI.button.onClick.AddListener(AddSharedSegment);
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

            clipNameJSON.val = animation.GetUniqueAnimationName("Shared 1");
            layerNameJSON.val = AtomAnimationClip.DefaultAnimationLayer;
        }

        protected override void OptionsUpdated()
        {
            _createSharedSegmentUI.button.interactable =
                !animation.index.segmentNames.Contains(AtomAnimationClip.SharedAnimationSegment) &&
                !string.IsNullOrEmpty(clipNameJSON.val) &&
                animation.clips.All(c => c.animationName != clipNameJSON.val) &&
                !string.IsNullOrEmpty(layerNameJSON.val);
        }
    }
}
