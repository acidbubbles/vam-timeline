using System.Collections.Generic;
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
            InitSendLayerToSharedSegmentUI();

            RefreshUI();
        }

        public void InitCreateSharedSegmentUI()
        {
            _createSharedSegmentUI = prefabFactory.CreateButton("Create shared segment");
            _createSharedSegmentUI.button.onClick.AddListener(AddSharedSegment);
        }

        public void InitSendLayerToSharedSegmentUI()
        {
            var sendLayerToSharedUI = prefabFactory.CreateButton("Send layer to shared segment");
            sendLayerToSharedUI.button.onClick.AddListener(SendLayerToShared);
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

        public void SendLayerToShared()
        {
            if (current.animationSegment == AtomAnimationClip.SharedAnimationSegment)
                return;

            var currentTargets = new HashSet<IAtomAnimationTarget>(currentLayer.SelectMany(c => c.GetAllTargets()));
            var reservedTargets = new HashSet<IAtomAnimationTarget>(animation.clips.Where(c => c.animationSegment != current.animationSegment).SelectMany(c => c.GetAllTargets()));
            if (currentTargets.Any(t => reservedTargets.Any(t.TargetsSameAs)))
            {
                SuperController.LogError("Timeline: Cannot send current layer to the shared segment because some targets exists in the shared segment already or in another segment.");
                return;
            }

            foreach (var clip in currentLayer)
            {
                clip.animationSegment = AtomAnimationClip.SharedAnimationSegment;
                clip.animationLayer = layerNameJSON.val;
            }
        }

        #endregion

        protected override void RefreshUI()
        {
            base.RefreshUI();

            clipNameJSON.val = animation.GetUniqueAnimationName(AtomAnimationClip.SharedAnimationSegmentId, "Shared 1");
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
