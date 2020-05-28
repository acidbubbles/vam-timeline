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
    public class AtomAnimationUIManager : IDisposable
    {
        protected IAtomPlugin _plugin;
        private AtomAnimationBaseUI _current;
        private AnimationControlPanel _controlPanel;
        private bool _uiRefreshScheduled;
        private bool _uiRefreshInProgress;
        private bool _uiRefreshInvalidated;
        private bool _uiRefreshInvokeAnimationModified;
        private string _currentScreen;
        private readonly UnityEvent _screenChanged = new UnityEvent();

        public AtomAnimationUIManager(IAtomPlugin plugin)
        {
            _plugin = plugin;
        }

        public void Init()
        {
            // Left side
            InitAnimationSelectorUI(false);

            InitControlPanelUI(false);

            // Right side
            InitTabs();

            ChangeScreen(GetDefaultScreen());
        }

        private void InitTabs()
        {
            var screens = new[]{
                AtomAnimationSettingsUI.ScreenName,
                AtomAnimationTargetsUI.ScreenName,
                AtomAnimationEditUI.ScreenName,
                AtomAnimationBulkUI.ScreenName,
                AtomAnimationAdvancedUI.ScreenName,
                AtomAnimationLockedUI.ScreenName
            };

            // TODO: Extract in a component
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
                btn.label = changeTo;

                btn.gameObject.transform.SetParent(group.transform, false);
                btn.button.onClick.AddListener(() =>
                {
                    ChangeScreen(changeTo);
                });

                _screenChanged.AddListener(() => {
                    var selected = _currentScreen == changeTo;
                    btn.button.interactable = !selected;
                });
            }
        }

        private void ChangeScreen(string screen)
        {
            _currentScreen = screen;
            _plugin.LockedJSON.valNoCallback = screen == AtomAnimationLockedUI.ScreenName;
            _screenChanged.Invoke();
            RefreshCurrentUI(false);
            // TODO: Highlight button
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

        private List<string> ListAvailableScreens()
        {
            var list = new List<string>();
            if (_plugin.Animation == null || _plugin.Animation.Current == null) return list;
            return list;
        }

        public string GetDefaultScreen()
        {
            if (_plugin.Animation == null || _plugin.LockedJSON.val)
                return AtomAnimationLockedUI.ScreenName;
            else if (_plugin.Animation.IsEmpty())
                return AtomAnimationTargetsUI.ScreenName;
            else
                return AtomAnimationEditUI.ScreenName;
        }

        public void AnimationModified()
        {
            if (_plugin.Animation == null) return;
            if (_plugin.LockedJSON.val && _currentScreen != AtomAnimationLockedUI.ScreenName)
                ChangeScreen(AtomAnimationLockedUI.ScreenName);
            if (!_plugin.LockedJSON.val && _currentScreen == AtomAnimationLockedUI.ScreenName)
                ChangeScreen(GetDefaultScreen());

            RefreshCurrentUI(true);

            _controlPanel.Bind(_plugin.Animation.Current);
            _controlPanel.SetScrubberPosition(_plugin.Animation.Time, true);
        }

        public void AnimationFrameUpdated()
        {
            RefreshCurrentUI(false);

            _controlPanel.SetScrubberPosition(_plugin.Animation.Time, true);
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
                case AtomAnimationEditUI.ScreenName:
                    _current = new AtomAnimationEditUI(_plugin);
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

            yield return 0;

            _uiRefreshInProgress = false;

            if (_uiRefreshInvalidated)
            {
                _uiRefreshInvalidated = false;
                _uiRefreshScheduled = true;
                _plugin.StartCoroutine(RefreshCurrentUIDeferred(_currentScreen));
            }
        }

        public void UpdatePlaying()
        {
            if (_current == null) return;
            _current.UpdatePlaying();
            if (!_plugin.LockedJSON.val)
                _controlPanel.SetScrubberPosition(_plugin.Animation.Time, false);
        }

        public void Dispose()
        {
            _screenChanged.RemoveAllListeners();
        }
    }
}

