using System.Linq;

namespace VamTimeline
{
    public class AddLayerScreen : AddScreenBase
    {
        public const string ScreenName = "Add layer";

        public override string screenId => ScreenName;

        private UIDynamicButton _createLayerUI;
        private UIDynamicButton _splitLayerUI;

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

            #warning Merge layers (if all clips match)

            RefreshUI();
        }

        public void InitCreateLayerUI()
        {
            _createLayerUI = prefabFactory.CreateButton("Create new layer");
            _createLayerUI.button.onClick.AddListener(AddLayer);
        }

        private void InitSplitLayerUI()
        {
            _splitLayerUI = prefabFactory.CreateButton("Split selection to new layer");
            _splitLayerUI.button.onClick.AddListener(SplitLayer);
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

            clipNameJSON.val = current.animationName;
            layerNameJSON.val = animation.GetUniqueLayerName(current, current.animationLayer == "Main" ? "Layer 1" : null);
        }

        protected override void OptionsUpdated()
        {
            var nameValid =
                !string.IsNullOrEmpty(clipNameJSON.val) &&
                animation.index.segments
                    .Where(s => s.Key != current.animationSegment)
                    .SelectMany(s => s.Value.layers)
                    .SelectMany(l => l)
                    .All(c => c.animationName != clipNameJSON.val) &&
                !string.IsNullOrEmpty(layerNameJSON.val) &&
                !currentSegment.layerNames.Contains(layerNameJSON.val);

            _createLayerUI.button.interactable = nameValid;
            _splitLayerUI.button.interactable = nameValid;
        }
    }
}
