using System.Linq;

namespace VamTimeline
{
    public class AddSegmentScreen : AddScreenBase
    {
        public const string ScreenName = "Add segment";

        public override string screenId => ScreenName;

        private UIDynamicButton _createSegmentUI;
        private UIDynamicButton _copySegmentUI;

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

            prefabFactory.CreateSpacer();
            prefabFactory.CreateHeader("More", 2);

            CreateChangeScreenButton("<i>Create <b>shared segment</b>...</i>", AddSharedSegmentScreen.ScreenName);

            RefreshUI();
        }

        public void InitCreateSegmentUI()
        {
            _createSegmentUI = prefabFactory.CreateButton("Create new segment");
            _createSegmentUI.button.onClick.AddListener(AddSegment);

            _copySegmentUI = prefabFactory.CreateButton("Copy to new segment");
            _copySegmentUI.button.onClick.AddListener(CopySegment);
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

        private void CopySegment()
        {
            foreach (var source in currentSegment.layers.SelectMany(l => l))
            {
                var clip = operations.AddAnimation().AddAnimationAsCopy(source, null, animation.clips.Count, segmentNameJSON.val);
                if(createInOtherAtoms.val) plugin.peers.SendSyncAnimation(clip);
            }

            animationEditContext.SelectAnimation(animation.index.segments[segmentNameJSON.val].layers[0][0]);
            ChangeScreen(EditAnimationScreen.ScreenName);
        }

        #endregion

        protected override void RefreshUI()
        {
            base.RefreshUI();

            clipNameJSON.val = animation.GetUniqueAnimationName(current);
            layerNameJSON.val = AtomAnimationClip.DefaultAnimationLayer;
            segmentNameJSON.val = animation.GetUniqueSegmentName(current);
        }

        protected override void OptionsUpdated()
        {
            var isValid =
                !string.IsNullOrEmpty(clipNameJSON.val) &&
                animation.clips.All(c => c.animationName != clipNameJSON.val) &&
                !string.IsNullOrEmpty(layerNameJSON.val) &&
                !string.IsNullOrEmpty(segmentNameJSON.val) &&
                !animation.index.segmentNames.Contains(segmentNameJSON.val);

            _createSegmentUI.button.interactable = isValid;
            _copySegmentUI.button.interactable = isValid;
        }
    }
}
