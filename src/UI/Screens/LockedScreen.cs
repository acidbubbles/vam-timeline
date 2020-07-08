namespace VamTimeline
{
    public class LockedScreen : ScreenBase
    {
        public const string ScreenName = "Locked";

        public override string screenId => ScreenName;

        public LockedScreen()
            : base()
        {

        }
        public override void Init(IAtomPlugin plugin)
        {
            base.Init(plugin);

            InitExplanation();
        }

        private void InitExplanation()
        {
            var textJSON = new JSONStorableString("Help", @"
<b>Locked</b>

Enable ""Edit Mode"" to make modifications to this animation.
");
            var textUI = prefabFactory.CreateTextField(textJSON);
            textUI.height = 350f;
        }
    }
}

