namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class PerformanceScreen : ScreenBase
    {
        public const string ScreenName = "Lock";

        public override string name => ScreenName;

        public PerformanceScreen(IAtomPlugin plugin)
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
<b>Performance Mode</b>

This mode is optimized to reduce the runtime cost of Timeline to a strict minimum.

Use this mode before saving and publishing a scene.
");
            RegisterStorable(textJSON);
            var textUI = plugin.CreateTextField(textJSON, true);
            textUI.height = 350f;
            RegisterComponent(textUI);
        }
    }
}

