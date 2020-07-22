namespace VamTimeline
{
    public class MoreScreen : ScreenBase
    {
        public const string ScreenName = "More...";

        public override string screenId => ScreenName;


        public MoreScreen()
            : base()
        {
        }

        public override void Init(IAtomPlugin plugin)
        {
            base.Init(plugin);

            CreateHeader("More options", 1);

            CreateChangeScreenButton("<b>Import / export</b> animations...", ImportExportScreen.ScreenName);
            CreateChangeScreenButton("<b>Bulk</b> changes...", BulkScreen.ScreenName);
            CreateChangeScreenButton("<b>Mocap</b> import...", MocapScreen.ScreenName);
            CreateChangeScreenButton("<b>Advanced</b> keyframe tools...", AdvancedKeyframeToolsScreen.ScreenName);

            prefabFactory.CreateSpacer();

            CreateChangeScreenButton("Options...", OptionsScreen.ScreenName);

            prefabFactory.CreateSpacer();

            CreateChangeScreenButton("Help", HelpScreen.ScreenName);
        }
    }
}

