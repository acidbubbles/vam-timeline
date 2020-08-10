using System.Linq;

namespace VamTimeline
{
    public class AddAnimationScreen : ScreenBase
    {
        public const string ScreenName = "Add Animation";
        private UIDynamicButton _addAnimationTransitionUI;

        public override string screenId => ScreenName;

        public AddAnimationScreen()
            : base()
        {

        }

        #region Init

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            // Right side

            CreateChangeScreenButton($"<b><</b> <i>Back to {AnimationsScreen.ScreenName}</i>", AnimationsScreen.ScreenName);

            CreateHeader("Add animations", 1);

            prefabFactory.CreateSpacer();

            CreateHeader("Animations", 2);

            InitCreateAnimationUI();

            prefabFactory.CreateSpacer();

            CreateHeader("Layers", 2);

            InitCreateLayerUI();
            InitSplitLayerUI();

            prefabFactory.CreateSpacer();

            CreateHeader("More", 2);

            CreateChangeScreenButton("<i><b>Import</b> from file...</i>", ImportExportScreen.ScreenName);
            CreateChangeScreenButton("<i><b>Manage</b> animations list...</i>", ManageAnimationsScreen.ScreenName);
        }

        private void InitCreateAnimationUI()
        {
            var addAnimationFromCurrentFrameUI = prefabFactory.CreateButton("Create Animation From Current Frame");
            addAnimationFromCurrentFrameUI.button.onClick.AddListener(() => AddAnimationFromCurrentFrame());

            var addAnimationAsCopyUI = prefabFactory.CreateButton("Create Copy Of Current Animation");
            addAnimationAsCopyUI.button.onClick.AddListener(() => AddAnimationAsCopy());

            _addAnimationTransitionUI = prefabFactory.CreateButton($"Create Transition (Current -> Next)");
            _addAnimationTransitionUI.button.onClick.AddListener(() => AddTransitionAnimation());

            RefreshButtons();
        }

        public void InitCreateLayerUI()
        {
            var createLayerUI = prefabFactory.CreateButton("Create New Layer");
            createLayerUI.button.onClick.AddListener(() => AddLayer());
        }

        private void InitSplitLayerUI()
        {
            var splitLayerUI = prefabFactory.CreateButton("Split selection to new layer");
            splitLayerUI.button.onClick.AddListener(() => SplitLayer());
        }

        #endregion

        #region Callbacks

        private void AddAnimationAsCopy()
        {
            var clip = operations.AddAnimation().AddAnimationAsCopy();
            if(clip == null) return;
            animation.SelectAnimation(clip.animationName);
            ChangeScreen(EditAnimationScreen.ScreenName);
        }

        private void AddAnimationFromCurrentFrame()
        {
            var clip = operations.AddAnimation().AddAnimationFromCurrentFrame();
            if(clip == null) return;
            animation.SelectAnimation(clip.animationName);
            ChangeScreen(EditAnimationScreen.ScreenName);
        }

        private void AddTransitionAnimation()
        {
            var clip = operations.AddAnimation().AddTransitionAnimation();
            if(clip == null) return;
            animation.SelectAnimation(clip.animationName);
            ChangeScreen(EditAnimationScreen.ScreenName);
        }

        private void AddLayer()
        {
            var clip = operations.layers().Add();

            animation.SelectAnimation(clip.animationName);
            ChangeScreen(EditAnimationScreen.ScreenName);
        }

        private void SplitLayer()
        {
            var targets = current.GetSelectedTargets().ToList();
            if (targets.Count == 0)
            {
                SuperController.LogError("Timeline: You must select a subset of targets to split to another layer.");
                return;
            }

            operations.layers().SplitLayer(targets);
        }

        #endregion

        #region Events

        protected override void OnCurrentAnimationChanged(AtomAnimation.CurrentAnimationChangedEventArgs args)
        {
            base.OnCurrentAnimationChanged(args);

            RefreshButtons();
        }

        private void RefreshButtons()
        {
            bool hasNext = current.nextAnimationName != null;
            bool nextIsTransition = false;
            if (hasNext)
            {
                var nextClip = animation.GetClip(current.nextAnimationName);
                if (nextClip != null)
                    nextIsTransition = nextClip.autoTransitionPrevious;
                else
                    hasNext = false;
            }
            _addAnimationTransitionUI.button.interactable = hasNext && !nextIsTransition;
            if (!hasNext)
                _addAnimationTransitionUI.label = $"Create Transition (No sequence)";
            else if (nextIsTransition)
                _addAnimationTransitionUI.label = $"Create Transition (Next is transition)";
            else
                _addAnimationTransitionUI.label = $"Create Transition (Current -> Next)";
        }

        #endregion
    }
}

