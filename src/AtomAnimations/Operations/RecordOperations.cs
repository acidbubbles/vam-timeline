using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class RecordOperations
    {
        private readonly AtomAnimation _animation;
        private readonly AtomAnimationClip _clip;
        private readonly PeerManager _peerManager;

        public RecordOperations(AtomAnimation animation, AtomAnimationClip clip, PeerManager peerManager)
        {
            _animation = animation;
            _clip = clip;
            _peerManager = peerManager;
        }

        public IEnumerator StartRecording(
            IMonoBehavior owner,
            int timeMode,
            bool recordExtendsLength,
            int recordInSeconds,
            List<ICurveAnimationTarget> targets,
            FreeControllerV3AnimationTarget raycastTarget,
            bool exitOnMenuOpen,
            bool showStartMarkers)
        {
            if (_animation.recording)
            {
                SuperController.LogError("Timeline: Already recording");
                yield break;
            }

            if (_animation.isPlaying)
            {
                SuperController.LogError("Timeline: Stop playback before recording");
                yield break;
            }

            ShowText("Preparing to record...");

            yield return 0;

            if (!owner.isActiveAndEnabled)
            {
                ShowText(null);
                yield break;
            }

            BeforeRecording(targets, recordExtendsLength);

            yield return 0;

            if (!owner.isActiveAndEnabled)
            {
                ShowText(null);
                yield break;
            }

            if(showStartMarkers && !_clip.loop) ShowStartMarkers(targets);

            for (var i = recordInSeconds; i > 0; i--)
            {
                ShowText($"Start recording in {i}...");
                var next = Time.realtimeSinceStartup + 1f;
                while (Time.realtimeSinceStartup < next)
                {
                    yield return 0;
                    if (!owner.isActiveAndEnabled || Input.GetKeyDown(KeyCode.Escape))
                    {
                        ShowText(null);
                        HideStartMarkers(targets);
                        yield break;
                    }
                }
            }

            ShowText(null);

            AtomAnimationBackup.singleton.ClearBackup();
            RecordFirstKeyframe(targets);
            StartRecording(timeMode, recordExtendsLength, targets);
            if(showStartMarkers && _clip.loop) ShowStartMarkers(targets);

            var lastRecordedTime = 0f;
            var recordLengthStr = _clip.infinite ? "∞" : _clip.animationLength.ToString("0.0");
            while (true)
            {
                lastRecordedTime = Mathf.Max(lastRecordedTime, _animation.playTime);
                if (!_animation.isPlaying)
                    break;
                if (!recordExtendsLength && _animation.playTime > _clip.animationLength)
                    break;
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Space))
                    break;
                if (!_clip.playbackMainInLayer)
                    break;
                if (exitOnMenuOpen && SuperController.singleton.mainHUD.gameObject.activeSelf)
                    break;
                if (!owner.isActiveAndEnabled)
                    break;
                if (!ReferenceEquals(raycastTarget, null))
                    PositionCameraTarget(raycastTarget);
                if (UIPerformance.ShouldRun(UIPerformance.LowFrequency))
                    ShowText($"Recording... {_clip.clipTime:0.0} / {recordLengthStr}");

                yield return 0;
            }

            HideStartMarkers(targets);
            ClearAllGrabbedControllers(targets);
            AfterRecording(targets);
        }

        private void ShowStartMarkers(List<ICurveAnimationTarget> targets)
        {
            var nextAnimation = !_clip.loop && _clip.nextAnimationNameId != 0 ? _animation.index.ByLayerQualified(_clip.animationLayerQualifiedId).FirstOrDefault(c => c.animationNameId == _clip.nextAnimationNameId) : null;
            foreach (var target in targets.OfType<FreeControllerV3AnimationTarget>())
            {
                var nextTarget = nextAnimation?.targetControllers.FirstOrDefault(t => t.TargetsSameAs(target));
                var snapNext = nextTarget != null && !nextTarget.hasParentBound;
                var control = target.animatableRef.controller.control;
                var previousPosition = control.position;
                var previousRotation = control.rotation;
                if (snapNext)
                {
                    var controlParent = control.parent;
                    var position = controlParent.TransformPoint(nextTarget.EvaluatePosition(0f));
                    var rotation = controlParent.rotation * nextTarget.EvaluateRotation(0f);
                    control.SetPositionAndRotation(position, rotation);
                }
                target.animatableRef.controller.TakeSnapshot();
                target.animatableRef.controller.drawSnapshot = true;
                if (snapNext)
                {
                    control.SetPositionAndRotation(previousPosition, previousRotation);
                }
            }
        }

        private void HideStartMarkers(List<ICurveAnimationTarget> targets)
        {
            foreach (var target in targets.OfType<FreeControllerV3AnimationTarget>())
            {
                target.animatableRef.controller.drawSnapshot = false;
            }
        }

        private void RecordFirstKeyframe(IList<ICurveAnimationTarget> targets)
        {
            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                target.SetKeyframeToCurrent(0);
                if (_clip.loop)
                    target.SetKeyframeToCurrent(_clip.animationLength);
            }
        }

        private void BeforeRecording(List<ICurveAnimationTarget> targets, bool recordExtendsLength)
        {
            var keyframesOps = new KeyframesOperations(_clip);
            var isEmpty = _clip.IsEmpty();

            foreach (var target in targets)
            {
                keyframesOps.RemoveAll(target, fromTime: _clip.clipTime);
            }

            if (isEmpty)
                _clip.loop = false;
            _clip.DirtyAll();
            _animation.RebuildAnimationNow();

            foreach (var target in targets)
            {
                target.IncreaseCapacity(90 * (int)(recordExtendsLength ? 60 : target.GetLeadCurve().duration.Snap(1)));
            }

            GC.Collect();
        }

        private void StartRecording(int timeMode, bool recordExtendsLength, List<ICurveAnimationTarget> targets)
        {
            _animation.recording = true;

            _animation.SetTemporaryTimeMode(timeMode);
            _peerManager.SendStartRecording(timeMode);

            _animation.autoStop = recordExtendsLength ? 0 : (_clip.loop ? _clip.animationLength - 0.0009f : _clip.animationLength + 0.0009f);
            _animation.globalSpeed = 1f;
            _clip.recording = true;
            _clip.infinite = recordExtendsLength;
            _clip.speed = 1f;
            foreach (var target in targets)
            {
                target.recording = true;
                target.StartBulkUpdates();
            }

            _animation.PlayClip(_clip, false);
        }

        private void AfterRecording(List<ICurveAnimationTarget> targets)
        {
            if (!_animation.recording) return;
            _animation.recording = false;

            _animation.RestoreTemporaryTimeMode();
            _peerManager.SendStopRecording();

            ShowText(null);

            if (_clip.infinite)
            {
                _clip.infinite = false;
                var resizeOp = new ResizeAnimationOperations();
                resizeOp.CropOrExtendEnd(_clip, _clip.GetAllCurveTargets().Select(t => t.GetLeadCurve().duration).Max());
            }

            foreach (var target in targets)
            {
                target.TrimCapacity();
                target.EndBulkUpdates();
            }

            GC.Collect();
            _clip.clipTime = Mathf.Min(_clip.animationLength, _clip.clipTime).Snap();
        }

        private static void ClearAllGrabbedControllers(IEnumerable<ICurveAnimationTarget> targets)
        {
#if (VAM_GT_1_20)
            foreach (var target in targets.OfType<FreeControllerV3AnimationTarget>().Where(t => t.animatableRef.controller.isGrabbing))
            {
                target.animatableRef.controller.RestorePreLinkState();
                target.animatableRef.controller.isGrabbing = false;
            }
#endif
        }

        private void ShowText(string text)
        {
            if (_animation.fadeManager?.ShowText(text) != true)
                SuperController.singleton.helpText = text;
        }

        private static void PositionCameraTarget(FreeControllerV3AnimationTarget cameraTarget)
        {
            var cameraTransform = SuperController.singleton.centerCameraTarget.transform;
            var cameraPosition = cameraTransform.position;
            var cameraForward = cameraTransform.forward;
            var distance = 1.7f;
            RaycastHit hit;
            const int layerMask = ~(1 << 8);
            if (Physics.Raycast(cameraPosition + cameraForward * 0.3f, cameraForward, out hit, 10f, layerMask))
                distance = hit.distance;

            cameraTarget.animatableRef.controller.control.position = cameraPosition + cameraForward * (0.3f + distance);
        }
    }
}
