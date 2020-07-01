namespace VamTimeline
{
    public class SettingsScreen : ScreenBase
    {
        public const string ScreenName = "Settings";

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
        }

        private void CreateSnap()
        {
            var snapUI = prefabFactory.CreateSlider(plugin.snapJSON);
            snapUI.valueFormat = "F3";
        }

        #endregion

        public override void OnDestroy()
        {
            base.OnDestroy();
        }
    }
}

