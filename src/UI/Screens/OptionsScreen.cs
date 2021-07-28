using System.Collections.Generic;

namespace VamTimeline
{
    public class OptionsScreen : ScreenBase
    {
        public const string ScreenName = "Settings";
        private JSONStorableBool _lockedJSON;
        private JSONStorableBool _syncWithPeersJSON;
        private JSONStorableBool _syncSubsceneOnlyJSON;
        private JSONStorableFloat _snapJSON;
        private JSONStorableBool _autoKeyframeAllControllersJSON;
        private JSONStorableBool _showPaths;

        public override string screenId => ScreenName;

        #region Init

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            // Right side

            CreateChangeScreenButton("<b><</b> <i>Back</i>", MoreScreen.ScreenName);

            prefabFactory.CreateSpacer();

            InitLockedUI();

            prefabFactory.CreateSpacer();

            InitUseRealTimeUI();

            prefabFactory.CreateSpacer();

            InitDisableSync();
#if (VAM_GT_1_20)
            if (!ReferenceEquals(plugin.containingAtom.containingSubScene, null))
                InitSyncSubsceneOnly();
#endif

            prefabFactory.CreateSpacer();

            InitSnapUI();

            prefabFactory.CreateSpacer();

            InitAutoKeyframeUI();

            prefabFactory.CreateSpacer();

            InitShowPathsUI();

            animationEditContext.onEditorSettingsChanged.AddListener(OnEditorSettingsChanged);
        }

        private void InitLockedUI()
        {
            _lockedJSON = new JSONStorableBool("Lock (ignore in-game edits)", animationEditContext.locked, val => animationEditContext.locked = val);
            prefabFactory.CreateToggle(_lockedJSON);
        }

        private void InitUseRealTimeUI()
        {
            var timeTypeJSON = new JSONStorableStringChooser("Time mode", new List<string> {TimeTypeValues.GameTime.ToString(), TimeTypeValues.RealTime.ToString()}, TimeTypeValues.GameTime.ToString(), "Time mode");
            timeTypeJSON.displayChoices = new List<string>
            {
                "Game time (slows with low fps)",
                "Real time (better for audio sync)"
            };
            timeTypeJSON.valNoCallback = animation.timeMode.ToString();
            timeTypeJSON.setCallbackFunction = val => animation.timeMode = int.Parse(val);
            prefabFactory.CreatePopup(timeTypeJSON, true, true);
        }

        private void InitDisableSync()
        {
            _syncWithPeersJSON = new JSONStorableBool("Sync with other atoms", animation.syncWithPeers, val => animation.syncWithPeers = val);
            prefabFactory.CreateToggle(_syncWithPeersJSON);
        }

        private void InitSyncSubsceneOnly()
        {
            _syncSubsceneOnlyJSON = new JSONStorableBool("Send sync in subscene only", animation.syncSubsceneOnly, val => animation.syncSubsceneOnly = val);
            prefabFactory.CreateToggle(_syncSubsceneOnlyJSON);
        }

        private void InitSnapUI()
        {
            _snapJSON = new JSONStorableFloat("Snap", 0.001f, val => animationEditContext.snap = val.Snap(), 0.1f, 1f)
            {
                valNoCallback = animationEditContext.snap
            };
            var snapUI = prefabFactory.CreateSlider(_snapJSON);
            snapUI.valueFormat = "F3";
        }

        private void InitAutoKeyframeUI()
        {
            _autoKeyframeAllControllersJSON = new JSONStorableBool("Keyframe all controllers at once", animationEditContext.autoKeyframeAllControllers, val => animationEditContext.autoKeyframeAllControllers = val);
            prefabFactory.CreateToggle(_autoKeyframeAllControllersJSON);
        }

        private void InitShowPathsUI()
        {
            _showPaths = new JSONStorableBool(
                "Show selected controllers paths", animationEditContext.showPaths,
                val => animationEditContext.showPaths = val);
            prefabFactory.CreateToggle(_showPaths);
        }

        #endregion

        private void OnEditorSettingsChanged(string _)
        {
            _lockedJSON.valNoCallback = animationEditContext.locked;
            _snapJSON.valNoCallback = animationEditContext.snap;
            _autoKeyframeAllControllersJSON.valNoCallback = animationEditContext.autoKeyframeAllControllers;
        }
    }
}

