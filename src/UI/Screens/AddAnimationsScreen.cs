namespace VamTimeline
{
    public class AddAnimationsScreen : ScreenBase
    {
        public const string ScreenName = "Add animations";

        public override string screenId => ScreenName;

        #region Init

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            // Right side

            CreateChangeScreenButton($"<b><</b> <i>Back to {AnimationsScreen.ScreenName}</i>", AnimationsScreen.ScreenName);

            prefabFactory.CreateSpacer();
            prefabFactory.CreateHeader("Create", 1);

            CreateChangeScreenButton("<i>Create <b>clip</b>...</i>", AddClipScreen.ScreenName);
            CreateChangeScreenButton("<i>Create <b>layer</b>...</i>", AddLayerScreen.ScreenName);
            if (animation.index.useSegment)
                CreateChangeScreenButton("<i>Create <b>segment</b>...</i>", AddSegmentScreen.ScreenName);
            else
                CreateChangeScreenButton("<i>Use <b>segments</b>...</i>", AddSegmentScreen.ScreenName);

            prefabFactory.CreateSpacer();
            prefabFactory.CreateHeader("More", 1);

            CreateChangeScreenButton("<i><b>Import</b> from file...</i>", ImportExportScreen.ScreenName);
            CreateChangeScreenButton("<i><b>Manage/reorder</b> animations...</i>", ManageAnimationsScreen.ScreenName);
        }

        #endregion
    }
}

