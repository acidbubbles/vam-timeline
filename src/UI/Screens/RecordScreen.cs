namespace VamTimeline
{
    public class RecordScreen : ScreenBase
    {
        public const string ScreenName = "Record";

        public override string screenId => ScreenName;


        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            CreateChangeScreenButton("<b><</b> <i>Back</i>", MoreScreen.ScreenName);
        }
    }
}

