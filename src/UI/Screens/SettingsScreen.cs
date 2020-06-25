using System;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class SettingsScreen : ScreenBase
    {
        public const string ScreenName = "Settings";

        public override string name => ScreenName;

        public SettingsScreen(IAtomPlugin plugin)
            : base(plugin)
        {

        }

        #region Init

        public override void Init()
        {
            base.Init();

            // Right side

            CreateChangeScreenButton("<b><</b> <i>Back</i>", MoreScreen.ScreenName, true);

            CreateSpacer(true);

            CreateSnap(true);
        }

        private void CreateSnap(bool rightSide)
        {
            RegisterStorable(plugin.snapJSON);
            var snapUI = plugin.CreateSlider(plugin.snapJSON, rightSide);
            snapUI.valueFormat = "F3";
            RegisterComponent(snapUI);
        }

        #endregion

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}

