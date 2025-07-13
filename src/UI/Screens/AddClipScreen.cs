using System.Linq;

namespace VamTimeline
{
    public class AddClipScreen : AddScreenBase
    {
        public const string ScreenName = "Add animation";

        private JSONStorableBool _copySettingsJSON;
        private JSONStorableBool _copyKeyframesJSON;
        private JSONStorableBool _sequenceToJSON;
        private JSONStorableBool _createOnAllLayersJSON;
        private UIDynamicButton _addAnimationTransitionUI;
        private UIDynamicButton _createNewUI;
        private UIDynamicButton _splitAtScrubberUI;

        public override string screenId => ScreenName;

        #region Init

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            // Right side

            prefabFactory.CreateHeader("Create animation", 1);

            InitNewClipNameUI();
            InitNewPositionUI();
            InitCopySettings();
            InitCopyKeyframes();
            InitCreateOnAllLayers();
            InitSequenceTo();
            InitCreateInOtherAtomsUI();
            InitAddAnotherUI();
            InitCreateAnimationUI();

            prefabFactory.CreateSpacer();
            prefabFactory.CreateHeader("Advanced", 1);

            InitSplitAtScubberUI();
            InitCreateTransitionUI();
            InitMergeUI();

            prefabFactory.CreateSpacer();
            prefabFactory.CreateHeader("More", 1);

            CreateChangeScreenButton("<i><b>Import</b> from file...</i>", ImportExportScreen.ScreenName);
            CreateChangeScreenButton("<i><b>Manage/reorder</b> animations...</i>", ManageAnimationsScreen.ScreenName);

            RefreshUI();
        }

        private void InitCreateAnimationUI()
        {
            _createNewUI = prefabFactory.CreateButton("<b>Create animation</b>");
            _createNewUI.button.onClick.AddListener(AddAnimation);
        }

        private void InitCreateTransitionUI()
        {
            _addAnimationTransitionUI = prefabFactory.CreateButton("Create transition (current -> next)");
            _addAnimationTransitionUI.button.onClick.AddListener(AddTransitionAnimation);
        }

        private void InitSplitAtScubberUI()
        {
            _splitAtScrubberUI = prefabFactory.CreateButton("Split at scrubber position");
            _splitAtScrubberUI.button.onClick.AddListener(SplitAnimationAtScrubber);
        }

        private void InitCopySettings()
        {
            _copySettingsJSON = new JSONStorableBool("Copy settings", false, val =>
            {
                if (!val) _copyKeyframesJSON.valNoCallback = false;
            });
            prefabFactory.CreateToggle(_copySettingsJSON);
        }

        private void InitCopyKeyframes()
        {
            _copyKeyframesJSON = new JSONStorableBool("Copy keyframes", false, val =>
            {
                if (val) _copySettingsJSON.valNoCallback = true;
            });
            prefabFactory.CreateToggle(_copyKeyframesJSON);
        }

        private void InitSequenceTo()
        {
            _sequenceToJSON = new JSONStorableBool("Sequence current to new", false);
            prefabFactory.CreateToggle(_sequenceToJSON);
        }

        private void InitCreateOnAllLayers()
        {
            _createOnAllLayersJSON = new JSONStorableBool("Create on all layers", false);
            prefabFactory.CreateToggle(_createOnAllLayersJSON);
        }

        private void InitMergeUI()
        {
            var mergeUI = prefabFactory.CreateButton("Merge with next animation");
            mergeUI.button.onClick.AddListener(MergeWithNext);
        }

        #endregion

        #region Callbacks

        private void SplitAnimationAtScrubber()
        {
            var time = current.clipTime.Snap();
            if (time < 0.001 || time > current.animationLength - 0.001)
            {
                SuperController.LogError("Timeline: To split animations, move the scrubber to a position other than the first or last frame");
                return;
            }

            foreach (var created in operations.AddAnimation().AddAnimation(clipNameJSON.val, createPositionJSON.val, true, true, _createOnAllLayersJSON.val))
            {
                created.source.loop = false;
                created.source.loopPreserveLastFrame = false;
                created.source.DirtyAll();
                created.created.loop = false;
                created.created.DirtyAll();
                operations.Resize().CropOrExtendAt(created.created, created.created.animationLength - time, 0);
                operations.Resize().CropOrExtendEnd(created.source, time);
                if (createInOtherAtomsJSON.val) plugin.peers.SendSyncAnimation(created.created);
            }
        }

        private void MergeWithNext()
        {
            var next = currentLayer.SkipWhile(c => c != current).Skip(1).FirstOrDefault();
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

        private void AddAnimation()
        {
            var result = operations.AddAnimation().AddAnimation(clipNameJSON.val, createPositionJSON.val, _copySettingsJSON.val, _copyKeyframesJSON.val, _createOnAllLayersJSON.val);
            var clip = result.Select(r => r.created).FirstOrDefault(c => c.animationLayerQualified == current.animationLayerQualified);
            if(clip == null) return;
            if (_sequenceToJSON.val)
            {
                current.nextAnimationName = clip.animationName;
                current.nextAnimationTime = clip.animationLength;
            }
            if (createInOtherAtomsJSON.val) plugin.peers.SendAddAnimation(clip, createPositionJSON.val, _copySettingsJSON.val, _copyKeyframesJSON.val, _createOnAllLayersJSON.val);
            animationEditContext.SelectAnimation(clip);
            if (!addAnotherJSON.val) ChangeScreen(EditAnimationScreen.ScreenName);
        }

        private void AddTransitionAnimation()
        {
            var result = operations.AddAnimation().AddTransitionAnimation(_createOnAllLayersJSON.val);
            var clip = result.FirstOrDefault(c => c.source == current)?.created;
            if(clip == null) return;
            if (createInOtherAtomsJSON.val) plugin.peers.SendSyncAnimation(clip);
            animationEditContext.SelectAnimation(clip);
            if (!addAnotherJSON.val) ChangeScreen(EditAnimationScreen.ScreenName);
        }

        #endregion

        #region Events

        protected override void RefreshUI()
        {
            base.RefreshUI();

            clipNameJSON.val = animation.GetUniqueAnimationNameInLayer(current);

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
                currentLayer.All(c => c.animationName != clipNameJSON.val) &&
                current.isOnSharedSegment
                    ? animation.index.segmentsById.Where(kvp => kvp.Key != AtomAnimationClip.SharedAnimationSegmentId)
                        .SelectMany(l => l.Value.allClips)
                        .All(c => c.animationName != clipNameJSON.val)
                    : animation.index.ByName(AtomAnimationClip.SharedAnimationSegment, clipNameJSON.val).Count == 0;

            _createNewUI.button.interactable = nameValid;
            _splitAtScrubberUI.button.interactable = nameValid;
        }
    }
}
