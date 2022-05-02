using System.Collections.Generic;
using System.Linq;

namespace VamTimeline
{
    public class AddClipScreen : AddScreenBase
    {
        public const string ScreenName = "Add animation";

        private const string _positionFirst = "First";
        private const string _positionPrevious = "Previous";
        private const string _positionNext = "Next";
        private const string _positionLast = "Last";

        private UIDynamicButton _addAnimationTransitionUI;
        private JSONStorableStringChooser _createPosition;
        private UIDynamicButton _createNewUI;
        private UIDynamicButton _createNewCarrySettingsUI;
        private UIDynamicButton _createCopyUI;
        private UIDynamicButton _splitAtScrubberUI;

        public override string screenId => ScreenName;

        #region Init

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            // Right side

            prefabFactory.CreateHeader("Create", 1);

            InitNewClipNameUI();
            InitNewPositionUI();
            InitCreateAnimationUI();

            prefabFactory.CreateHeader("Options", 2);

            InitCreateInOtherAtomsUI();
            #warning Option to create on all layers

            prefabFactory.CreateSpacer();
            prefabFactory.CreateHeader("Advanced", 1);

            InitMergeUI();

            prefabFactory.CreateSpacer();
            prefabFactory.CreateHeader("More", 1);

            CreateChangeScreenButton("<i><b>Import</b> from file...</i>", ImportExportScreen.ScreenName);
            CreateChangeScreenButton("<i><b>Manage</b> animations list...</i>", ManageAnimationsScreen.ScreenName);

            RefreshUI();
        }

        private void InitCreateAnimationUI()
        {
            _createNewUI = prefabFactory.CreateButton("Create blank");
            _createNewUI.button.onClick.AddListener(() => AddAnimationFromCurrentFrame(false));

            _createNewCarrySettingsUI = prefabFactory.CreateButton("Create new (carry settings)");
            _createNewCarrySettingsUI.button.onClick.AddListener(() => AddAnimationFromCurrentFrame(true));

            _createCopyUI = prefabFactory.CreateButton("Create copy");
            _createCopyUI.button.onClick.AddListener(AddAnimationAsCopy);

            _splitAtScrubberUI = prefabFactory.CreateButton("Split at scrubber position");
            _splitAtScrubberUI.button.onClick.AddListener(SplitAnimationAtScrubber);

            _addAnimationTransitionUI = prefabFactory.CreateButton("Create transition (current -> next)");
            _addAnimationTransitionUI.button.onClick.AddListener(AddTransitionAnimation);
        }

        private void InitMergeUI()
        {
            var mergeUI = prefabFactory.CreateButton("Merge with next animation");
            mergeUI.button.onClick.AddListener(MergeWithNext);
        }

        private void InitNewPositionUI()
        {
            _createPosition = new JSONStorableStringChooser(
                "Add at position",
                new List<string> { _positionFirst, _positionPrevious, _positionNext, _positionLast },
                _positionNext,
                "Add at position");
            prefabFactory.CreatePopup(_createPosition, false, true);
        }

        #endregion

        #region Callbacks

        private void AddAnimationAsCopy()
        {
            var clip = operations.AddAnimation().AddAnimationAsCopy(clipNameJSON.val, GetPosition());
            if(clip == null) return;
            animationEditContext.SelectAnimation(clip);
            ChangeScreen(EditAnimationScreen.ScreenName);
            if(createInOtherAtoms.val) plugin.peers.SendSyncAnimation(clip);
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

            var newClip = operations.AddAnimation().AddAnimationAsCopy(clipNameJSON.val, GetPosition());
            newClip.loop = false;
            newClip.Rebuild(current);
            operations.Resize().CropOrExtendAt(newClip, newClip.animationLength - time, 0);
            current.loop = false;
            operations.Resize().CropOrExtendEnd(current, time);
            if(createInOtherAtoms.val) plugin.peers.SendSyncAnimation(newClip);
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
            var clip = operations.AddAnimation().AddAnimationFromCurrentFrame(copySettings, clipNameJSON.val, GetPosition());
            if(clip == null) return;
            animationEditContext.SelectAnimation(clip);
            ChangeScreen(EditAnimationScreen.ScreenName);
            if(createInOtherAtoms.val) plugin.peers.SendSyncAnimation(clip);

        }

        private void AddTransitionAnimation()
        {
            var clip = operations.AddAnimation().AddTransitionAnimation();
            if(clip == null) return;
            animationEditContext.SelectAnimation(clip);
            ChangeScreen(EditAnimationScreen.ScreenName);
            if(createInOtherAtoms.val) plugin.peers.SendSyncAnimation(clip);
        }

        #endregion

        #region Events

        protected override void RefreshUI()
        {
            base.RefreshUI();

            clipNameJSON.val = animation.GetNewAnimationName(current);

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
        }

        #endregion

        protected override void OptionsUpdated()
        {
            var nameValid =
                !string.IsNullOrEmpty(clipNameJSON.val) &&
                animation.index.segments
                    .Where(s => s.Key != current.animationSegment)
                    .SelectMany(s => s.Value.layers)
                    .SelectMany(l => l)
                    .All(c => c.animationName != clipNameJSON.val) &&
                currentLayer.All(c => c.animationName != clipNameJSON.val);

            _createNewUI.button.interactable = nameValid;
            _createNewCarrySettingsUI.button.interactable = nameValid;
            _createCopyUI.button.interactable = nameValid;
            _splitAtScrubberUI.button.interactable = nameValid;
        }
    }
}
