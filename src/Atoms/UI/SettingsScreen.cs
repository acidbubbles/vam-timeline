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
        public override string Name => ScreenName;

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
            RegisterStorable(Plugin.SnapJSON);
            var snapUI = Plugin.CreateSlider(Plugin.SnapJSON, rightSide);
            snapUI.valueFormat = "F3";
            RegisterComponent(snapUI);
        }

        private void CreateInterpolation(bool rightSide)
        {
            var interpolationSpeedJSON = new JSONStorableFloat("Interpolation Speed", 1f, (float val) => Plugin.Animation.InterpolationSpeed = val, 0.1f, 4f, true)
            {
                valNoCallback = Plugin.Animation.InterpolationSpeed
            };
            RegisterStorable(interpolationSpeedJSON);
            var interpolationSpeedUI = Plugin.CreateSlider(interpolationSpeedJSON, rightSide);
            RegisterComponent(interpolationSpeedUI);

            var interpolationTimeoutJSON = new JSONStorableFloat("Interpolation Timeout", 1f, (float val) => Plugin.Animation.InterpolationTimeout = val, 0f, 10f, true)
            {
                valNoCallback = Plugin.Animation.InterpolationTimeout
            };
            RegisterStorable(interpolationTimeoutJSON);
            var interpolationTimeoutUI = Plugin.CreateSlider(interpolationTimeoutJSON, rightSide);
            RegisterComponent(interpolationTimeoutUI);
        }

        #endregion


        public override void Dispose()
        {
            base.Dispose();
        }
    }
}

