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

            CreateInterpolation(true);
        }

        private void CreateSnap(bool rightSide)
        {
            RegisterStorable(plugin.snapJSON);
            var snapUI = plugin.CreateSlider(plugin.snapJSON, rightSide);
            snapUI.valueFormat = "F3";
            RegisterComponent(snapUI);
        }

        private void CreateInterpolation(bool rightSide)
        {
            var interpolationSpeedJSON = new JSONStorableFloat("Interpolation Speed", 1f, (float val) => plugin.animation.InterpolationSpeed = val, 0.1f, 4f, true)
            {
                valNoCallback = plugin.animation.InterpolationSpeed
            };
            RegisterStorable(interpolationSpeedJSON);
            var interpolationSpeedUI = plugin.CreateSlider(interpolationSpeedJSON, rightSide);
            RegisterComponent(interpolationSpeedUI);

            var interpolationTimeoutJSON = new JSONStorableFloat("Interpolation Timeout", 1f, (float val) => plugin.animation.InterpolationTimeout = val, 0f, 10f, true)
            {
                valNoCallback = plugin.animation.InterpolationTimeout
            };
            RegisterStorable(interpolationTimeoutJSON);
            var interpolationTimeoutUI = plugin.CreateSlider(interpolationTimeoutJSON, rightSide);
            RegisterComponent(interpolationTimeoutUI);
        }

        #endregion

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}

