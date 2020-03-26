using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
        private JSONStorableStringChooser _screens;
        private UIDynamicPopup _screenUI;
        private bool _uiRefreshScheduled;
        private bool _uiRefreshInProgress;
        private bool _uiRefreshInvalidated;

        public AtomAnimationUIManager(IAtomPlugin plugin)
        {
            _plugin = plugin;
        }

        public void Init()
        {
            _screens = new JSONStorableStringChooser(
                "Screen",
                ListAvailableScreens(),
                GetDefaultScreen(),
                "Tab",
                (string screen) =>
                {
                    _plugin.LockedJSON.valNoCallback = screen == AtomAnimationLockedUI.ScreenName;
                    RefreshCurrentUI();
                }
            );
            _screenUI = _plugin.CreateScrollablePopup(_screens);
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
            list.Add(AtomAnimationHelpUI.ScreenName);
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
            _screens.choices = ListAvailableScreens();
            if (_plugin.LockedJSON.val && _screens.val != AtomAnimationLockedUI.ScreenName)
                _screens.valNoCallback = AtomAnimationLockedUI.ScreenName;
            if (!_plugin.LockedJSON.val && _screens.val == AtomAnimationLockedUI.ScreenName)
                _screens.valNoCallback = GetDefaultScreen();
            RefreshCurrentUI(c => c.AnimationModified());
        }

        public void AnimationFrameUpdated()
        {
            RefreshCurrentUI(c => c.AnimationFrameUpdated());
        }

        public void UIUpdated()
        {
            if (_plugin.Animation == null) return;
            RefreshCurrentUI(c => c.UIUpdated());
        }

        public void RefreshCurrentUI(Action<AtomAnimationBaseUI> fn = null)
        {
            if (_plugin.Animation == null) return;

            if (_uiRefreshInProgress)
                _uiRefreshInvalidated = true;
            else if (!_uiRefreshScheduled)
            {
                _uiRefreshScheduled = true;
                _plugin.StartCoroutine(RefreshCurrentUIDeferred(fn, _screens.val));
            }
        }

        private IEnumerator RefreshCurrentUIDeferred(Action<AtomAnimationBaseUI> fn, string screen)
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
                    fn?.Invoke(_current);
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
                case AtomAnimationHelpUI.ScreenName:
                    _current = new AtomAnimationHelpUI(_plugin);
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
                _plugin.StartCoroutine(RefreshCurrentUIDeferred(fn, _screens.val));
            }
        }

        public void UpdatePlaying()
        {
            if (_current == null) return;
            _current.UpdatePlaying();
        }
    }
}

