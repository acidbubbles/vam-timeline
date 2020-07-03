namespace VamTimeline
{
    public class PerformanceScreen : ScreenBase
    {
        public const string ScreenName = "Lock";

        public override string screenId => ScreenName;

        public PerformanceScreen()
            : base()
        {

        }
        public override void Init(IAtomPlugin plugin)
        {
            base.Init(plugin);

            plugin.animation.locked = true;

            InitExplanation();
        }

        private void InitExplanation()
        {
            var textJSON = new JSONStorableString("Help", @"
<b>Performance Mode</b>

This mode is optimized to reduce the runtime cost of Timeline to a strict minimum.

Use this mode before saving and publishing a scene.
");
            var textUI = prefabFactory.CreateTextField(textJSON);
            textUI.height = 350f;
        }
    }
}

