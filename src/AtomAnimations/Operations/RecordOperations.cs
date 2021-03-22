using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VamTimeline
{
    public class RecordOperations
    {
        private readonly AtomAnimation _animation;
        private readonly AtomAnimationClip _clip;

        public RecordOperations(AtomAnimation animation, AtomAnimationClip clip)
        {
            _animation = animation;
            _clip = clip;
        }

        public IEnumerator StartRecording(List<FreeControllerAnimationTarget> controllers, List<FloatParamAnimationTarget> floatParams)
        {
            // TODO: Handle stopping in the middle of it
            // TODO: Handle starting while it's already recording
            // TODO: Counter should use in-game rather than helptext

            _animation.StopAll();
            _animation.ResetAll();

            var keyframesOps = new KeyframesOperations(_clip);

            foreach (var target in controllers)
            {
                keyframesOps.RemoveAll(target);
                target.recording = true;
            }
            foreach (var target in floatParams)
            {
                keyframesOps.RemoveAll(target);
                target.recording = true;
            }

            SuperController.singleton.helpText = "Starting recording in 3...";
            var next = Time.realtimeSinceStartup + 1f;
            while (Time.realtimeSinceStartup < next)
            {
                yield return 0;
                if(Input.GetKeyDown(KeyCode.Escape))
                    yield break;
            }

            SuperController.singleton.helpText = "Starting recording in 2...";
            next = Time.realtimeSinceStartup + 1f;
            while (Time.realtimeSinceStartup < next)
            {
                yield return 0;
                if(Input.GetKeyDown(KeyCode.Escape))
                    yield break;
            }

            SuperController.singleton.helpText = "Starting recording in 1...";
            next = Time.realtimeSinceStartup + 1f;
            while (Time.realtimeSinceStartup < next)
            {
                yield return 0;
                if(Input.GetKeyDown(KeyCode.Escape))
                    yield break;
            }

            SuperController.singleton.helpText = "Recording...";

            _animation.PlayClip(_clip, false);

            while (_animation.playTime <= _clip.animationLength && _animation.isPlaying)
                yield return 0;

            _animation.StopAll();
            _animation.ResetAll();
            SuperController.singleton.helpText = "";

            // TODO: This needs to be guaranteed. We could register the enumerator inside a disposable class, dispose being called in different cancel situations
            foreach (var target in controllers)
            {
                target.recording = false;
            }
            foreach (var target in floatParams)
            {
                target.recording = false;
            }

            // TODO: This should be deferred
            _animation.RebuildAnimationNow();
        }
    }
}
