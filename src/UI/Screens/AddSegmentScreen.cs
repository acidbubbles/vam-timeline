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

                InitNewSegmentNameUI("Assign name to current segment");
                InitUseSegments();
            }
            else
            {
                prefabFactory.CreateSpacer();
                prefabFactory.CreateHeader("Create segment", 1);

                InitNewClipNameUI();
                InitNewLayerNameUI();
                InitNewSegmentNameUI();
                InitNewPositionUI();
                InitCreateInOtherAtomsUI();
                InitAddAnotherUI();
                InitCreateSegmentUI();

                prefabFactory.CreateSpacer();
                prefabFactory.CreateHeader("Advanced", 2);

                InitCopySegmentUI();
                InitCreateTransitionSegmentUI();

                prefabFactory.CreateSpacer();
                prefabFactory.CreateHeader("More", 2);

                CreateChangeScreenButton("<i>Create <b>shared segment</b>...</i>", AddSharedSegmentScreen.ScreenName);
            }

            RefreshUI();
        }

        public void InitUseSegments()
        {
            var useSegmentsUI = prefabFactory.CreateButton("Use segments");
            useSegmentsUI.button.onClick.AddListener(UseSegments);
        }

        public void InitCreateSegmentUI()
        {
            _createSegmentUI = prefabFactory.CreateButton("<b>Create new segment</b>");
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
            var previousAnimationSegment = current.animationSegment;
            var animationSegment = !string.IsNullOrEmpty(segmentNameJSON.val) ? segmentNameJSON.val : animation.GetUniqueSegmentName("Segment 1");
            foreach (var clip in animation.index.segmentsById[AtomAnimationClip.NoneAnimationSegmentId].allClips)
            {
                clip.animationSegment = animationSegment;
            }
            if (animation.playingAnimationSegment == previousAnimationSegment)
                animation.playingAnimationSegment = animationSegment;
            animation.index.Rebuild();
            if (!addAnotherJSON.val) ChangeScreen(AddAnimationsScreen.ScreenName);
        }

        private void AddSegment()
        {
            var clip = operations.Segments().Add(clipNameJSON.val, layerNameJSON.val, segmentNameJSON.val, createPositionJSON.val);

            animationEditContext.SelectAnimation(clip);
            if(createInOtherAtomsJSON.val) plugin.peers.SendSyncAnimation(clip);
            if (!addAnotherJSON.val) ChangeScreen(EditAnimationScreen.ScreenName);
        }

        private void CopySegment()
        {
            var result = currentSegment.allClips
                .Select(c => operations.AddAnimation().AddAnimation(c, c.animationName, c.animationLayer, segmentNameJSON.val, AddAnimationOperations.Positions.NotSpecified, true, true))
                .Select(r => r.created)
                .ToList();

            animation.index.Rebuild();

            foreach (var r in result)
            {
                if (createInOtherAtomsJSON.val) plugin.peers.SendSyncAnimation(r);
            }

            animationEditContext.SelectAnimation(result.First(c => c.animationLayerId == current.animationLayerId && c.animationNameId == current.animationNameId));
            if (!addAnotherJSON.val) ChangeScreen(EditAnimationScreen.ScreenName);
        }

        private void CreateTransitionSegment()
        {
            var clip = operations.Segments().Add(clipNameJSON.val, layerNameJSON.val, segmentNameJSON.val, createPositionJSON.val);
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
            if (createInOtherAtomsJSON.val) plugin.peers.SendSyncAnimation(clip);
            if (!addAnotherJSON.val) ChangeScreen(EditAnimationScreen.ScreenName);
        }

        #endregion

        protected override void RefreshUI()
        {
            base.RefreshUI();

            if (clipNameJSON != null) clipNameJSON.val = animation.GetUniqueAnimationName(-1, current.animationName);
            if (layerNameJSON != null) layerNameJSON.val = AtomAnimationClip.DefaultAnimationLayer;
            if (segmentNameJSON != null) segmentNameJSON.val = current.isOnNoneSegment ? "Segment 1" : animation.GetUniqueSegmentName(current);
        }

        protected override void OptionsUpdated()
        {
            if (clipNameJSON == null) return;

            var isValid =
                !string.IsNullOrEmpty(clipNameJSON.val) &&
                animation.index.ByName(AtomAnimationClip.SharedAnimationSegment, clipNameJSON.val).Count == 0 &&
                !string.IsNullOrEmpty(layerNameJSON.val) &&
                !string.IsNullOrEmpty(segmentNameJSON.val) &&
                !animation.index.segmentNames.Contains(segmentNameJSON.val);

            _createSegmentUI.button.interactable = isValid;
            _copySegmentUI.button.interactable = isValid;
        }
    }
}
