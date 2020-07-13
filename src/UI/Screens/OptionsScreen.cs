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

        public override void Init(IAtomPlugin plugin)
        {
            base.Init(plugin);

            // Right side

            CreateChangeScreenButton("<b><</b> <i>Back</i>", MoreScreen.ScreenName);

            prefabFactory.CreateSpacer();

            InitSnapUI();

            prefabFactory.CreateSpacer();

            InitAutoKeyframeUI();

            animation.onEditorSettingsChanged.AddListener(OnEditorSettingsChanged);
        }

        private void InitSnapUI()
        {
            _snapJSON = new JSONStorableFloat("Snap", 0.1f, (float val) => animation.snap = val.Snap(), 0.1f, 1f)
            {
                valNoCallback = animation.snap
            };
            var snapUI = prefabFactory.CreateSlider(_snapJSON);
            snapUI.valueFormat = "F3";
        }

        private void InitAutoKeyframeUI()
        {
            _autoKeyframeAllControllersJSON = new JSONStorableBool("Auto keyframe all controllers", animation.autoKeyframeAllControllers, (bool val) => animation.autoKeyframeAllControllers = val);
            var autoKeyframeAllControllersUI = prefabFactory.CreateToggle(_autoKeyframeAllControllersJSON);
        }

        #endregion

        private void OnEditorSettingsChanged(string _)
        {
            _snapJSON.valNoCallback = animation.snap;
            _autoKeyframeAllControllersJSON.valNoCallback = animation.autoKeyframeAllControllers;
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
        }
    }
}

