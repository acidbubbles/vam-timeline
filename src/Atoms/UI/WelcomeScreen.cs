using System;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class WelcomeScreen : ScreenBase
    {
        public const string ScreenName = "Welcome";
        public override string Name => ScreenName;

        public WelcomeScreen(IAtomPlugin plugin)
            : base(plugin)
        {

        }
        public override void Init()
        {
            base.Init();

            InitExplanation();
        }

        private void InitExplanation()
        {
            var textJSON = new JSONStorableString("Help", @"
<b>Welcome to Timeline!</b>

This plugin allows for advanced keyframe-based editing.

Documentation available at:
github.com/acidbubbles/vam-timeline

<b>The basics</b>

- Choose what to animate in <i>Targets</i>
- Create keyframes in <i>Edit</i>
- Change animation length in <i>Settings</i>
");
            RegisterStorable(textJSON);
            var textUI = Plugin.CreateTextField(textJSON, true);
            textUI.height = 600;
            RegisterComponent(textUI);
        }
    }
}

