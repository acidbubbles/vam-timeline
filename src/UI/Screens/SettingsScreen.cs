namespace VamTimeline
{
    public class SettingsScreen : ScreenBase
    {
        public const string ScreenName = "Settings";
        private JSONStorableFloat _snapJSON;

        public override string screenId => ScreenName;

        public SettingsScreen()
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

            CreateSnap();

            animation.onEditorSettingsChanged.AddListener(OnEditorSettingsChanged);
        }

        private void OnEditorSettingsChanged(string _)
        {
            _snapJSON.valNoCallback = animation.snap;
        }

        private void CreateSnap()
        {
            _snapJSON = new JSONStorableFloat("Snap", 0.1f, 0.01f, 1f)
            {
                valNoCallback = animation.snap
            };
            var snapUI = prefabFactory.CreateSlider(_snapJSON);
            snapUI.valueFormat = "F3";
        }

        #endregion

        public override void OnDestroy()
        {
            base.OnDestroy();
        }
    }
}

