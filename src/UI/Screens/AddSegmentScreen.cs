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

            if (!animation.index.useSegment)
            {
                prefabFactory.CreateSpacer();
                prefabFactory.CreateHeader("Segments", 1);

                InitUseSegments();
            }
            else
            {
                prefabFactory.CreateSpacer();
                prefabFactory.CreateHeader("Create", 1);

                #warning Add segment position

                InitNewClipNameUI();
                InitNewLayerNameUI();
                InitNewSegmentNameUI();
                InitCreateInOtherAtomsUI();
                InitCreateSegmentUI();

                prefabFactory.CreateSpacer();
                prefabFactory.CreateHeader("Advanced", 2);

                InitCopySegmentUI();
                InitCreateTransitionSegmentUI();

                prefabFactory.CreateSpacer();
                prefabFactory.CreateHeader("More", 2);

                CreateChangeScreenButton("<i>Create <b>shared segment</b>...</i>", AddSharedSegmentScreen.ScreenName);

                RefreshUI();
            }
        }

        public void InitUseSegments()
        {
            var useSegmentsUI = prefabFactory.CreateButton("Use segments");
            useSegmentsUI.button.onClick.AddListener(UseSegments);
        }

        public void InitCreateSegmentUI()
        {
            _createSegmentUI = prefabFactory.CreateButton("Create new segment");
            _createSegmentUI.button.onClick.AddListener(AddSegment);
        }

        public void InitCopySegmentUI()
        {
            _copySegmentUI = prefabFactory.CreateButton("Create copy of segment");
            _copySegmentUI.button.onClick.AddListener(CopySegment);
        }

        public void InitCreateTransitionSegmentUI()
        {
            _copySegmentUI = prefabFactory.CreateButton("Create transition segment");
            _copySegmentUI.button.onClick.AddListener(CreateTransitionSegment);
        }

        #endregion

        #region Callbacks

        private void UseSegments()
        {
            var animationSegment = animation.GetUniqueSegmentName("Segment 1");
            foreach (var clip in animation.index.segments[AtomAnimationClip.NoneAnimationSegment].layers.SelectMany(l => l))
            {
                clip.animationSegment = animationSegment;
            }
            animation.index.Rebuild();
            ReloadScreen();
        }

        private void AddSegment()
        {
            var clip = operations.Segments().Add(clipNameJSON.val, layerNameJSON.val, segmentNameJSON.val);

            animationEditContext.SelectAnimation(clip);
            ChangeScreen(EditAnimationScreen.ScreenName);
            if(createInOtherAtoms.val) plugin.peers.SendSyncAnimation(clip);
        }

        private void CopySegment()
        {

            var result = operations.AddAnimation().AddAnimation(animation.GetUniqueAnimationName(current), AddAnimationOperations.Positions.NotSpecified, true, true, true);
            foreach (var r in result)
            {
                r.created.animationSegment = segmentNameJSON.val;
                if (createInOtherAtoms.val) plugin.peers.SendSyncAnimation(r.created);
            }

            animationEditContext.SelectAnimation(result[0].created);
            ChangeScreen(EditAnimationScreen.ScreenName);
        }

        private void CreateTransitionSegment()
        {
            var clip = operations.Segments().Add(clipNameJSON.val, layerNameJSON.val, segmentNameJSON.val);
            clip.loop = false;

            foreach (var layer in currentSegment.layers.Select(l => l.Last()))
            {
                foreach (var target in layer.targetControllers)
                {
                    var added =clip.Add(target.animatableRef);
                    if (added != null)
                    {
                        var snapshot = target.GetCurveSnapshot(layer.animationLength);
                        added.SetCurveSnapshot(0f, snapshot);
                        added.SetCurveSnapshot(clip.animationLength, snapshot);
                    }
                }
                foreach (var target in layer.targetFloatParams)
                {
                    var added =clip.Add(target.animatableRef);
                    if (added != null)
                    {
                        var snapshot = target.GetCurveSnapshot(layer.animationLength);
                        added.SetCurveSnapshot(0f, snapshot);
                        added.SetCurveSnapshot(clip.animationLength, snapshot);
                    }
                }
            }

            animationEditContext.SelectAnimation(clip);
            ChangeScreen(EditAnimationScreen.ScreenName);
            if(createInOtherAtoms.val) plugin.peers.SendSyncAnimation(clip);
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
