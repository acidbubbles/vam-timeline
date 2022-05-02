using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace VamTimeline
{
    public class ScreensManager : MonoBehaviour
    {
        public class ScreenChangedEventArgs { public string screenName; public object screenArg; }
        public class ScreenChangedEvent : UnityEvent<ScreenChangedEventArgs> { }

        public static ScreensManager Configure(GameObject go)
        {
            var content = VamPrefabFactory.CreateScrollRect(go);
            return content.gameObject.AddComponent<ScreensManager>();
        }

        public readonly ScreenChangedEvent onScreenChanged = new ScreenChangedEvent();
        public Transform popupParent;
        private IAtomPlugin _plugin;
        private ScreenBase _current;
        private bool _uiRefreshScheduled;
        private bool _uiRefreshInProgress;
        private bool _uiRefreshInvalidated;
        private string _currentScreen;
        private object _currentScreenArg;
        private Coroutine _uiRefreshCoroutine;

        public void Bind(IAtomPlugin plugin, string defaultScreen)
        {
            _plugin = plugin;
            _currentScreen = defaultScreen;
            _currentScreenArg = null;
            if (enabled && plugin.animation != null)
                ChangeScreen(GetDefaultScreen(), null);
        }

        public void ReloadScreen()
        {
            var screen = _currentScreen;
            DestroyImmediate(_current.gameObject);
            _current = null;
            _currentScreen = null;
            ChangeScreen(screen, _currentScreenArg);
        }

        public void ChangeScreen(ScreenBase.ScreenChangeRequestEventArgs args)
        {
            ChangeScreen(args.screenName, args.screenArg);
        }

        public void ChangeScreen(string screen, object screenArg)
        {
            if (SuperController.singleton.gameMode != SuperController.GameMode.Edit || !_plugin.isActiveAndEnabled)
            {
                _currentScreen = LockedScreen.ScreenName;
                _currentScreenArg = null;
            }
            else
            {
                _currentScreen = screen;
                _currentScreenArg = screenArg;
            }
            if (!isActiveAndEnabled) return;
            RefreshCurrentUI(_currentScreen);
        }

        public string GetDefaultScreen()
        {
            if (SuperController.singleton.gameMode != SuperController.GameMode.Edit)
                return LockedScreen.ScreenName;
            if (_currentScreen != null && _currentScreen != LockedScreen.ScreenName)
                return _currentScreen;
            if (_plugin.animation.clips.Count > 1)
                return AnimationsScreen.ScreenName;
            return TargetsScreen.ScreenName;
        }

        private void RefreshCurrentUI(string screen)
        {
            if (_plugin.animation == null) return;

            if (_uiRefreshInProgress)
            {
                _uiRefreshInvalidated = true;
            }
            else if (!_uiRefreshScheduled)
            {
                _uiRefreshScheduled = true;
                _uiRefreshCoroutine = StartCoroutine(RefreshCurrentUIDeferred(screen));
            }
        }

        private IEnumerator RefreshCurrentUIDeferred(string screen)
        {
            // Let every event trigger a UI refresh
            yield return 0;

            _uiRefreshScheduled = false;

            // Cannot proceed
            if (_plugin == null || _plugin.animation == null || _plugin.animationEditContext.current == null) yield break;

            // Same UI, just refresh
            if (_current != null && _current.screenId == screen)
            {
                _uiRefreshCoroutine = null;
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
                    SuperController.LogError($"Timeline.{nameof(ScreensManager)}.{nameof(RefreshCurrentUIDeferred)} (while removing {_current.screenId}): {exc}");
                }

                _current = null;
            }

            yield return 0;

            var screenContainer = CreateScreenContainer();

            switch (screen)
            {
                case OptionsScreen.ScreenName:
                    _current = screenContainer.AddComponent<OptionsScreen>();
                    break;
                case AddRemoveTargetsScreen.ScreenName:
                    _current = screenContainer.AddComponent<AddRemoveTargetsScreen>();
                    break;
                case TargetsScreen.ScreenName:
                    _current = screenContainer.AddComponent<TargetsScreen>();
                    break;
                case AnimationsScreen.ScreenName:
                    _current = screenContainer.AddComponent<AnimationsScreen>();
                    break;
                case BulkScreen.ScreenName:
                    _current = screenContainer.AddComponent<BulkScreen>();
                    break;
                case AdvancedKeyframeToolsScreen.ScreenName:
                    _current = screenContainer.AddComponent<AdvancedKeyframeToolsScreen>();
                    break;
                case MocapScreen.ScreenName:
                    _current = screenContainer.AddComponent<MocapScreen>();
                    break;
                case RecordScreen.ScreenName:
                    _current = screenContainer.AddComponent<RecordScreen>();
                    break;
                case ReduceScreen.ScreenName:
                    _current = screenContainer.AddComponent<ReduceScreen>();
                    break;
                case MoreScreen.ScreenName:
                    _current = screenContainer.AddComponent<MoreScreen>();
                    break;
                case ImportExportScreen.ScreenName:
                    _current = screenContainer.AddComponent<ImportExportScreen>();
                    break;
                case EditAnimationScreen.ScreenName:
                    _current = screenContainer.AddComponent<EditAnimationScreen>();
                    break;
                case SequencingScreen.ScreenName:
                    _current = screenContainer.AddComponent<SequencingScreen>();
                    break;
                case AddAnimationsScreen.ScreenName:
                    _current = screenContainer.AddComponent<AddAnimationsScreen>();
                    break;
                case AddClipScreen.ScreenName:
                    _current = screenContainer.AddComponent<AddClipScreen>();
                    break;
                case AddLayerScreen.ScreenName:
                    _current = screenContainer.AddComponent<AddLayerScreen>();
                    break;
                case AddSegmentScreen.ScreenName:
                    _current = screenContainer.AddComponent<AddSegmentScreen>();
                    break;
                case AddSharedSegmentScreen.ScreenName:
                    _current = screenContainer.AddComponent<AddSharedSegmentScreen>();
                    break;
                case ManageAnimationsScreen.ScreenName:
                    _current = screenContainer.AddComponent<ManageAnimationsScreen>();
                    break;
                case LockedScreen.ScreenName:
                    _current = screenContainer.AddComponent<LockedScreen>();
                    break;
                case DiagnosticsScreen.ScreenName:
                    _current = screenContainer.AddComponent<DiagnosticsScreen>();
                    break;
                case HelpScreen.ScreenName:
                    _current = screenContainer.AddComponent<HelpScreen>();
                    break;
                case ControllerTargetSettingsScreen.ScreenName:
                    _current = screenContainer.AddComponent<ControllerTargetSettingsScreen>();
                    break;
                case LoggingScreen.ScreenName:
                    _current = screenContainer.AddComponent<LoggingScreen>();
                    break;
                default:
                    throw new InvalidOperationException($"Unknown screen {screen}");
            }

            try
            {
                _current.transform.SetParent(transform, false);
                _current.popupParent = popupParent;
                _current.onScreenChangeRequested.AddListener(ChangeScreen);
                _current.onScreenReloadRequested.AddListener(ReloadScreen);
                _current.Init(_plugin, _currentScreenArg);
                onScreenChanged.Invoke(new ScreenChangedEventArgs { screenName = _current.screenId, screenArg = _currentScreenArg });
            }
            catch (Exception exc)
            {
                SuperController.LogError($"Timeline.{nameof(ScreensManager)}.{nameof(RefreshCurrentUIDeferred)} (while initializing {_current.screenId}): {exc}");
            }

            yield return 0;

            _uiRefreshInProgress = false;

            _uiRefreshCoroutine = null;

            if (_uiRefreshInvalidated)
            {
                _uiRefreshInvalidated = false;
                _uiRefreshScheduled = true;
                _uiRefreshCoroutine = StartCoroutine(RefreshCurrentUIDeferred(_currentScreen));
            }
        }

        private GameObject CreateScreenContainer()
        {
            var go = new GameObject();
            go.transform.SetParent(transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.pivot = new Vector2(0, 1);

            var group = go.AddComponent<VerticalLayoutGroup>();
            group.spacing = 10f;
            group.childControlHeight = true;
            group.childForceExpandHeight = false;

            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return go;
        }

        public void OnEnable()
        {
            if (_plugin == null) return;
            // TODO: In both instances in this file, use "last screen" with it's arg if there was one
            ChangeScreen(GetDefaultScreen(), null);
        }

        public void OnDisable()
        {
            if(_current != null) Destroy(_current.gameObject);
            _current = null;
            if (_uiRefreshCoroutine != null) StopCoroutine(_uiRefreshCoroutine);
            _uiRefreshInProgress = false;
            _uiRefreshInvalidated = false;
            _uiRefreshScheduled = false;
        }

        public void OnDestroy()
        {
            onScreenChanged.RemoveAllListeners();
        }
    }
}

