using System.Collections.Generic;
using System.Linq;

namespace VamTimeline
{
    public class AddAnimationScreen : ScreenBase
    {
        public const string ScreenName = "Add Animation";

        private const string _positionFirst = "First";
        private const string _positionPrevious = "Previous";
        private const string _positionNext = "Next";
        private const string _positionLast = "Last";

        private static bool _previousCreateInOtherAtoms;

        private UIDynamicButton _addAnimationTransitionUI;
        private JSONStorableStringChooser _createPosition;
        private JSONStorableString _createName;
        private JSONStorableBool _createInOtherAtoms;

        public override string screenId => ScreenName;

        #region Init

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            // Right side

            CreateChangeScreenButton($"<b><</b> <i>Back to {AnimationsScreen.ScreenName}</i>", AnimationsScreen.ScreenName);

            prefabFactory.CreateHeader("Add animations", 1);

            InitCreateAnimationUI();

            prefabFactory.CreateHeader("Add options", 2);

            InitCreateAnimationOptionsUI();

            prefabFactory.CreateSpacer();
            prefabFactory.CreateHeader("Add layers", 1);

            InitCreateLayerUI();
            InitSplitLayerUI();

            prefabFactory.CreateSpacer();
            prefabFactory.CreateHeader("Add segment", 1);

            InitCreateSegmentUI();

            prefabFactory.CreateSpacer();

            prefabFactory.CreateHeader("More", 1);

            CreateChangeScreenButton("<i><b>Import</b> from file...</i>", ImportExportScreen.ScreenName);
            CreateChangeScreenButton("<i><b>Manage</b> animations list...</i>", ManageAnimationsScreen.ScreenName);

            RefreshUI();
        }

        private void InitCreateAnimationUI()
        {
            var createNewUI = prefabFactory.CreateButton("Create new");
            createNewUI.button.onClick.AddListener(() => AddAnimationFromCurrentFrame(false));

            var createNewCarrySettingsUI = prefabFactory.CreateButton("Create new (carry settings)");
            createNewCarrySettingsUI.button.onClick.AddListener(() => AddAnimationFromCurrentFrame(true));

            var createCopyUI = prefabFactory.CreateButton("Create copy");
            createCopyUI.button.onClick.AddListener(AddAnimationAsCopy);

            var splitAtScrubberUI = prefabFactory.CreateButton("Split at scrubber position");
            splitAtScrubberUI.button.onClick.AddListener(SplitAnimationAtScrubber);

            var mergeUI = prefabFactory.CreateButton("Merge with next animation");
            mergeUI.button.onClick.AddListener(MergeWithNext);

            _addAnimationTransitionUI = prefabFactory.CreateButton("Create transition (current -> next)");
            _addAnimationTransitionUI.button.onClick.AddListener(AddTransitionAnimation);
        }

        private void InitCreateAnimationOptionsUI()
        {
            _createName = new JSONStorableString("New animation name", "");
            prefabFactory.CreateTextInput(_createName);

            _createPosition = new JSONStorableStringChooser(
                "Add at position",
                new List<string> { _positionFirst, _positionPrevious, _positionNext, _positionLast },
                _positionNext,
                "Add at position");
            prefabFactory.CreatePopup(_createPosition, false, true);

            _createInOtherAtoms = new JSONStorableBool("Auto create in other atoms", _previousCreateInOtherAtoms, val => _previousCreateInOtherAtoms = val);
            prefabFactory.CreateToggle(_createInOtherAtoms);
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

        public void InitCreateSegmentUI()
        {
            var createSegmentUI = prefabFactory.CreateButton("Create new segment");
            createSegmentUI.button.onClick.AddListener(AddSegment);

            var createSharedSegmentUI = prefabFactory.CreateButton("Create shared segment");
            createSharedSegmentUI.button.onClick.AddListener(AddSharedSegment);
        }

        #endregion

        #region Callbacks

        private void AddAnimationAsCopy()
        {
            var clip = operations.AddAnimation().AddAnimationAsCopy(_createName.val, GetPosition());
            if(clip == null) return;
            animationEditContext.SelectAnimation(clip);
            ChangeScreen(EditAnimationScreen.ScreenName);
            if(_createInOtherAtoms.val) plugin.peers.SendSyncAnimation(clip);
        }

        private int GetPosition()
        {
            switch (_createPosition.val)
            {
                case _positionFirst:
                    return animation.clips.FindIndex(c => c.animationLayerQualified == current.animationLayerQualified);
                case _positionPrevious:
                    return animation.clips.IndexOf(current);
                case _positionNext:
                    return animation.clips.IndexOf(current) + 1;
                default:
                    return animation.clips.FindLastIndex(c => c.animationLayerQualified == current.animationLayerQualified) + 1;
            }
        }

        private void SplitAnimationAtScrubber()
        {
            var time = current.clipTime.Snap();
            if (time < 0.001 || time > current.animationLength - 0.001)
            {
                SuperController.LogError("Timeline: To split animations, move the scrubber to a position other than the first or last frame");
                return;
            }

            var newClip = operations.AddAnimation().AddAnimationAsCopy(_createName.val, GetPosition());
            newClip.loop = false;
            newClip.Rebuild(current);
            operations.Resize().CropOrExtendAt(newClip, newClip.animationLength - time, 0);
            current.loop = false;
            operations.Resize().CropOrExtendEnd(current, time);
            if(_createInOtherAtoms.val) plugin.peers.SendSyncAnimation(newClip);
        }

        private void MergeWithNext()
        {
            var next = animation.index.ByLayer(current.animationLayerQualified).SkipWhile(c => c != current).Skip(1).FirstOrDefault();
            if (next == null) return;

            var animationLengthOffset = current.animationLength;
            current.animationLength = (current.animationLength + next.animationLength).Snap();

            foreach (var curT in current.GetAllCurveTargets())
            {
                var nextT = next.GetAllCurveTargets().FirstOrDefault(c => c.TargetsSameAs(curT));
                if (nextT == null) continue;
                var curEnum = curT.GetCurves().GetEnumerator();
                var nextEnum = nextT.GetCurves().GetEnumerator();
                while (curEnum.MoveNext() && nextEnum.MoveNext())
                {
                    if (curEnum.Current == null) continue;
                    if (nextEnum.Current == null) continue;
                    curEnum.Current.keys.RemoveAt(curEnum.Current.keys.Count - 1);
                    curEnum.Current.keys.AddRange(nextEnum.Current.keys.Select(k =>
                    {
                        k.time += animationLengthOffset;
                        return k;
                    }));
                }
                curEnum.Dispose();
                nextEnum.Dispose();
            }

            foreach (var curT in current.targetTriggers)
            {
                var nextT = next.targetTriggers.FirstOrDefault(c => c.TargetsSameAs(curT));
                if (nextT == null) continue;
                foreach (var trigger in nextT.triggersMap)
                {
                    curT.SetKeyframe(trigger.Value.startTime + animationLengthOffset, trigger.Value);
                }
            }

            operations.AddAnimation().DeleteAnimation(next);

            current.DirtyAll();
        }

        private void AddAnimationFromCurrentFrame(bool copySettings)
        {
            var clip = operations.AddAnimation().AddAnimationFromCurrentFrame(copySettings, _createName.val, GetPosition());
            if(clip == null) return;
            animationEditContext.SelectAnimation(clip);
            ChangeScreen(EditAnimationScreen.ScreenName);
            if(_createInOtherAtoms.val) plugin.peers.SendSyncAnimation(clip);

        }

        private void AddTransitionAnimation()
        {
            var clip = operations.AddAnimation().AddTransitionAnimation();
            if(clip == null) return;
            animationEditContext.SelectAnimation(clip);
            ChangeScreen(EditAnimationScreen.ScreenName);
            if(_createInOtherAtoms.val) plugin.peers.SendSyncAnimation(clip);
        }

        private void AddLayer()
        {
            var clip = operations.Layers().Add();

            animationEditContext.SelectAnimation(clip);
            ChangeScreen(EditAnimationScreen.ScreenName);
            if(_createInOtherAtoms.val) plugin.peers.SendSyncAnimation(clip);
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

        private void AddSegment()
        {
            var clip = operations.Segments().Add();

            animationEditContext.SelectAnimation(clip);
            ChangeScreen(EditAnimationScreen.ScreenName);
            if(_createInOtherAtoms.val) plugin.peers.SendSyncAnimation(clip);
        }

        private void AddSharedSegment()
        {
            if (animation.index.segmentNames.Contains(AtomAnimationClip.SharedAnimationSegment))
            {
                animationEditContext.SelectAnimation(animation.clips.First(c => c.animationSegment == AtomAnimationClip.SharedAnimationSegment));
                return;
            }

            var clip = operations.Segments().AddShared();
            animationEditContext.SelectAnimation(clip);
            ChangeScreen(EditAnimationScreen.ScreenName);
            if(_createInOtherAtoms.val) plugin.peers.SendSyncAnimation(clip);
        }

        #endregion

        #region Events

        protected override void OnCurrentAnimationChanged(AtomAnimationEditContext.CurrentAnimationChangedEventArgs args)
        {
            base.OnCurrentAnimationChanged(args);

            RefreshUI();
        }

        private void RefreshUI()
        {
            var hasNext = current.nextAnimationName != null;
            var nextIsTransition = false;
            if (hasNext)
            {
                var nextClip = animation.GetClip(current.animationSegment, current.animationLayer, current.nextAnimationName);
                if (nextClip != null)
                    nextIsTransition = nextClip.autoTransitionPrevious;
                else
                    hasNext = false;
            }
            _addAnimationTransitionUI.button.interactable = hasNext && !nextIsTransition;
            if (!hasNext)
                _addAnimationTransitionUI.label = "Create Transition (No sequence)";
            else if (nextIsTransition)
                _addAnimationTransitionUI.label = "Create Transition (Next is transition)";
            else
                _addAnimationTransitionUI.label = "Create Transition (Current -> Next)";

            _createName.valNoCallback = animation.GetNewAnimationName(current);
        }

        #endregion
    }
}

