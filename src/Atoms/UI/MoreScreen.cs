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

            CreateChangeScreenButton("Settings...", SettingsScreen.ScreenName, true);
            CreateChangeScreenButton("Advanced...", AdvancedScreen.ScreenName, true);
        }
    }
}

