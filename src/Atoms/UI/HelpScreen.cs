namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class HelpScreen : ScreenBase
    {
        public const string ScreenName = "Help";
        public override string Name => ScreenName;

        public HelpScreen(IAtomPlugin plugin)
            : base(plugin)
        {

        }
        public override void Init()
        {
            base.Init();

            InitExplanation();
        }

        private void InitExplanation()
        {
            var textJSON = new JSONStorableString("Help", @"
<b>Need help?</b>

Documentation available at:

github.com/acidbubbles/vam-timeline
");
            RegisterStorable(textJSON);
            var textUI = Plugin.CreateTextField(textJSON, true);
            textUI.height = 600;
            RegisterComponent(textUI);
        }
    }
}

