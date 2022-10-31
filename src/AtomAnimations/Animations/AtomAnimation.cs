using System;
using System.Linq;
using System.Runtime.CompilerServices;
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
                onClipsListChanged.Invoke();
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
            var deltaTime = timeMode == TimeModes.RealTime ? (Time.time - _lastUpdateTime) : Time.deltaTime;
            _lastUpdateTime = Time.time;

            SyncTriggers(true);

            if (!allowAnimationProcessing || paused) return;

            SampleFloatParams();
            SyncTriggers(false);
            ProcessAnimationSequence(deltaTime * globalSpeed);

            if (fadeManager?.black == true && playTime > _scheduleFadeIn && !simulationFrozen)
            {
                if(logger.sequencing) logger.Log(logger.sequencingCategory, $"Fade in {playTime - _scheduleFadeIn:0.000}s after transition.");
                _scheduleFadeIn = float.MaxValue;
                fadeManager.FadeIn();
            }
        }

        public void FixedUpdate()
        {
            var deltaTime = timeMode == TimeModes.RealTime ? (Time.time - _lastFixedUpdateTime) : Time.deltaTime;
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
