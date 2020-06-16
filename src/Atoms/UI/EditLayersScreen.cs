namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class EditLayersScreen : ScreenBase
    {
        public const string ScreenName = "Layers";

        public override string name => ScreenName;

        public EditLayersScreen(IAtomPlugin plugin)
            : base(plugin)
        {

        }
        public override void Init()
        {
            base.Init();

            CreateChangeScreenButton("<i><b>Clips</b></i>", ClipsScreen.ScreenName, true);
            CreateChangeScreenButton("<i><b>Add</b> a new animation...</i>", AddAnimationScreen.ScreenName, true);
        }
    }
}

