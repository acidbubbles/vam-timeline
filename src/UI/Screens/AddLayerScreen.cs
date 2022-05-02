namespace VamTimeline
{
    public class AddLayerScreen : AddScreenBase
    {
        public const string ScreenName = "Add layer";

        public override string screenId => ScreenName;

        #region Init

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            prefabFactory.CreateSpacer();
            prefabFactory.CreateHeader("Create", 1);

            InitCreateLayerUI();
            InitSplitLayerUI();

            prefabFactory.CreateSpacer();
            prefabFactory.CreateHeader("Options", 2);

            InitCreateInOtherAtomsUI();
        }

        public void InitCreateLayerUI()
        {
            var createLayerUI = prefabFactory.CreateButton("Create new layer");
            createLayerUI.button.onClick.AddListener(AddLayer);
        }

        private void InitSplitLayerUI()
        {
            var splitLayerUI = prefabFactory.CreateButton("Split selection to new layer");
            splitLayerUI.button.onClick.AddListener(SplitLayer);
        }

        #endregion

        #region Callbacks

        private void AddLayer()
        {
            var clip = operations.Layers().Add();

            animationEditContext.SelectAnimation(clip);
            ChangeScreen(EditAnimationScreen.ScreenName);
            if(createInOtherAtoms.val) plugin.peers.SendSyncAnimation(clip);
        }

        private void SplitLayer()
        {
            var targets = animationEditContext.GetSelectedTargets().ToList();
            if (targets.Count == 0)
            {
                SuperController.LogError("Timeline: You must select a subset of targets to split to another layer.");
                return;
            }

            operations.Layers().SplitLayer(targets);
        }

        #endregion
    }
}
