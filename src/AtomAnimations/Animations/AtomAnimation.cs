using System;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public partial class AtomAnimation : MonoBehaviour
    {
        #region Event Handlers

        private void OnAnimationSettingsChanged(string param)
        {
            index.Rebuild();
            onAnimationSettingsChanged.Invoke();
            if (param == nameof(AtomAnimationClip.animationName) || param == nameof(AtomAnimationClip.animationLayer) || param == nameof(AtomAnimationClip.animationSegment))
            {
                onClipsListChanged.Invoke();
                clipListChangedTrigger.Trigger();
            }
        }

        private void OnAnimationKeyframesDirty()
        {
            if (_animationRebuildInProgress) throw new InvalidOperationException("A rebuild is already in progress. This is usually caused by by RebuildAnimation triggering dirty (internal error).");
            if (_animationRebuildRequestPending) return;
            _animationRebuildRequestPending = true;
            StartCoroutine(RebuildDeferred());
        }

        private void OnTargetsListChanged()
        {
            index.Rebuild();
            OnAnimationKeyframesDirty();
        }

        #endregion

        #region Unity Lifecycle

        private float _lastUpdateTime = Time.unscaledTime;
        private float _lastFixedUpdateTime = Time.unscaledTime;

        public void Update()
        {
            float deltaTime;
            switch (timeMode)
            {
                case TimeModes.RealTime:
                    deltaTime = Time.time - _lastUpdateTime;
                    break;
                case TimeModes.RealTimeLegacy:
                    deltaTime = Time.unscaledDeltaTime * Time.timeScale;
                    break;
                default:
                    deltaTime = Time.deltaTime;
                    break;
            }
            _lastUpdateTime = Time.time;

            SyncTriggers(true);

            clipListChangedTrigger.trigger.Update();
            isPlayingChangedTrigger.trigger.Update();

            if (!allowAnimationProcessing || paused) return;

            SampleFloatParams();
            SyncTriggers(false);
            ProcessAnimationSequence(deltaTime * globalSpeed);
            applyNextPose = false;

            if (fadeManager?.black == true && playTime > _scheduleFadeIn && !simulationFrozen)
            {
                if(logger.sequencing) logger.Log(logger.sequencingCategory, $"Fade in {playTime - _scheduleFadeIn:0.000}s after transition.");
                _scheduleFadeIn = float.MaxValue;
                fadeManager.FadeIn();
            }
        }

        public void FixedUpdate()
        {
            float deltaTime;
            switch (timeMode)
            {
                case TimeModes.RealTime:
                    deltaTime = Time.time - _lastFixedUpdateTime;
                    break;
                case TimeModes.RealTimeLegacy:
                    deltaTime = Time.unscaledDeltaTime * Time.timeScale;
                    break;
                default:
                    deltaTime = Time.deltaTime;
                    break;
            }
            _lastFixedUpdateTime = Time.time;


            if (!allowAnimationProcessing || paused) return;

            var delta = deltaTime * _globalSpeed;
            playTime += delta;

            if (autoStop > 0f && playTime >= autoStop)
            {
                StopAll();
                return;
            }

            AdvanceClipsTime(delta);
            SampleControllers();
        }

        public void OnDestroy()
        {
            isPlayingChangedTrigger.Dispose();
            clipListChangedTrigger.Dispose();

            onAnimationSettingsChanged.RemoveAllListeners();
            onIsPlayingChanged.RemoveAllListeners();
            onClipIsPlayingChanged.RemoveAllListeners();
            onSpeedChanged.RemoveAllListeners();
            onWeightChanged.RemoveAllListeners();
            onClipsListChanged.RemoveAllListeners();
            onAnimationRebuilt.RemoveAllListeners();
            animatables.RemoveAllListeners();

            foreach (var clip in clips)
            {
                clip.Dispose();
            }
        }

        #endregion

        private int _restoreTimeMode;
        public void SetTemporaryTimeMode(int temporaryTimeMode)
        {
            _restoreTimeMode = timeMode;
            timeMode = temporaryTimeMode;
        }

        public void RestoreTemporaryTimeMode()
        {
            timeMode = _restoreTimeMode;
            _restoreTimeMode = 0;
        }

        public void CleanupAnimatables()
        {
            if (animatables.locked) return;

            for (var i = animatables.storableFloats.Count - 1; i >= 0; i--)
            {
                var a = animatables.storableFloats[i];
                if(!clips.Any(c => c.targetFloatParams.Any(t => t.animatableRef == a)))
                    animatables.RemoveStorableFloat(a);
            }

            for (var i = animatables.controllers.Count - 1; i >= 0; i--)
            {
                var a = animatables.controllers[i];
                if(!clips.Any(c => c.targetControllers.Any(t => t.animatableRef == a)))
                    animatables.RemoveController(a);
            }

            for (var i = animatables.triggers.Count - 1; i >= 0; i--)
            {
                var a = animatables.triggers[i];
                if(!clips.Any(c => c.targetTriggers.Any(t => t.animatableRef == a)))
                    animatables.RemoveTriggerTrack(a);
            }
        }
    }
}
