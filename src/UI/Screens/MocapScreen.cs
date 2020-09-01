using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class MocapScreen : ScreenBase
    {
        public const string ScreenName = "Mocap";
        private const string _recordingLabel = "\u25A0 Waiting for recording...";
        private const string _startRecordControllersLabel = "\u25B6 Clear & mocap controllers now";
        private const string _startRecordFloatParamsLabel = "\u25B6 Clear & record float params now";
        private static readonly TimeSpan _importMocapTimeout = TimeSpan.FromSeconds(5);
        private static Coroutine _recordingControllersCoroutine;
        private static Coroutine _recordingFloatParamsCoroutine;
        private static bool _lastResizeAnimation = false;
        private static float _lastReduceMinPosDistance = 0.04f;
        private static float _lastReduceMinRotation = 10f;
        private static float _lastReduceMaxFramesPerSecond = 5f;
        private static bool _importMocapOnLoad;
        private static bool _simplifyFloatParamsOnLoad;

        public override string screenId => ScreenName;

        private JSONStorableStringChooser _importRecordedOptionsJSON;
        private JSONStorableFloat _reduceMinPosDistanceJSON;
        private JSONStorableFloat _reduceMaxFramesPerSecondJSON;
        private JSONStorableFloat _reduceMinRotationJSON;
        private JSONStorableBool _resizeAnimationJSON;
        private UIDynamicButton _importRecordedUI;
        private UIDynamicButton _reduceKeyframesUI;
        private UIDynamicButton _playAndRecordControllersUI;
        private UIDynamicButton _playAndRecordFloatParamsUI;

        public MocapScreen()
            : base()
        {
        }

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            // Right side

            CreateChangeScreenButton("<b><</b> <i>Back</i>", MoreScreen.ScreenName);

            prefabFactory.CreateSpacer();

            if (_importRecordedOptionsJSON == null)
                _importRecordedOptionsJSON = new JSONStorableStringChooser(
                    "Import options",
                     new List<string> { "Keyframe Reduction", "Fixed Frames per Second" },
                     "Keyframe Reduction",
                     "Import options")
                {
                    isStorable = false
                };
            var importRecordedOptionsUI = prefabFactory.CreatePopup(_importRecordedOptionsJSON, false);

            _resizeAnimationJSON = new JSONStorableBool("Resize animation to mocap length", current.targetControllers.Count == 0 || _lastResizeAnimation, (bool val) => _lastResizeAnimation = val);
            var resizeAnimationUI = prefabFactory.CreateToggle(_resizeAnimationJSON);

            _reduceMinPosDistanceJSON = new JSONStorableFloat("Minimum distance between frames", 0.04f, (float val) => _lastReduceMinPosDistance = val, 0.001f, 0.5f, true)
            {
                valNoCallback = _lastReduceMinPosDistance
            };
            var reduceMinPosDistanceUI = prefabFactory.CreateSlider(_reduceMinPosDistanceJSON);

            _reduceMinRotationJSON = new JSONStorableFloat("Minimum rotation between frames", 10f, (float val) => _lastReduceMinRotation = val, 0.1f, 90f, true)
            {
                valNoCallback = _lastReduceMinRotation
            };
            var reduceMinRotationUI = prefabFactory.CreateSlider(_reduceMinRotationJSON);

            _reduceMaxFramesPerSecondJSON = new JSONStorableFloat("Max frames per second", 5f, (float val) => _reduceMaxFramesPerSecondJSON.valNoCallback = _lastReduceMaxFramesPerSecond = Mathf.Round(val), 1f, 10f, true)
            {
                valNoCallback = _lastReduceMaxFramesPerSecond
            };
            var maxFramesPerSecondUI = prefabFactory.CreateSlider(_reduceMaxFramesPerSecondJSON);

            prefabFactory.CreateSpacer();

            _importRecordedUI = prefabFactory.CreateButton("Import recorded animation (mocap)");
            _importRecordedUI.button.onClick.AddListener(() => ImportRecorded());

            prefabFactory.CreateSpacer();

            _reduceKeyframesUI = prefabFactory.CreateButton("Reduce float params keyframes");
            _reduceKeyframesUI.button.onClick.AddListener(() => ReduceKeyframes());

            prefabFactory.CreateSpacer();

            _playAndRecordControllersUI = prefabFactory.CreateButton(_recordingControllersCoroutine != null ? _recordingLabel : _startRecordControllersLabel);
            _playAndRecordControllersUI.button.onClick.AddListener(() => PlayAndRecordControllers());

            _playAndRecordFloatParamsUI = prefabFactory.CreateButton(_recordingFloatParamsCoroutine != null ? _recordingLabel : _startRecordFloatParamsLabel);
            _playAndRecordFloatParamsUI.button.onClick.AddListener(() => PlayAndRecordFloatParams());

            prefabFactory.CreateSpacer();

            var clearMocapUI = prefabFactory.CreateButton("Clear atom's mocap");
            clearMocapUI.button.onClick.AddListener(() => ClearMocapData());
            clearMocapUI.buttonColor = Color.yellow;

            animation.onTargetsSelectionChanged.AddListener(OnTargetsSelectionChanged);
            OnTargetsSelectionChanged();

            if (_importMocapOnLoad)
            {
                _importMocapOnLoad = false;
                ImportRecorded();
            }

            if (_simplifyFloatParamsOnLoad)
            {
                _simplifyFloatParamsOnLoad = false;
                ReduceKeyframes();
            }
        }

        private void OnTargetsSelectionChanged()
        {
            if (_recordingControllersCoroutine != null)
                _playAndRecordControllersUI.button.interactable = true;
            else if (current.GetSelectedTargets().Any())
                _playAndRecordControllersUI.button.interactable = true;
            else if (plugin.containingAtom.freeControllers.Any(fc => fc.GetComponent<MotionAnimationControl>()?.armedForRecord ?? false))
                _playAndRecordControllersUI.button.interactable = true;
            else
                _playAndRecordControllersUI.button.interactable = false;
        }

        private void ImportRecorded()
        {
            try
            {
                GetMocapImportOp().Prepare(_resizeAnimationJSON.val);

                if (_importRecordedUI == null) throw new NullReferenceException(nameof(_importRecordedUI));

                _importRecordedUI.buttonText.text = "Importing, please wait...";
                _importRecordedUI.button.interactable = false;

                StartCoroutine(ImportRecordedCoroutine());
            }
            catch (Exception exc)
            {
                SuperController.LogError($"Timeline.{nameof(MocapScreen)}.{nameof(ImportRecorded)}: {exc}");
            }
        }

        private IEnumerator ImportRecordedCoroutine()
        {
            IEnumerator enumerator = GetMocapImportOp().Execute();

            while (true)
            {
                try
                {
                    if (!enumerator.MoveNext())
                        break;
                }
                catch (Exception exc)
                {
                    _importRecordedUI.buttonText.text = "Import recorded animation (mocap)";
                    _importRecordedUI.button.interactable = true;
                    SuperController.LogError($"Timeline.{nameof(MocapScreen)}.{nameof(ImportRecordedCoroutine)}[{_importRecordedOptionsJSON.val}]: {exc}");
                    yield break;
                }
                if (enumerator.Current is MocapOperationsBase.Progress)
                {
                    var progress = (MocapOperationsBase.Progress)enumerator.Current;
                    _importRecordedUI.buttonText.text = $"Importing, please wait... ({progress.controllersProcessed} / {progress.controllersTotal})";
                    yield return 0;
                }
                else
                {
                    yield return enumerator.Current;
                }
            }

            _importRecordedUI.buttonText.text = "Import recorded animation (mocap)";
            _importRecordedUI.button.interactable = true;
        }

        private MocapOperationsBase GetMocapImportOp()
        {
            MocapOperationsBase x;
            if (_importRecordedOptionsJSON.val == "Keyframe Reduction")
                x = operations.MocapReduce(new MocapReduceSettings
                {
                    maxFramesPerSecond = _reduceMaxFramesPerSecondJSON.val,
                    minPosDelta = _reduceMinPosDistanceJSON.val,
                    minRotDelta = _reduceMinRotationJSON.val
                });
            else
                x = operations.MocapImport(new MocapImportSettings
                {
                    maxFramesPerSecond = _reduceMaxFramesPerSecondJSON.val
                });
            return x;
        }

        private void ReduceKeyframes()
        {
            _reduceKeyframesUI.buttonText.text = "Optimizing, please wait...";
            _reduceKeyframesUI.button.interactable = false;

            StartCoroutine(ReduceKeyframesCoroutine());
        }

        private IEnumerator ReduceKeyframesCoroutine()
        {
            var enumerator = operations.ParamKeyframeReduction().ReduceKeyframes(_reduceMaxFramesPerSecondJSON.val, _reduceMinPosDistanceJSON.val);
            while (true)
            {
                try
                {
                    if (!enumerator.MoveNext())
                        break;
                }
                catch (Exception exc)
                {
                    _reduceKeyframesUI.button.interactable = true;
                    _reduceKeyframesUI.buttonText.text = "Reduce float params keyframes";
                    SuperController.LogError($"Timeline.{nameof(MocapScreen)}.{nameof(ReduceKeyframesCoroutine)}[FloatParam]: {exc}");
                    yield break;
                }
                yield return enumerator.Current;
            }

            _reduceKeyframesUI.button.interactable = true;
            _reduceKeyframesUI.buttonText.text = "Reduce float params keyframes";
        }

        public void PlayAndRecordControllers()
        {
            if (_recordingControllersCoroutine != null)
            {
                plugin.StopCoroutine(_recordingControllersCoroutine);
                _recordingControllersCoroutine = null;
                _playAndRecordControllersUI.label = _startRecordControllersLabel;
                SuperController.singleton.StopPlayback();
                animation.StopAll();
                return;
            }
            foreach (var target in current.GetSelectedTargets().OfType<FreeControllerAnimationTarget>())
            {
                var mac = target.controller.GetComponent<MotionAnimationControl>();
                if (mac == null) continue;
                mac.armedForRecord = true;
            }
            animation.StopAll();
            animation.ResetAll();
            _recordingControllersCoroutine = plugin.StartCoroutine(PlayAndRecordControllersCoroutine());
        }

        private IEnumerator PlayAndRecordControllersCoroutine()
        {
            yield return 0;
            ClearMocapData();
            SuperController.singleton.SelectModeAnimationRecord();
            while (!IsRecording())
            {
                if (string.IsNullOrEmpty(SuperController.singleton.helpText))
                {
                    _recordingControllersCoroutine = null;
                    _playAndRecordControllersUI.label = _startRecordControllersLabel;
                    SuperController.singleton.SelectController(plugin.containingAtom.mainController);
                    yield break;
                }
                yield return 0;
            }
            var excludedControllers = current.targetControllers.Where(t => t.controller.GetComponent<MotionAnimationControl>()?.armedForRecord == true).ToList();
            foreach (var target in excludedControllers)
                target.playbackEnabled = false;
            animation.PlayAll(false);
            while ((_lastResizeAnimation || animation.playTime <= animation.clipTime) && IsRecording())
            {
                yield return 0;
            }
            if (IsRecording()) SuperController.singleton.StopPlayback();
            animation.StopAll();
            foreach (var target in excludedControllers)
                target.playbackEnabled = true;
            SuperController.singleton.motionAnimationMaster.StopPlayback();
            _recordingControllersCoroutine = null;
            ClearAllGrabbedControllers();
            if (!plugin.containingAtom.mainController.selected)
            {
                _importMocapOnLoad = true;
                SuperController.singleton.SelectController(plugin.containingAtom.mainController);
            }
        }

        private static bool IsRecording()
        {
            // There's a bool but it's protected.
            return SuperController.singleton.helpText?.StartsWith("Recording...") ?? false;
        }

        private void ClearAllGrabbedControllers()
        {
            foreach (var target in current.targetControllers.Where(t => t.controller.isGrabbing))
            {
                target.ignoreGrabEnd = true;
                try
                {
                    target.controller.RestorePreLinkState();
                    target.controller.isGrabbing = false;
                }
                finally
                {
                    target.ignoreGrabEnd = false;
                }
            }
        }

        private void ClearMocapData()
        {
            SuperController.singleton.motionAnimationMaster.SeekToBeginning();
            foreach (var mac in plugin.containingAtom.freeControllers.Select(fc => fc.GetComponent<MotionAnimationControl>()).Where(mac => mac != null))
            {
                mac.ClearAnimation();
            }
        }

        public void PlayAndRecordFloatParams()
        {
            if (_recordingFloatParamsCoroutine != null)
            {
                plugin.StopCoroutine(_recordingFloatParamsCoroutine);
                _recordingFloatParamsCoroutine = null;
                _playAndRecordFloatParamsUI.label = _startRecordFloatParamsLabel;
                animation.StopAll();
                SuperController.singleton.helpText = string.Empty;
                return;
            }
            if (!current.GetAllOrSelectedTargets().OfType<FloatParamAnimationTarget>().Any())
            {
                SuperController.LogError("Timeline: No float params to record");
                return;
            }
            animation.StopAll();
            animation.ResetAll();
            foreach (var target in current.GetSelectedTargets().OfType<FloatParamAnimationTarget>())
            {
                operations.Keyframes().RemoveAll(target);
            }
            _recordingFloatParamsCoroutine = plugin.StartCoroutine(PlayAndRecordFloatParamsCoroutine());
        }

        private IEnumerator PlayAndRecordFloatParamsCoroutine()
        {
            var sctrl = SuperController.singleton;
            sctrl.helpText = "Press Select or Spacebar to start float params recording";
            ChangeScreen(TargetsScreen.ScreenName);
            yield return 0; // Avoid select from same frame to interact
            while (!AreAnyStartRecordKeysDown())
                yield return 0;
            sctrl.helpText = string.Empty;
            animation.PlayAll(false);
            while (animation.playTime <= current.animationLength)
                yield return 0;
            animation.StopAll();
            _recordingFloatParamsCoroutine = null;
            _simplifyFloatParamsOnLoad = true;
            ChangeScreen(ScreenName);
        }

        private bool AreAnyStartRecordKeysDown()
        {
            var sctrl = SuperController.singleton;
            if (sctrl.isOVR)
            {
                if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.Touch)) return true;
                if (OVRInput.GetDown(OVRInput.Button.Three, OVRInput.Controller.Touch)) return true;
            }
            if (sctrl.isOpenVR)
            {
                if (sctrl.selectAction.stateDown) return true;
            }
            if (Input.GetKeyDown(KeyCode.Space)) return true;
            return false;
        }

        public override void OnDestroy()
        {
            animation.onTargetsSelectionChanged.RemoveListener(OnTargetsSelectionChanged);
        }
    }
}

