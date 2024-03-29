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
            string text;
            if(animationEditContext.locked)
                text = @"Enable ""Edit Mode"" to make modifications to this animation";
            else if (!plugin.isActiveAndEnabled)
                text = "Enable the Timeline plugin as well as the containing atom to make modifications to this animation";
            else
                text = "Could not identify a lock reason";

            var textJSON = new JSONStorableString("Help", $@"
<b>Locked</b>

{text}
");
            var textUI = prefabFactory.CreateTextField(textJSON);
            textUI.height = 350f;
        }
    }
}

