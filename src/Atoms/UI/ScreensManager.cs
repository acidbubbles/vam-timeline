using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class ScreensManager : IDisposable
    {
        protected IAtomPlugin _plugin;
        private ScreenBase _current;
        private AnimationControlPanel _controlPanel;
        private bool _uiRefreshScheduled;
        private bool _uiRefreshInProgress;
        private bool _uiRefreshInvalidated;
        private string _currentScreen;
        private readonly UnityEvent _screenChanged = new UnityEvent();

        public ScreensManager(IAtomPlugin plugin)
        {
            _plugin = plugin;
        }

        public void Init()
        {
            // Left side
            // TODO: This should be in the control panel
            InitAnimationSelectorUI(false);

            InitControlPanelUI(false);

            // Right side
            InitTabs();
        }

        private void InitTabs()
        {
            var screens = new[]{
                SettingsScreen.ScreenName,
                // TODO: Move this inside the "Edit" menu (and make Edit go to that screen when empty)
                TargetsScreen.ScreenName,
                EditScreen.ScreenName,
                BulkScreen.ScreenName,
                AdvancedScreen.ScreenName,
                PerformanceScreen.ScreenName
            };

            var tabsContainer = _plugin.CreateSpacer(true);
            tabsContainer.height = 100f;

            var group = tabsContainer.gameObject.AddComponent<GridLayoutGroup>();
            group.constraint = GridLayoutGroup.Constraint.Flexible;
            group.constraintCount = screens.Length;
            group.spacing = Vector2.zero;
            group.cellSize = new Vector2(512f / 3f, 50f);
            group.childAlignment = TextAnchor.MiddleCenter;

            foreach (var screen in screens)
            {
                var changeTo = screen;
                var btn = UnityEngine.Object.Instantiate(_plugin.Manager.configurableButtonPrefab).GetComponent<UIDynamicButton>();
                btn.gameObject.transform.SetParent(group.transform, false);
                btn.label = changeTo;

                btn.button.onClick.AddListener(() =>
                {
                    ChangeScreen(changeTo);
                });

                _screenChanged.AddListener(() =>
                {
                    var selected = _currentScreen == changeTo;
                    btn.button.interactable = !selected;
                });
            }
        }

        private void ChangeScreen(string screen)
        {
            _currentScreen = screen;
            _plugin.LockedJSON.valNoCallback = screen == PerformanceScreen.ScreenName;
            _screenChanged.Invoke();
            RefreshCurrentUI();
        }

        protected void InitAnimationSelectorUI(bool rightSide)
        {
            var animationUI = _plugin.CreateScrollablePopup(_plugin.AnimationDisplayJSON, rightSide);
            animationUI.label = "Animation";
            animationUI.popupPanelHeight = 800f;
        }

        private void InitControlPanelUI(bool rightSide)
        {
            var controlPanelContainer = _plugin.CreateSpacer(rightSide);
            controlPanelContainer.height = 500f;
            _controlPanel = controlPanelContainer.gameObject.AddComponent<AnimationControlPanel>();
            _controlPanel.Bind(_plugin);
        }

        public void Bind(AtomAnimation animation)
        {
            _controlPanel.Bind(animation);
            ChangeScreen(GetDefaultScreen());
        }

        private List<string> ListAvailableScreens()
        {
            var list = new List<string>();
            if (_plugin.Animation == null || _plugin.Animation.Current == null) return list;
            return list;
        }

        public string GetDefaultScreen()
        {
            if (_plugin.Animation == null || _plugin.LockedJSON.val)
                return PerformanceScreen.ScreenName;
            else if (_plugin.Animation.IsEmpty())
                return WelcomeScreen.ScreenName;
            else
                return EditScreen.ScreenName;
        }

        public void UpdateLocked(bool isLocked)
        {
            if (isLocked && _currentScreen != PerformanceScreen.ScreenName)
                ChangeScreen(PerformanceScreen.ScreenName);
            if (!isLocked && _currentScreen == PerformanceScreen.ScreenName)
                ChangeScreen(GetDefaultScreen());
        }

        public void RefreshCurrentUI()
        {
            if (_plugin.Animation == null) return;

            if (_uiRefreshInProgress)
                _uiRefreshInvalidated = true;
            else if (!_uiRefreshScheduled)
            {
                _uiRefreshScheduled = true;
                _plugin.StartCoroutine(RefreshCurrentUIDeferred(_currentScreen));
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
                    SuperController.LogError($"VamTimeline.{nameof(ScreensManager)}.{nameof(RefreshCurrentUIDeferred)} (while removing {_current.Name}): {exc}");
                }

                _current = null;
            }

            yield return 0;

            // Create new screen
            switch (screen)
            {
                case SettingsScreen.ScreenName:
                    _current = new SettingsScreen(_plugin);
                    break;
                case TargetsScreen.ScreenName:
                    _current = new TargetsScreen(_plugin);
                    break;
                case EditScreen.ScreenName:
                    _current = new EditScreen(_plugin);
                    break;
                case BulkScreen.ScreenName:
                    _current = new BulkScreen(_plugin);
                    break;
                case AdvancedScreen.ScreenName:
                    _current = new AdvancedScreen(_plugin);
                    break;
                case PerformanceScreen.ScreenName:
                    _current = new PerformanceScreen(_plugin);
                    break;
                case WelcomeScreen.ScreenName:
                    _current = new WelcomeScreen(_plugin);
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
                SuperController.LogError($"VamTimeline.{nameof(ScreensManager)}.{nameof(RefreshCurrentUIDeferred)} (while initializing {_current.Name}): {exc}");
            }

            yield return 0;

            _uiRefreshInProgress = false;

            if (_uiRefreshInvalidated)
            {
                _uiRefreshInvalidated = false;
                _uiRefreshScheduled = true;
                _plugin.StartCoroutine(RefreshCurrentUIDeferred(_currentScreen));
            }
        }

        public void Enable()
        {
            ChangeScreen(GetDefaultScreen());
        }

        public void Disable()
        {
            _current?.Dispose();
            _current = null;
            _currentScreen = null;
        }

        public void Dispose()
        {
            _screenChanged.RemoveAllListeners();
        }
    }
}

