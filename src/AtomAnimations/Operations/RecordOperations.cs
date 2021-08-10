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
        private static int _timeMode;

        private readonly AtomAnimation _animation;
        private readonly AtomAnimationClip _clip;

        public RecordOperations(AtomAnimation animation, AtomAnimationClip clip)
        {
            _animation = animation;
            _clip = clip;
        }

        public IEnumerator StartRecording(
            bool recordExtendsLength,
            int recordInSeconds,
            List<ICurveAnimationTarget> targets,
            FreeControllerV3AnimationTarget raycastTarget
        )
        {
            // TODO: Counter should use in-game rather than helptext
            if (_recording)
            {
                SuperController.LogError("Timeline: Already recording");
                yield break;
            }

            _animation.StopAll();
            _animation.ResetAll();

            var exitOnMenuOpen = (SuperController.singleton.isOVR || SuperController.singleton.isOpenVR) && targets.OfType<FreeControllerV3AnimationTarget>().Any();
            if (exitOnMenuOpen) SuperController.singleton.HideMainHUD();

            SuperController.singleton.helpText = $"Preparing to record...";

            yield return 0;

            CleanupClip(targets, recordExtendsLength);

            yield return 0;

            for (var i = recordInSeconds; i > 0; i--)
            {
                SuperController.singleton.helpText = $"Start recording in {i}...";
                var next = Time.realtimeSinceStartup + 1f;
                while (Time.realtimeSinceStartup < next)
                {
                    yield return 0;
                    if (Input.GetKeyDown(KeyCode.Escape))
                        yield break;
                }
            }

            SuperController.singleton.helpText = "Recording...";

            StartRecording(recordExtendsLength, targets);

            var lastRecordedTime = 0f;
            while (true)
            {
                lastRecordedTime = Mathf.Max(lastRecordedTime, _animation.playTime);
                if (!_animation.isPlaying)
                    break;
                #warning This will probably overwrite the first frame for looping animations?
                if (!recordExtendsLength && _animation.playTime > _clip.animationLength)
                    break;
                if (Input.GetKeyDown(KeyCode.Escape))
                    break;
                if (!_clip.playbackMainInLayer)
                    break;
                if (exitOnMenuOpen && SuperController.singleton.mainHUD.gameObject.activeSelf)
                    break;
                if (!ReferenceEquals(raycastTarget, null))
                    PositionCameraTarget(raycastTarget);
                yield return 0;
            }

            StopRecording(targets);
        }

        private void CleanupClip(List<ICurveAnimationTarget> targets, bool recordExtendsLength)
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
                target.IncreaseCapacity(90 * (int)(recordExtendsLength ? 60 * 60 : target.GetLeadCurve().duration.Snap(1000)));
            }

            GC.Collect();
        }

        private void StartRecording(bool recordExtendsLength, List<ICurveAnimationTarget> targets)
        {
            _timeMode = _animation.timeMode;

            _animation.timeMode = TimeModes.RealTime;
            _clip.recording = true;
            _clip.infinite = recordExtendsLength;
            foreach (var target in targets)
            {
                target.recording = true;
                target.StartBulkUpdates();
            }

            _animation.PlayClip(_clip, false);

            _recording = true;
        }

        private void StopRecording(List<ICurveAnimationTarget> targets)
        {
            if (!_recording) return;

            _recording = false;

            _animation.timeMode = _timeMode;

            var resizeOp = new ResizeAnimationOperations();
            _animation.StopAll();
            _animation.ResetAll();
            SuperController.singleton.helpText = "";

            // TODO: This needs to be guaranteed. We could register the enumerator inside a disposable class, dispose being called in different cancel situations
            _clip.recording = false;
            if (_clip.infinite)
            {
                _clip.infinite = false;
                resizeOp.CropOrExtendEnd(_clip, _clip.GetAllCurveTargets().Select(t => t.GetLeadCurve().duration).Max());
            }

            foreach (var target in targets)
            {
                target.recording = false;
                target.TrimCapacity();
                target.EndBulkUpdates();
            }

            GC.Collect();

            SuperController.singleton.ShowMainHUD();
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
