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
                valNoCallback = plugin.logger.clearOnPlay || plugin.logger.general || plugin.logger.triggers || plugin.logger.sequencing || plugin.logger.peersSync
            };
            prefabFactory.CreateToggle(toggleAllJSON);

            prefabFactory.CreateHeader("Logging inclusions", 1);

            var generalJSON = new JSONStorableBool("General", false, val => plugin.logger.general = val) { valNoCallback = plugin.logger.general };
            prefabFactory.CreateToggle(generalJSON);

            var triggersJSON = new JSONStorableBool("Triggers", false, val => plugin.logger.triggers = val) { valNoCallback = plugin.logger.triggers };
            prefabFactory.CreateToggle(triggersJSON);

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

            prefabFactory.CreateSpacer();
            prefabFactory.CreateHeader("Sync to other atoms", 1);

            var syncOtherAtoms = prefabFactory.CreateButton("Sync logging settings on all atoms");
            syncOtherAtoms.button.onClick.AddListener(() => plugin.peers.SendLoggingSettings());

            toggleAllJSON.setCallbackFunction = val =>
            {
                clearOnPlayJSON.val = val;
                generalJSON.val = val;
                triggersJSON.val = val;
                sequencingJSON.val = val;
                peerSyncJSON.val = val;
            };
        }
    }
}

