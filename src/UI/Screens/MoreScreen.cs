using UnityEngine;

namespace VamTimeline
{
    public class MoreScreen : ScreenBase
    {
        public const string ScreenName = "More...";

        public override string screenId => ScreenName;

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            prefabFactory.CreateHeader("More options", 1);

            CreateChangeScreenButton("<b>Import / export</b> animations...", ImportExportScreen.ScreenName);

            prefabFactory.CreateSpacer();

            CreateChangeScreenButton("<b>Convert</b> VaM native scene anim...", MocapScreen.ScreenName);
            CreateChangeScreenButton("<b>Record</b> animation...", RecordScreen.ScreenName);
            CreateChangeScreenButton("<b>Reduce</b> keyframes...", ReduceScreen.ScreenName);
            CreateChangeScreenButton("<b>Smooth</b> keyframes...", SmoothScreen.ScreenName);

            prefabFactory.CreateSpacer();

            CreateChangeScreenButton("<b>Bulk</b> changes...", BulkScreen.ScreenName);
            CreateChangeScreenButton("<b>Advanced</b> keyframe tools...", AdvancedKeyframeToolsScreen.ScreenName);
            CreateChangeScreenButton("<b>Grouping</b>...", GroupingScreen.ScreenName);

            prefabFactory.CreateSpacer();

            CreateChangeScreenButton("<b>Diagnostics</b> and scene analysis...", DiagnosticsScreen.ScreenName);
            CreateChangeScreenButton("<b>Options</b>...", OptionsScreen.ScreenName);
            CreateChangeScreenButton("<b>Logging</b>...", LoggingScreen.ScreenName);
            CreateChangeScreenButton("<b>Defaults</b>...", DefaultsScreen.ScreenName);

            prefabFactory.CreateSpacer();

            CreateChangeScreenButton("Built-in Help", HelpScreen.ScreenName);
            var helpButton = prefabFactory.CreateButton("[Browser] Online help");
            helpButton.button.onClick.AddListener(() => Application.OpenURL("https://github.com/acidbubbles/vam-timeline/wiki"));

            var hubButton = prefabFactory.CreateButton("[Browser] Virt-A-Mate Hub");
            hubButton.button.onClick.AddListener(() => Application.OpenURL("https://hub.virtamate.com/resources/timeline.94/"));

            prefabFactory.CreateSpacer();

            var patreonBtn = prefabFactory.CreateButton("[Browser] Support me on Patreon!");
            patreonBtn.textColor = new Color(0.97647f, 0.40784f, 0.32941f);
            patreonBtn.buttonColor = Color.white;
            patreonBtn.button.onClick.AddListener(() => Application.OpenURL("https://www.patreon.com/acidbubbles"));
        }
    }
}

