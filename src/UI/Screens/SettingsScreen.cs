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

        public override string screenId => ScreenName;

        public SettingsScreen()
            : base()
        {
        }

        #region Init

        public override void Init(IAtomPlugin plugin)
        {
            base.Init(plugin);

            // Right side

            CreateChangeScreenButton("<b><</b> <i>Back</i>", MoreScreen.ScreenName, true);

            prefabFactory.CreateSpacer();

            CreateSnap(true);
        }

        private void CreateSnap(bool rightSide)
        {
                        var snapUI = prefabFactory.CreateSlider(plugin.snapJSON, rightSide);
            snapUI.valueFormat = "F3";
        }

        #endregion

        public override void OnDestroy()
        {
            base.OnDestroy();
        }
    }
}

