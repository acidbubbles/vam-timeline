namespace VamTimeline
{
    public class LockedScreen : ScreenBase
    {
        public const string ScreenName = "Locked";

        public override string screenId => ScreenName;

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

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

