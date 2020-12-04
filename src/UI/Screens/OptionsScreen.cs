namespace VamTimeline
{
    public class OptionsScreen : ScreenBase
    {
        public const string ScreenName = "Settings";
        private JSONStorableBool _lockedJSON;
        private JSONStorableFloat _snapJSON;
        private JSONStorableBool _autoKeyframeAllControllersJSON;
        private JSONStorableBool _showPaths;

        public override string screenId => ScreenName;

        #region Init

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            // Right side

            CreateChangeScreenButton("<b><</b> <i>Back</i>", MoreScreen.ScreenName);

            prefabFactory.CreateSpacer();

            InitLockedUI();

            prefabFactory.CreateSpacer();

            InitSnapUI();

            prefabFactory.CreateSpacer();

            InitAutoKeyframeUI();

            animationEditContext.onEditorSettingsChanged.AddListener(OnEditorSettingsChanged);

            _showPaths = new JSONStorableBool(
                "Show paths", animationEditContext.showPaths,
                val => animationEditContext.showPaths = val);

            prefabFactory.CreateToggle(_showPaths);
        }

        private void InitLockedUI()
        {
            _lockedJSON = new JSONStorableBool("Lock edits", animationEditContext.locked, val => animationEditContext.locked = val);
            prefabFactory.CreateToggle(_lockedJSON);
        }

        private void InitSnapUI()
        {
            _snapJSON = new JSONStorableFloat("Snap", 0.001f, val => animationEditContext.snap = val.Snap(), 0.1f, 1f)
            {
                valNoCallback = animationEditContext.snap
            };
            var snapUI = prefabFactory.CreateSlider(_snapJSON);
            snapUI.valueFormat = "F3";
        }

        private void InitAutoKeyframeUI()
        {
            _autoKeyframeAllControllersJSON = new JSONStorableBool("Keyframe all controllers at once", animationEditContext.autoKeyframeAllControllers, val => animationEditContext.autoKeyframeAllControllers = val);
            prefabFactory.CreateToggle(_autoKeyframeAllControllersJSON);
        }

        #endregion

        private void OnEditorSettingsChanged(string _)
        {
            _lockedJSON.valNoCallback = animationEditContext.locked;
            _snapJSON.valNoCallback = animationEditContext.snap;
            _autoKeyframeAllControllersJSON.valNoCallback = animationEditContext.autoKeyframeAllControllers;
        }
    }
}

