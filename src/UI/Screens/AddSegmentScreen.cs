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

            InitCreateSegmentUI();

            prefabFactory.CreateSpacer();
            prefabFactory.CreateHeader("Options", 2);

            InitCreateInOtherAtomsUI();

            prefabFactory.CreateSpacer();
            prefabFactory.CreateHeader("More", 2);

            CreateChangeScreenButton("<i>Create <b>shared segment</b>...</i>", AddSharedSegmentScreen.ScreenName);
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
            var clip = operations.Segments().Add();

            animationEditContext.SelectAnimation(clip);
            ChangeScreen(EditAnimationScreen.ScreenName);
            if(createInOtherAtoms.val) plugin.peers.SendSyncAnimation(clip);
        }

        #endregion
    }
}
