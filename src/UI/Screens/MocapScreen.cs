using System;
using System.Collections;
using System.Collections.Generic;
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
        private static Coroutine _recordingControllersCoroutine;
        private static Coroutine _recordingFloatParamsCoroutine;
        private static bool _lastResizeAnimation;
        private static float _lastReduceMinPosDistance = 0.02f;
        private static float _lastReduceMinRotation = 5f;
        private static float _lastReduceMaxFramesPerSecond = 10f;
        private static bool _importMocapOnLoad;
        private static bool _simplifyFloatParamsOnLoad;
        private static bool? _lastAutoRecordStop;


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
            prefabFactory.CreatePopup(_importRecordedOptionsJSON, false, true);

            _resizeAnimationJSON = new JSONStorableBool("Resize animation to mocap length", current.targetControllers.Count == 0 || _lastResizeAnimation, val => _lastResizeAnimation = val);
            prefabFactory.CreateToggle(_resizeAnimationJSON);

            _reduceMinPosDistanceJSON = new JSONStorableFloat("Minimum distance between frames", 0.04f, val => _lastReduceMinPosDistance = val, 0.001f, 0.5f)
            {
                valNoCallback = _lastReduceMinPosDistance
            };
            prefabFactory.CreateSlider(_reduceMinPosDistanceJSON);

            _reduceMinRotationJSON = new JSONStorableFloat("Minimum rotation between frames", 10f, val => _lastReduceMinRotation = val, 0.1f, 90f)
            {
                valNoCallback = _lastReduceMinRotation
            };
            prefabFactory.CreateSlider(_reduceMinRotationJSON);

            _reduceMaxFramesPerSecondJSON = new JSONStorableFloat("Max frames per second", 5f, val => _reduceMaxFramesPerSecondJSON.valNoCallback = _lastReduceMaxFramesPerSecond = Mathf.Round(val), 1f, 10f)
            {
                valNoCallback = _lastReduceMaxFramesPerSecond
            };
            prefabFactory.CreateSlider(_reduceMaxFramesPerSecondJSON);

            prefabFactory.CreateSpacer();

            _importRecordedUI = prefabFactory.CreateButton("Import recorded animation (mocap)");
            _importRecordedUI.button.onClick.AddListener(ImportRecorded);

            prefabFactory.CreateSpacer();

            _reduceKeyframesUI = prefabFactory.CreateButton("Reduce float params keyframes");
            _reduceKeyframesUI.button.onClick.AddListener(ReduceKeyframes);

            prefabFactory.CreateSpacer();

            _playAndRecordControllersUI = prefabFactory.CreateButton(_recordingControllersCoroutine != null ? _recordingLabel : _startRecordControllersLabel);
            _playAndRecordControllersUI.button.onClick.AddListener(PlayAndRecordControllers);

            _playAndRecordFloatParamsUI = prefabFactory.CreateButton(_recordingFloatParamsCoroutine != null ? _recordingLabel : _startRecordFloatParamsLabel);
            _playAndRecordFloatParamsUI.button.onClick.AddListener(PlayAndRecordFloatParams);

            prefabFactory.CreateSpacer();

            var clearMocapUI = prefabFactory.CreateButton("Clear atom's mocap");
            clearMocapUI.button.onClick.AddListener(ClearMocapData);
            clearMocapUI.buttonColor = Color.yellow;

            animationEditContext.onTargetsSelectionChanged.AddListener(OnTargetsSelectionChanged);
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
            else if (animationEditContext.GetSelectedTargets().Any())
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
            var controllers = animationEditContext.GetSelectedTargets().OfType<FreeControllerAnimationTarget>().Select(t => t.controller).ToList();
            var enumerator = GetMocapImportOp().Execute(controllers);

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
            var enumerator = operations.ParamKeyframeReduction().ReduceKeyframes(animationEditContext.GetAllOrSelectedTargets().OfType<FloatParamAnimationTarget>().ToList(), _reduceMaxFramesPerSecondJSON.val, _reduceMinPosDistanceJSON.val);
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
                if (_lastAutoRecordStop != null)
                {
                    SuperController.singleton.motionAnimationMaster.autoRecordStop = _lastAutoRecordStop.Value;
                    _lastAutoRecordStop = null;
                }
                return;
            }
            _lastAutoRecordStop = SuperController.singleton.motionAnimationMaster.autoRecordStop;
            SuperController.singleton.motionAnimationMaster.autoRecordStop = false;
            foreach (var target in animationEditContext.GetSelectedTargets().OfType<FreeControllerAnimationTarget>())
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
                    if (_lastAutoRecordStop != null)
                    {
                        SuperController.singleton.motionAnimationMaster.autoRecordStop = _lastAutoRecordStop.Value;
                        _lastAutoRecordStop = null;
                    }
                    yield break;
                }
                yield return 0;
            }
            var excludedControllers = current.targetControllers.Where(t => t.controller.GetComponent<MotionAnimationControl>()?.armedForRecord == true).ToList();
            foreach (var target in excludedControllers)
                target.playbackEnabled = false;
            animationEditContext.PlayCurrentAndOtherMainsInLayers(false);
            while ((_lastResizeAnimation || animationEditContext.playTime <= animationEditContext.clipTime) && IsRecording())
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
            if (_lastAutoRecordStop != null)
            {
                SuperController.singleton.motionAnimationMaster.autoRecordStop = _lastAutoRecordStop.Value;
                _lastAutoRecordStop = null;
            }
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
            #if (VAM_GT_1_20)
            animationEditContext.ignoreGrabEnd = true;
            try
            {
                foreach (var target in current.targetControllers.Where(t => t.controller.isGrabbing))
                {
                    target.controller.RestorePreLinkState();
                    target.controller.isGrabbing = false;
                }
            }
            finally
            {
                animationEditContext.ignoreGrabEnd = false;
            }
            #else
            throw new NotSupportedException("This feature requires Virt-A-Mate 1.20+");
            #endif
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
            if (!animationEditContext.GetAllOrSelectedTargets().OfType<FloatParamAnimationTarget>().Any())
            {
                SuperController.LogError("Timeline: No float params to record");
                return;
            }
            animation.StopAll();
            animation.ResetAll();
            foreach (var target in animationEditContext.GetSelectedTargets().OfType<FloatParamAnimationTarget>())
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
            animationEditContext.PlayCurrentAndOtherMainsInLayers(false);
            while (animation.playTime <= current.animationLength && animation.isPlaying)
                yield return 0;
            animationEditContext.Stop();
            animationEditContext.clipTime = 0f;
            _recordingFloatParamsCoroutine = null;
            _simplifyFloatParamsOnLoad = true;
            ChangeScreen(ScreenName);
        }

        private static bool AreAnyStartRecordKeysDown()
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
            animationEditContext.onTargetsSelectionChanged.RemoveListener(OnTargetsSelectionChanged);
        }
    }
}

