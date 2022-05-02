using System.Linq;

namespace VamTimeline
{
    public class AddSegmentScreen : AddScreenBase
    {
        public const string ScreenName = "Add segment";

        public override string screenId => ScreenName;

        private UIDynamicButton _createSegmentUI;

        #region Init

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            prefabFactory.CreateSpacer();
            prefabFactory.CreateHeader("Create", 1);

            InitNewClipNameUI();
            InitNewLayerNameUI();
            InitNewSegmentNameUI();
            InitCreateSegmentUI();

            prefabFactory.CreateSpacer();
            prefabFactory.CreateHeader("Options", 2);

            InitCreateInOtherAtomsUI();
            #warning Option to copy all layers

            prefabFactory.CreateSpacer();
            prefabFactory.CreateHeader("More", 2);

            CreateChangeScreenButton("<i>Create <b>shared segment</b>...</i>", AddSharedSegmentScreen.ScreenName);

            RefreshUI();
        }

        public void InitCreateSegmentUI()
        {
            _createSegmentUI = prefabFactory.CreateButton("Create new segment");
            _createSegmentUI.button.onClick.AddListener(AddSegment);
        }

        #endregion

        #region Callbacks

        private void AddSegment()
        {
            var clip = operations.Segments().Add(clipNameJSON.val, layerNameJSON.val, segmentNameJSON.val);

            animationEditContext.SelectAnimation(clip);
            ChangeScreen(EditAnimationScreen.ScreenName);
            if(createInOtherAtoms.val) plugin.peers.SendSyncAnimation(clip);
        }

        #endregion

        protected override void RefreshUI()
        {
            base.RefreshUI();

            clipNameJSON.val = animation.GetNewAnimationName(current);
            layerNameJSON.val = AtomAnimationClip.DefaultAnimationLayer;
            segmentNameJSON.val = animation.GetNewSegmentName(current);
        }

        protected override void OptionsUpdated()
        {
            _createSegmentUI.button.interactable =
                !string.IsNullOrEmpty(clipNameJSON.val) &&
                animation.clips.All(c => c.animationName != clipNameJSON.val) &&
                !string.IsNullOrEmpty(layerNameJSON.val) &&
                !string.IsNullOrEmpty(segmentNameJSON.val) &&
                !animation.index.segmentNames.Contains(segmentNameJSON.val);
        }
    }
}
