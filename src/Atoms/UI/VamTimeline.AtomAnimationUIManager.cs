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
                new List<string>{
                AtomAnimationSettingsUI.ScreenName,
                AtomAnimationLockedUI.ScreenName
                },
                AtomAnimationLockedUI.ScreenName,
                "Tab",
                (string screen) =>
                {
                    _plugin.LockedJSON.val = screen == AtomAnimationLockedUI.ScreenName;
                    RefreshCurrentUI();
                }
            );
            _screenUI = _plugin.CreatePopup(_screens);
        }

        public void AnimationUpdated()
        {
            if (_plugin.Animation == null) return;
            if (_plugin.LockedJSON.val && _screens.val != AtomAnimationLockedUI.ScreenName)
                _screens.valNoCallback = AtomAnimationLockedUI.ScreenName;
            if (!_plugin.LockedJSON.val && _screens.val == AtomAnimationLockedUI.ScreenName)
                _screens.valNoCallback = AtomAnimationSettingsUI.ScreenName;
            RefreshCurrentUI(() => _current.AnimationUpdated());
        }

        public void UIUpdated()
        {
            if (_plugin.Animation == null) return;
            RefreshCurrentUI(() => _current.UIUpdated());
        }

        public void RefreshCurrentUI(Action fn = null)
        {
            if (_plugin.Animation == null) return;

            _plugin.StartCoroutine(RefreshCurrentUIDefered(fn));
        }

        private IEnumerator RefreshCurrentUIDefered(Action fn)
        {
            yield return new WaitForEndOfFrame();
            if (_current == null || _current.Name != _screens.val)
            {
                if (_current != null)
                    _current.Remove();

                switch (_screens.val)
                {
                    case AtomAnimationLockedUI.ScreenName:
                        _current = new AtomAnimationLockedUI(_plugin);
                        break;
                    case AtomAnimationSettingsUI.ScreenName:
                        _current = new AtomAnimationSettingsUI(_plugin);
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown screen {_screens.val}");
                }
                _current.Init();
                _current.AnimationUpdated();

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

