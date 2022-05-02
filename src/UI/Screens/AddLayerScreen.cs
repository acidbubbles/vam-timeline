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

            InitNewClipNameUI();
            InitNewLayerNameUI();
            InitCreateLayerUI();
            InitSplitLayerUI();

            prefabFactory.CreateSpacer();
            prefabFactory.CreateHeader("Options", 2);

            #warning Option to create all clips with same settings on all layers
            InitCreateInOtherAtomsUI();

            RefreshUI();
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
            var clip = operations.Layers().Add(clipNameJSON.val, layerNameJSON.val);

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

            operations.Layers().SplitLayer(targets, layerNameJSON.val);
        }

        #endregion

        protected override void RefreshUI()
        {
            base.RefreshUI();

            clipNameJSON.valNoCallback = current.animationName;
            layerNameJSON.valNoCallback = animation.GetNewLayerName(current, current.animationLayer == "Main" ? "Layer 1" : null);
        }
    }
}
