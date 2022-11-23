using System.Text.RegularExpressions;

namespace VamTimeline
{
    public class LoggingScreen : ScreenBase
    {
        public const string ScreenName = "Logging";

        public override string screenId => ScreenName;

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            CreateChangeScreenButton("<b><</b> <i>Back</i>", MoreScreen.ScreenName);

            InitLoggingUI();
        }

        private void InitLoggingUI()
        {
            prefabFactory.CreateHeader("Quick links", 1);

            var toggleAllJSON = new JSONStorableBool("Toggle all", false)
            {
                valNoCallback = plugin.logger.clearOnPlay || plugin.logger.general || plugin.logger.triggersReceived || plugin.logger.sequencing || plugin.logger.peersSync || plugin.logger.triggersInvoked
            };
            prefabFactory.CreateToggle(toggleAllJSON);

            prefabFactory.CreateHeader("Logging inclusions", 1);

            var generalJSON = new JSONStorableBool("General", false, val => plugin.logger.general = val) { valNoCallback = plugin.logger.general };
            prefabFactory.CreateToggle(generalJSON);

            var triggersReceivedJSON = new JSONStorableBool("Triggers (Received)", false, val => plugin.logger.triggersReceived = val) { valNoCallback = plugin.logger.triggersReceived };
            prefabFactory.CreateToggle(triggersReceivedJSON);

            var triggersInvokedJSON = new JSONStorableBool("Triggers (Invoked)", false, val => plugin.logger.triggersInvoked = val) { valNoCallback = plugin.logger.triggersInvoked };
            prefabFactory.CreateToggle(triggersInvokedJSON);

            var sequencingJSON = new JSONStorableBool("Sequencing", false, val => plugin.logger.sequencing= val) { valNoCallback = plugin.logger.sequencing};
            prefabFactory.CreateToggle(sequencingJSON);

            var peerSyncJSON = new JSONStorableBool("Peer syncing", false, val => plugin.logger.peersSync = val) { valNoCallback = plugin.logger.peersSync };
            prefabFactory.CreateToggle(peerSyncJSON);

            var filterJSON = new JSONStorableString("Filter", "", val =>
            {
                if (string.IsNullOrEmpty(val))
                {
                    plugin.logger.filter = null;
                    return;
                }
                var regex = new Regex(val, RegexOptions.Compiled);
                plugin.logger.filter = regex;
            }){valNoCallback = plugin.logger.filter?.ToString()};
            prefabFactory.CreateTextInput(filterJSON);

            prefabFactory.CreateSpacer();
            prefabFactory.CreateHeader("Logging options", 1);

            var clearOnPlayJSON = new JSONStorableBool("Clear on play", false, val => plugin.logger.clearOnPlay = val) { valNoCallback = plugin.logger.clearOnPlay };
            prefabFactory.CreateToggle(clearOnPlayJSON);

            var showCurrentAnimationJSON = new JSONStorableBool("Show what's playing in help text", false, val => plugin.logger.showPlayInfoInHelpText = val) { valNoCallback = plugin.logger.showPlayInfoInHelpText };
            prefabFactory.CreateToggle(showCurrentAnimationJSON);

            prefabFactory.CreateSpacer();
            prefabFactory.CreateHeader("Sync to other atoms", 1);

            var syncOtherAtoms = prefabFactory.CreateButton("Sync logging settings on all atoms");
            syncOtherAtoms.button.onClick.AddListener(() => plugin.peers.SendLoggingSettings());

            toggleAllJSON.setCallbackFunction = val =>
            {
                clearOnPlayJSON.val = val;
                generalJSON.val = val;
                triggersInvokedJSON.val = val;
                triggersReceivedJSON.val = val;
                sequencingJSON.val = val;
                peerSyncJSON.val = val;
            };
        }
    }
}

