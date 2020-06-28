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
    public class ScreensManager : MonoBehaviour
    {
        public static ScreensManager Configure(GameObject go)
        {
            var group = go.AddComponent<VerticalLayoutGroup>();
            return go.AddComponent<ScreensManager>();
        }

        private readonly UnityEvent _screenChanged = new UnityEvent();
        private IAtomPlugin _plugin;
        private ScreenBase _current;
        private bool _uiRefreshScheduled;
        private bool _uiRefreshInProgress;
        private bool _uiRefreshInvalidated;
        private string _currentScreen;
        private string _defaultScreen;

        public void Bind(IAtomPlugin plugin)
        {
            _plugin = plugin;
            InitTabs();
            OnEnable();
        }

        private void InitTabs()
        {
            var screens = new[]{
                EditScreen.ScreenName,
                ClipsScreen.ScreenName,
                MoreScreen.ScreenName,
                PerformanceScreen.ScreenName
            };

            var tabsContainer = new GameObject("Tabs");
            tabsContainer.transform.SetParent(transform, false);

            tabsContainer.AddComponent<LayoutElement>().minHeight = 60f;

            var group = tabsContainer.AddComponent<GridLayoutGroup>();
            group.constraint = GridLayoutGroup.Constraint.Flexible;
            group.constraintCount = screens.Length;
            group.spacing = Vector2.zero;
            group.cellSize = new Vector2(512f / 4f, 50f);
            group.childAlignment = TextAnchor.MiddleCenter;

            foreach (var screen in screens)
            {
                var changeTo = screen;
                var btn = Instantiate(_plugin.manager.configurableButtonPrefab).GetComponent<UIDynamicButton>();
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
            _currentScreen = _defaultScreen = screen;
            _plugin.lockedJSON.val = screen == PerformanceScreen.ScreenName;
            _screenChanged.Invoke();
            RefreshCurrentUI();
        }

        private List<string> ListAvailableScreens()
        {
            var list = new List<string>();
            if (_plugin.animation == null || _plugin.animation.current == null) return list;
            return list;
        }

        public string GetDefaultScreen()
        {
            if (_defaultScreen != null)
                return _defaultScreen;
            else if (_plugin.animation == null || _plugin.lockedJSON.val)
                return PerformanceScreen.ScreenName;
            else
                return EditScreen.ScreenName;
        }

        public void UpdateLocked(bool isLocked)
        {
            if (isLocked)
            {
                if (_currentScreen != PerformanceScreen.ScreenName)
                    ChangeScreen(PerformanceScreen.ScreenName);
            }
            else
            {
                if (_currentScreen == PerformanceScreen.ScreenName)
                    ChangeScreen(GetDefaultScreen());
            }
        }

        public void RefreshCurrentUI()
        {
            if (_plugin.animation == null) return;

            if (_uiRefreshInProgress)
                _uiRefreshInvalidated = true;
            else if (!_uiRefreshScheduled)
                StartCoroutine(RefreshCurrentUIDeferred(_currentScreen));
        }

        private IEnumerator RefreshCurrentUIDeferred(string screen)
        {
            _uiRefreshScheduled = true;

            // Let every event trigger a UI refresh
            yield return 0;

            _uiRefreshScheduled = false;

            // Cannot proceed
            if (_plugin == null || _plugin.animation == null || _plugin.animation.current == null) yield break;

            // Same UI, just refresh
            if (_current != null && _current.screenId == screen)
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
                    Destroy(_current.gameObject);
                }
                catch (Exception exc)
                {
                    SuperController.LogError($"VamTimeline.{nameof(ScreensManager)}.{nameof(RefreshCurrentUIDeferred)} (while removing {_current.screenId}): {exc}");
                }

                _current = null;
            }

            yield return 0;

            // Create new screen
            var go = new GameObject();
            go.transform.SetParent(transform, false);
            var group = go.AddComponent<VerticalLayoutGroup>();
            group.spacing = 10f;
            group.childControlHeight = false;

            switch (screen)
            {
                case SettingsScreen.ScreenName:
                    _current = go.AddComponent<SettingsScreen>();
                    break;
                case TargetsScreen.ScreenName:
                    _current = go.AddComponent<TargetsScreen>();
                    break;
                case EditScreen.ScreenName:
                    _current = go.AddComponent<EditScreen>();
                    break;
                case ClipsScreen.ScreenName:
                    _current = go.AddComponent<ClipsScreen>();
                    break;
                case BulkScreen.ScreenName:
                    _current = go.AddComponent<BulkScreen>();
                    break;
                case AdvancedScreen.ScreenName:
                    _current = go.AddComponent<AdvancedScreen>();
                    break;
                case MocapScreen.ScreenName:
                    _current = go.AddComponent<MocapScreen>();
                    break;
                case EditLayersScreen.ScreenName:
                    _current = go.AddComponent<EditLayersScreen>();
                    break;
                case MoreScreen.ScreenName:
                    _current = go.AddComponent<MoreScreen>();
                    break;
                case EditAnimationScreen.ScreenName:
                    _current = go.AddComponent<EditAnimationScreen>();
                    break;
                case EditSequenceScreen.ScreenName:
                    _current = go.AddComponent<EditSequenceScreen>();
                    break;
                case AddAnimationScreen.ScreenName:
                    _current = go.AddComponent<AddAnimationScreen>();
                    break;
                case ManageAnimationsScreen.ScreenName:
                    _current = go.AddComponent<ManageAnimationsScreen>();
                    break;
                case PerformanceScreen.ScreenName:
                    _current = go.AddComponent<PerformanceScreen>();
                    break;
                case HelpScreen.ScreenName:
                    _current = go.AddComponent<HelpScreen>();
                    break;
                default:
                    throw new InvalidOperationException($"Unknown screen {screen}");
            }

            try
            {
                _current.transform.SetParent(transform, false);
                _current.onScreenChangeRequested.AddListener(ChangeScreen);
                _current.Init(_plugin);
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(ScreensManager)}.{nameof(RefreshCurrentUIDeferred)} (while initializing {_current.screenId}): {exc}");
            }

            yield return 0;

            _uiRefreshInProgress = false;

            if (_uiRefreshInvalidated)
            {
                _uiRefreshInvalidated = false;
                _uiRefreshScheduled = true;
                StartCoroutine(RefreshCurrentUIDeferred(_currentScreen));
            }
        }

        public void OnEnable()
        {
            ChangeScreen(GetDefaultScreen());
        }

        public void OnDisable()
        {
            Destroy(_current?.gameObject);
            _current = null;
            _currentScreen = null;
        }

        public void OnDestroy()
        {
            _screenChanged.RemoveAllListeners();
        }
    }
}

