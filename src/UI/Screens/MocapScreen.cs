using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class MocapScreen : ScreenBase
    {
        public const string ScreenName = "Import scene animation";
        private const string _recordingLabel = "\u25A0 Waiting for recording...";
        private const string _startRecordControllersLabel = "\u25B6 Clear & record selected";
        private static Coroutine _recordingControllersCoroutine;
        private static bool _lastResizeAnimation;
        private static bool? _lastAutoRecordStop;

        public override string screenId => ScreenName;

        private JSONStorableBool _resizeAnimationJSON;
        private UIDynamicButton _importRecordedUI;
        private UIDynamicButton _playAndRecordControllersUI;

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            // Right side

            CreateChangeScreenButton("<b><</b> <i>Back</i>", MoreScreen.ScreenName);

            prefabFactory.CreateSpacer();

            prefabFactory.CreateHeader("Import scene animation", 1);
            _resizeAnimationJSON = new JSONStorableBool("Resize animation to match import", current.targetControllers.Count == 0 || _lastResizeAnimation, val => _lastResizeAnimation = val);
            prefabFactory.CreateToggle(_resizeAnimationJSON);

            _importRecordedUI = prefabFactory.CreateButton("Import scene animation (mocap)");
            _importRecordedUI.button.onClick.AddListener(ImportRecorded);

            prefabFactory.CreateSpacer();

            prefabFactory.CreateHeader("Manage scene animation", 1);

            _playAndRecordControllersUI = prefabFactory.CreateButton(_recordingControllersCoroutine != null ? _recordingLabel : _startRecordControllersLabel);
            _playAndRecordControllersUI.button.onClick.AddListener(PlayAndRecordControllers);

            var clearMocapUI = prefabFactory.CreateButton("Clear atom's scene animation");
            clearMocapUI.button.onClick.AddListener(ClearMocapData);
            clearMocapUI.buttonColor = Color.yellow;

            prefabFactory.CreateSpacer();

            CreateChangeScreenButton("<i>Go to <b>reduce</b> screen...</i>", ReduceScreen.ScreenName);

            animationEditContext.animation.animatables.onTargetsSelectionChanged.AddListener(OnTargetsSelectionChanged);
            OnTargetsSelectionChanged();
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
                operations.MocapImport().Prepare(_resizeAnimationJSON.val);

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
            var controllers = animationEditContext.GetSelectedTargets().OfType<FreeControllerV3AnimationTarget>().Select(t => t.animatableRef.controller).ToList();

            AtomAnimationBackup.singleton.ClearBackup();

            var enumerator = operations.MocapImport().Execute(controllers);

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
                    SuperController.LogError($"Timeline.{nameof(MocapScreen)}.{nameof(ImportRecordedCoroutine)}: {exc}");
                    yield break;
                }
                var progress = enumerator.Current as MocapImportOperations.Progress;
                if (progress != null)
                {
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
            foreach (var target in animationEditContext.GetSelectedTargets().OfType<FreeControllerV3AnimationTarget>())
            {
                var mac = target.animatableRef.controller.GetComponent<MotionAnimationControl>();
                if (mac == null) continue;
                mac.armedForRecord = true;
            }
            animation.StopAndReset();
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
            var excludedControllers = current.targetControllers.Where(t => t.animatableRef.controller.GetComponent<MotionAnimationControl>()?.armedForRecord == true).ToList();
            foreach (var target in excludedControllers)
                target.playbackEnabled = false;
            animationEditContext.PlayCurrentClip();
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
                foreach (var target in current.targetControllers.Where(t => t.animatableRef.controller.isGrabbing))
                {
                    target.animatableRef.controller.RestorePreLinkState();
                    target.animatableRef.controller.isGrabbing = false;
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
            prefabFactory.CreateConfirm("Clear all scene animation from atom?", ClearMocapDataConfirm);
        }

        private void ClearMocapDataConfirm()
        {
            SuperController.singleton.motionAnimationMaster.SeekToBeginning();
            foreach (var mac in plugin.containingAtom.freeControllers.Select(fc => fc.GetComponent<MotionAnimationControl>()).Where(mac => mac != null))
            {
                mac.ClearAnimation();
            }
        }

        public override void OnDestroy()
        {
            animationEditContext.animation.animatables.onTargetsSelectionChanged.RemoveListener(OnTargetsSelectionChanged);
        }
    }
}

