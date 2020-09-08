namespace VamTimeline
{
    public class OptionsScreen : ScreenBase
    {
        public const string ScreenName = "Settings";
        private JSONStorableFloat _snapJSON;
        private JSONStorableBool _autoKeyframeAllControllersJSON;

        public override string screenId => ScreenName;

        public OptionsScreen()
            : base()
        {
        }

        #region Init

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            // Right side

            CreateChangeScreenButton("<b><</b> <i>Back</i>", MoreScreen.ScreenName);

            prefabFactory.CreateSpacer();

            InitSnapUI();

            prefabFactory.CreateSpacer();

            InitAutoKeyframeUI();

            animationEditContext.onEditorSettingsChanged.AddListener(OnEditorSettingsChanged);
        }

        private void InitSnapUI()
        {
            _snapJSON = new JSONStorableFloat("Snap", 0.001f, (float val) => animationEditContext.snap = val.Snap(), 0.1f, 1f)
            {
                valNoCallback = animationEditContext.snap
            };
            var snapUI = prefabFactory.CreateSlider(_snapJSON);
            snapUI.valueFormat = "F3";
        }

        private void InitAutoKeyframeUI()
        {
            _autoKeyframeAllControllersJSON = new JSONStorableBool("Auto keyframe all controllers", animationEditContext.autoKeyframeAllControllers, (bool val) => animationEditContext.autoKeyframeAllControllers = val);
            var autoKeyframeAllControllersUI = prefabFactory.CreateToggle(_autoKeyframeAllControllersJSON);
        }

        #endregion

        private void OnEditorSettingsChanged(string _)
        {
            _snapJSON.valNoCallback = animationEditContext.snap;
            _autoKeyframeAllControllersJSON.valNoCallback = animationEditContext.autoKeyframeAllControllers;
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
        }
    }
}

