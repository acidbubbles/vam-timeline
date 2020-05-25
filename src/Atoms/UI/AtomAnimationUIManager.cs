using System;
using System.Collections;
using System.Collections.Generic;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class AtomAnimationUIManager
    {
        protected IAtomPlugin _plugin;
        private AtomAnimationBaseUI _current;
        private JSONStorableStringChooser _screensJSON;
        private UIDynamicPopup _screenUI;
        private DopeSheet _dopeSheet;
        private bool _uiRefreshScheduled;
        private bool _uiRefreshInProgress;
        private bool _uiRefreshInvalidated;
        private bool _uiRefreshInvokeAnimationModified;

        public AtomAnimationUIManager(IAtomPlugin plugin)
        {
            _plugin = plugin;
        }

        public void Init()
        {
            // Left side
            InitAnimationSelectorUI(false);
            InitDopeSheetUI(false);
            InitPlaybackUI(false);
            InitFrameNavUI(false);

            // Right side
            _screensJSON = new JSONStorableStringChooser(
                "Screen",
                ListAvailableScreens(),
                GetDefaultScreen(),
                "Tab",
                (string screen) =>
                {
                    _plugin.LockedJSON.valNoCallback = screen == AtomAnimationLockedUI.ScreenName;
                    RefreshCurrentUI(false);
                }
            );
            _screenUI = _plugin.CreateScrollablePopup(_screensJSON, true);
        }

        protected void InitAnimationSelectorUI(bool rightSide)
        {
            var animationUI = _plugin.CreateScrollablePopup(_plugin.AnimationDisplayJSON, rightSide);
            animationUI.label = "Animation";
            animationUI.popupPanelHeight = 800f;
        }

        protected void InitDopeSheetUI(bool rightSide)
        {
            var dopeSheetContainer = _plugin.CreateSpacer(rightSide);
            dopeSheetContainer.height = 260f;

            // Replace play, stop, frame nav and scrubber (text field for precise time?)
            // https://docs.blender.org/manual/en/latest/editors/dope_sheet/introduction.html

            _dopeSheet = new DopeSheet(dopeSheetContainer, 520, dopeSheetContainer.height, DopeSheetStyle.Default());
        }

        protected void InitPlaybackUI(bool rightSide)
        {
            var scrubberUI = _plugin.CreateSlider(_plugin.ScrubberJSON);
            scrubberUI.valueFormat = "F3";

            var playUI = _plugin.CreateButton("\u25B6 Play", rightSide);
            playUI.button.onClick.AddListener(() => _plugin.PlayJSON.actionCallback());

            var stopUI = _plugin.CreateButton("\u25A0 Stop", rightSide);
            stopUI.button.onClick.AddListener(() => _plugin.StopJSON.actionCallback());
        }

        protected void InitFrameNavUI(bool rightSide)
        {
            var selectedControllerUI = _plugin.CreateScrollablePopup(_plugin.FilterAnimationTargetJSON, rightSide);
            selectedControllerUI.popupPanelHeight = 600f;

            var nextFrameUI = _plugin.CreateButton("\u2192 Next Frame", rightSide);
            nextFrameUI.button.onClick.AddListener(() => _plugin.NextFrameJSON.actionCallback());

            var previousFrameUI = _plugin.CreateButton("\u2190 Previous Frame", rightSide);
            previousFrameUI.button.onClick.AddListener(() => _plugin.PreviousFrameJSON.actionCallback());
        }

        private List<string> ListAvailableScreens()
        {
            var list = new List<string>();
            if (_plugin.Animation == null || _plugin.Animation.Current == null) return list;
            list.Add(AtomAnimationSettingsUI.ScreenName);
            list.Add(AtomAnimationTargetsUI.ScreenName);
            if (_plugin.Animation.Current.TargetControllers.Count > 0)
                list.Add(AtomAnimationControllersUI.ScreenName);
            if (_plugin.Animation.Current.TargetFloatParams.Count > 0)
                list.Add(AtomAnimationFloatParamsUI.ScreenName);
            if (_plugin.Animation.Current.TargetControllers.Count > 0 || _plugin.Animation.Current.TargetFloatParams.Count > 0)
                list.Add(AtomAnimationBulkUI.ScreenName);
            list.Add(AtomAnimationAdvancedUI.ScreenName);
            list.Add(AtomAnimationLockedUI.ScreenName);
            return list;
        }

        public string GetDefaultScreen()
        {
            if (_plugin.Animation == null || _plugin.LockedJSON.val)
                return AtomAnimationLockedUI.ScreenName;
            else if (_plugin.Animation.IsEmpty())
                return AtomAnimationTargetsUI.ScreenName;
            else
                return AtomAnimationControllersUI.ScreenName;
        }

        public void AnimationModified()
        {
            if (_plugin.Animation == null) return;
            _screensJSON.choices = ListAvailableScreens();
            if (_plugin.LockedJSON.val && _screensJSON.val != AtomAnimationLockedUI.ScreenName)
                _screensJSON.valNoCallback = AtomAnimationLockedUI.ScreenName;
            if (!_plugin.LockedJSON.val && _screensJSON.val == AtomAnimationLockedUI.ScreenName)
                _screensJSON.valNoCallback = GetDefaultScreen();
            RefreshCurrentUI(true);

            // TODO: Highlight current filtered target, and allow selection through dope sheet
            // TODO: Rename Draw, refresh when updated, recreate when animation changed
            _dopeSheet.Bind(_plugin.Animation.Current);
        }

        public void AnimationFrameUpdated()
        {
            RefreshCurrentUI(false);

            _dopeSheet.SetScrubberPosition(_plugin.Animation.Time);
        }

        public void RefreshCurrentUI(bool animationModified)
        {
            if (_plugin.Animation == null) return;

            if (animationModified) _uiRefreshInvokeAnimationModified = true;

            if (_uiRefreshInProgress)
                _uiRefreshInvalidated = true;
            else if (!_uiRefreshScheduled)
            {
                _uiRefreshScheduled = true;
                _plugin.StartCoroutine(RefreshCurrentUIDeferred(_screensJSON.val));
            }
        }

        private IEnumerator RefreshCurrentUIDeferred(string screen)
        {
            // Let every event trigger a UI refresh
            yield return 0;

            _uiRefreshScheduled = false;

            // Cannot proceed
            if (_plugin == null || _plugin.Animation == null || _plugin.Animation.Current == null) yield break;

            // Same UI, just refresh
            if (_current != null && _current.Name == screen)
            {
                try
                {
                    if (_uiRefreshInvokeAnimationModified)
                    {
                        _uiRefreshInvokeAnimationModified = false;
                        _current.AnimationModified();
                    }
                    else
                    {
                        _current.AnimationFrameUpdated();
                    }
                }
                catch (Exception exc)
                {
                    SuperController.LogError($"VamTimeline.{nameof(AtomAnimationUIManager)}.{nameof(RefreshCurrentUIDeferred)} (while refreshing existing {_current.Name}): {exc}");
                }
                yield break;
            }

            // UI Change
            _uiRefreshInProgress = true;

            // Dispose previous
            if (_current != null)
            {
                try
                {
                    _current.Dispose();
                }
                catch (Exception exc)
                {
                    SuperController.LogError($"VamTimeline.{nameof(AtomAnimationUIManager)}.{nameof(RefreshCurrentUIDeferred)} (while removing {_current.Name}): {exc}");
                }

                _current = null;
            }

            yield return 0;

            // Create new screen
            switch (screen)
            {
                case AtomAnimationSettingsUI.ScreenName:
                    _current = new AtomAnimationSettingsUI(_plugin);
                    break;
                case AtomAnimationTargetsUI.ScreenName:
                    _current = new AtomAnimationTargetsUI(_plugin);
                    break;
                case AtomAnimationControllersUI.ScreenName:
                    _current = new AtomAnimationControllersUI(_plugin);
                    break;
                case AtomAnimationFloatParamsUI.ScreenName:
                    _current = new AtomAnimationFloatParamsUI(_plugin);
                    break;
                case AtomAnimationBulkUI.ScreenName:
                    _current = new AtomAnimationBulkUI(_plugin);
                    break;
                case AtomAnimationAdvancedUI.ScreenName:
                    _current = new AtomAnimationAdvancedUI(_plugin);
                    break;
                case AtomAnimationLockedUI.ScreenName:
                    _current = new AtomAnimationLockedUI(_plugin);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown screen {screen}");
            }

            try
            {
                _current.Init();
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AtomAnimationUIManager)}.{nameof(RefreshCurrentUIDeferred)} (while initializing {_current.Name}): {exc}");
            }

            try
            {
                _current.AnimationModified();
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AtomAnimationUIManager)}.{nameof(RefreshCurrentUIDeferred)} (while triggering modified event on {_current.Name}): {exc}");
            }

            // Hack to avoid having the drop down shown underneath new controls
            _screenUI.popup.Toggle();
            _screenUI.popup.Toggle();

            yield return 0;

            _uiRefreshInProgress = false;

            if (_uiRefreshInvalidated)
            {
                _uiRefreshInvalidated = false;
                _uiRefreshScheduled = true;
                _plugin.StartCoroutine(RefreshCurrentUIDeferred(_screensJSON.val));
            }
        }

        public void UpdatePlaying()
        {
            if (_current == null) return;
            _current.UpdatePlaying();
        }
    }
}

