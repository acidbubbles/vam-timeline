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
                    _plugin.LockedJSON.val = screen == AtomAnimationLockedUI.ScreenName;
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
            if (_plugin.Animation.Current.TargetControllers.Count > 0)
                list.Add(AtomAnimationControllersUI.ScreenName);
            if (_plugin.Animation.Current.TargetFloatParams.Count > 0)
                list.Add(AtomAnimationFloatParamsUI.ScreenName);
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
                return AtomAnimationSettingsUI.ScreenName;
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
            RefreshCurrentUI(() => _current.AnimationModified());
        }

        public void AnimationFrameUpdated()
        {
            RefreshCurrentUI(() => _current.AnimationFrameUpdated());
        }

        public void UIUpdated()
        {
            if (_plugin.Animation == null) return;
            RefreshCurrentUI(() => _current.UIUpdated());
        }

        public void RefreshCurrentUI(Action fn = null)
        {
            if (_plugin.Animation == null) return;

            _plugin.StartCoroutine(RefreshCurrentUIDeferred(fn));
        }

        private IEnumerator RefreshCurrentUIDeferred(Action fn)
        {
            yield return new WaitForEndOfFrame();
            if (_plugin == null || _plugin.Animation == null || _plugin.Animation.Current == null) yield break;
            if (_current == null || _current.Name != _screens.val)
            {
                if (_current != null)
                    _current.Remove();

                switch (_screens.val)
                {
                    case AtomAnimationSettingsUI.ScreenName:
                        _current = new AtomAnimationSettingsUI(_plugin);
                        break;
                    case AtomAnimationControllersUI.ScreenName:
                        _current = new AtomAnimationControllersUI(_plugin);
                        break;
                    case AtomAnimationFloatParamsUI.ScreenName:
                        _current = new AtomAnimationFloatParamsUI(_plugin);
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
                        throw new InvalidOperationException($"Unknown screen {_screens.val}");
                }

                try
                {
                    _current.Init();
                    _current.AnimationModified();
                }
                catch (Exception exc)
                {
                    SuperController.LogError($"VamTimeline.AtomAnimationUIManager.RefreshCurrentUIDefered ({_screens.val}): " + exc.ToString());
                }

                // Hack to avoid having the drop down shown underneath new controls
                _screenUI.popup.Toggle();
                _screenUI.popup.Toggle();
            }
            else
            {
                fn?.Invoke();
            }
        }

        public void UpdatePlaying()
        {
            if (_current == null) return;
            _current.UpdatePlaying();
        }
    }
}

