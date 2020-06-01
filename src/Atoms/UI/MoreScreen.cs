namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class MoreScreen : ScreenBase
    {
        public const string ScreenName = "More...";
        public override string Name => ScreenName;

        public MoreScreen(IAtomPlugin plugin)
            : base(plugin)
        {

        }
        public override void Init()
        {
            base.Init();

            // Right side

            InitSpeedUI(true);

            CreateSpacer(true);

            CreateChangeScreenButton("Edit Animation...", SettingsScreen.ScreenName, true);
            CreateChangeScreenButton("Settings...", SettingsScreen.ScreenName, true);
            CreateChangeScreenButton("Advanced...", AdvancedScreen.ScreenName, true);
        }

        private void InitSpeedUI(bool rightSide)
        {
            RegisterStorable(Plugin.SpeedJSON);
            var speedUI = Plugin.CreateSlider(Plugin.SpeedJSON, rightSide);
            speedUI.valueFormat = "F3";
            RegisterComponent(speedUI);
        }
    }
}

