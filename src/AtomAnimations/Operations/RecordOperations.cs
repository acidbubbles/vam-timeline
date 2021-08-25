using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class RecordOperations
    {
        private static bool _recording;

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
            int timeMode,
            bool recordExtendsLength,
            int recordInSeconds,
            List<ICurveAnimationTarget> targets,
            FreeControllerV3AnimationTarget raycastTarget,
            bool exitOnMenuOpen)
        {
            if (_recording)
            {
                SuperController.LogError("Timeline: Already recording");
                yield break;
            }

            _animation.StopAll();
            _animation.ResetAll();

            ShowText("Preparing to record...");

            yield return 0;

            BeforeRecording(targets, recordExtendsLength);

            yield return 0;

            for (var i = recordInSeconds; i > 0; i--)
            {
                ShowText($"Start recording in {i}...");
                var next = Time.realtimeSinceStartup + 1f;
                while (Time.realtimeSinceStartup < next)
                {
                    yield return 0;
                    if (Input.GetKeyDown(KeyCode.Escape))
                    {
                        ShowText(null);
                        yield break;
                    }
                }
            }

            ShowText(null);

            StartRecording(timeMode, recordExtendsLength, targets);

            RecordFirstKeyframe();

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
                if (!ReferenceEquals(raycastTarget, null))
                    PositionCameraTarget(raycastTarget);
                if (UIPerformance.ShouldRun(UIPerformance.LowFrequency))
                    ShowText($"Recording... {_clip.clipTime:0.0} / {recordLengthStr}");

                yield return 0;
            }

            AfterRecording(targets);
        }

        private void RecordFirstKeyframe()
        {
            var clipTime = _clip.clipTime.Snap();

            for (var i = 0; i < _clip.targetControllers.Count; i++)
            {
                var target = _clip.targetControllers[i];
                if (target.recording)
                {
                    target.SetKeyframeToCurrentTransform(clipTime);
                    if (_clip.loop && clipTime == 0)
                        target.SetKeyframeToCurrentTransform(_clip.animationLength);
                }
            }

            for (var i = 0; i < _clip.targetFloatParams.Count; i++)
            {
                var target = _clip.targetFloatParams[i];
                if (target.recording)
                {
                    target.SetKeyframe(clipTime, target.animatableRef.floatParam.val);
                    if (_clip.loop && clipTime == 0)
                        target.SetKeyframe(_clip.animationLength, target.animatableRef.floatParam.val);
                }
            }
        }

        private void ShowText(string text)
        {
            if (_animation.fadeManager?.ShowText(text) != true)
                SuperController.singleton.helpText = text;
        }

        private void BeforeRecording(List<ICurveAnimationTarget> targets, bool recordExtendsLength)
        {
            var keyframesOps = new KeyframesOperations(_clip);

            foreach (var target in targets)
            {
                keyframesOps.RemoveAll(target);
            }

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
            _recording = true;

            _animation.SetTemporaryTimeMode(timeMode);
            _peerManager.SendStartRecording(timeMode);

            _animation.autoStop = recordExtendsLength ? 0 : (_clip.loop ? _clip.animationLength - 0.0009f : _clip.animationLength + 0.0009f);
            _animation.speed = 1f;
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
            if (!_recording) return;
            _recording = false;

            _animation.RestoreTemporaryTimeMode();
            _peerManager.SendStopRecording();

            var resizeOp = new ResizeAnimationOperations();
            _animation.StopAll();
            _animation.ResetAll();
            ShowText(null);

            if (_clip.infinite)
            {
                _clip.infinite = false;
                resizeOp.CropOrExtendEnd(_clip, _clip.GetAllCurveTargets().Select(t => t.GetLeadCurve().duration).Max());
            }

            foreach (var target in targets)
            {
                target.TrimCapacity();
                target.EndBulkUpdates();
            }

            GC.Collect();
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
