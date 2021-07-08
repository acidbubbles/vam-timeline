using System.Linq;

namespace VamTimeline
{
    public class AddAnimationScreen : ScreenBase
    {
        public const string ScreenName = "Add Animation";
        private UIDynamicButton _addAnimationTransitionUI;

        public override string screenId => ScreenName;

        #region Init

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            // Right side

            CreateChangeScreenButton($"<b><</b> <i>Back to {AnimationsScreen.ScreenName}</i>", AnimationsScreen.ScreenName);

            prefabFactory.CreateHeader("Add animations", 1);

            prefabFactory.CreateSpacer();

            prefabFactory.CreateHeader("Animations", 2);

            InitCreateAnimationUI();

            prefabFactory.CreateSpacer();

            prefabFactory.CreateHeader("Layers", 2);

            InitCreateLayerUI();
            InitSplitLayerUI();

            prefabFactory.CreateSpacer();

            prefabFactory.CreateHeader("More", 2);

            CreateChangeScreenButton("<i><b>Import</b> from file...</i>", ImportExportScreen.ScreenName);
            CreateChangeScreenButton("<i><b>Manage</b> animations list...</i>", ManageAnimationsScreen.ScreenName);
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

            RefreshButtons();
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

        private void AddAnimationAsCopy()
        {
            var clip = operations.AddAnimation().AddAnimationAsCopy();
            if(clip == null) return;
            animationEditContext.SelectAnimation(clip);
            ChangeScreen(EditAnimationScreen.ScreenName);
        }

        private void SplitAnimationAtScrubber()
        {
            var time = current.clipTime.Snap();
            if (time < 0.001 || time > current.animationLength - 0.001)
            {
                SuperController.LogError("Timeline: To split animations, move the scrubber to a position other than the first or last frame");
                return;
            }

            var clip = operations.AddAnimation().AddAnimationAsCopy();
            if(clip == null) return;
            clip.loop = false;
            operations.Resize().CropOrExtendAt(clip, clip.animationLength - time, 0);
            current.loop = false;
            operations.Resize().CropOrExtendEnd(current, time);
        }

        private void MergeWithNext()
        {
            var next = animation.index.ByLayer(current.animationLayer).SkipWhile(c => c != current).Skip(1).FirstOrDefault();
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
                    curEnum.Current.keys.RemoveAt(curEnum.Current.keys.Count - 1);
                    curEnum.Current.keys.AddRange(nextEnum.Current.keys.Select(k =>
                    {
                        k.time += animationLengthOffset;
                        return k;
                    }));
                }
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
            var clip = operations.AddAnimation().AddAnimationFromCurrentFrame(copySettings);
            if(clip == null) return;
            animationEditContext.SelectAnimation(clip);
            ChangeScreen(EditAnimationScreen.ScreenName);
        }

        private void AddTransitionAnimation()
        {
            var clip = operations.AddAnimation().AddTransitionAnimation();
            if(clip == null) return;
            animationEditContext.SelectAnimation(clip);
            ChangeScreen(EditAnimationScreen.ScreenName);
        }

        private void AddLayer()
        {
            var clip = operations.Layers().Add();

            animationEditContext.SelectAnimation(clip);
            ChangeScreen(EditAnimationScreen.ScreenName);
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

        #region Events

        protected override void OnCurrentAnimationChanged(AtomAnimationEditContext.CurrentAnimationChangedEventArgs args)
        {
            base.OnCurrentAnimationChanged(args);

            RefreshButtons();
        }

        private void RefreshButtons()
        {
            var hasNext = current.nextAnimationName != null;
            var nextIsTransition = false;
            if (hasNext)
            {
                var nextClip = animation.GetClip(current.animationLayer, current.nextAnimationName);
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
    }
}

