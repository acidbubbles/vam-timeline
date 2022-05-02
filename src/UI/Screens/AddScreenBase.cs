namespace VamTimeline
{
    public abstract class AddScreenBase : ScreenBase
    {
        #region Init

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            // Right side

            CreateChangeScreenButton($"<b><</b> <i>Back to {AddAnimationsScreen.ScreenName}</i>", AddAnimationsScreen.ScreenName);
        }

        #endregion
    }
}
