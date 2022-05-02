namespace VamTimeline
{
    public class AddSegmentScreen : AddScreenBase
    {
        public const string ScreenName = "Add segment";

        public override string screenId => ScreenName;

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
            var createSegmentUI = prefabFactory.CreateButton("Create new segment");
            createSegmentUI.button.onClick.AddListener(AddSegment);
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

            clipNameJSON.valNoCallback = animation.GetNewAnimationName(current);
            layerNameJSON.valNoCallback = AtomAnimationClip.DefaultAnimationLayer;
            segmentNameJSON.valNoCallback = animation.GetNewSegmentName(current);
        }
    }
}
