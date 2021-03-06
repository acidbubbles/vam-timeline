﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace VamTimeline
{
    public class AddAnimationOperations
    {
        private readonly AtomAnimation _animation;
        private readonly AtomAnimationClip _clip;

        public AddAnimationOperations(AtomAnimation animation, AtomAnimationClip clip)
        {
            _animation = animation;
            _clip = clip;
        }

        public AtomAnimationClip AddAnimationAsCopy()
        {
            var clip = _animation.CreateClip(_clip);
            _clip.CopySettingsTo(clip);
            foreach (var origTarget in _clip.targetControllers)
            {
                var newTarget = CopyTarget(clip, origTarget);
                for (var i = 0; i < origTarget.curves.Count; i++)
                    newTarget.curves[i].keys = new List<BezierKeyframe>(origTarget.curves[i].keys);
                newTarget.dirty = true;
            }
            foreach (var origTarget in _clip.targetFloatParams)
            {
                if (!origTarget.animatableRef.EnsureAvailable(false)) continue;
                var newTarget = clip.Add(new JSONStorableFloatAnimationTarget(origTarget));
                newTarget.value.keys = new List<BezierKeyframe>(origTarget.value.keys);
                newTarget.dirty = true;
            }
            foreach (var origTarget in _clip.targetTriggers)
            {
                var newTarget = clip.Add(new TriggersTrackAnimationTarget (origTarget.animatableRef));
                foreach (var origTrigger in origTarget.triggersMap)
                {
                    var trigger = new CustomTrigger();
                    trigger.RestoreFromJSON(origTrigger.Value.GetJSON());
                    newTarget.SetKeyframe(origTrigger.Key, trigger);
                }
                newTarget.dirty = true;
            }
            return clip;
        }

        public AtomAnimationClip AddAnimationFromCurrentFrame(bool copySettings)
        {
            var clip = _animation.CreateClip(_clip);
            if (copySettings) _clip.CopySettingsTo(clip);
            foreach (var origTarget in _clip.targetControllers)
            {
                var newTarget = CopyTarget(clip, origTarget);
                newTarget.SetKeyframeToCurrentTransform(0f);
                newTarget.SetKeyframeToCurrentTransform(clip.animationLength);
            }
            foreach (var origTarget in _clip.targetFloatParams)
            {
                if (!origTarget.animatableRef.EnsureAvailable(false)) continue;
                var newTarget = clip.Add(origTarget.animatableRef);
                newTarget.SetKeyframe(0f, origTarget.animatableRef.floatParam.val);
                newTarget.SetKeyframe(clip.animationLength, origTarget.animatableRef.floatParam.val);
            }
            return clip;
        }

        public AtomAnimationClip AddTransitionAnimation()
        {
            var next = _animation.GetClip(_clip.animationLayer, _clip.nextAnimationName);
            if (next == null)
            {
                SuperController.LogError("There is no animation to transition to");
                return null;
            }

            var clip = _animation.CreateClip(_clip);
            clip.animationName = $"{_clip.animationName} > {next.animationName}";
            clip.loop = false;
            clip.autoTransitionPrevious = _animation.index.ByLayer(_clip.animationLayer).Any(c => c.animationLayer == _clip.animationLayer);
            clip.autoTransitionNext = _clip.nextAnimationName != null;
            clip.nextAnimationName = _clip.nextAnimationName;
            clip.blendInDuration = AtomAnimationClip.DefaultBlendDuration;
            clip.nextAnimationTime = clip.animationLength - clip.blendInDuration;
            clip.ensureQuaternionContinuity = _clip.ensureQuaternionContinuity;

            foreach (var origTarget in _clip.targetControllers)
            {
                var newTarget = CopyTarget(clip, origTarget);
                newTarget.SetCurveSnapshot(0f, origTarget.GetCurveSnapshot(_clip.animationLength));
                newTarget.SetCurveSnapshot(clip.animationLength, next.targetControllers.First(t => t.TargetsSameAs(origTarget)).GetCurveSnapshot(0f));
            }
            foreach (var origTarget in _clip.targetFloatParams)
            {
                if (!origTarget.animatableRef.EnsureAvailable(false)) continue;
                var newTarget = clip.Add(origTarget.animatableRef);
                newTarget.SetCurveSnapshot(0f, origTarget.GetCurveSnapshot(_clip.animationLength));
                newTarget.SetCurveSnapshot(clip.animationLength, next.targetFloatParams.First(t => t.TargetsSameAs(origTarget)).GetCurveSnapshot(0f));
            }

            _animation.clips.Remove(clip);
            _animation.clips.Insert(_animation.clips.IndexOf(_clip) + 1, clip);

            _clip.nextAnimationName = clip.animationName;
            return clip;
        }

        private static FreeControllerV3AnimationTarget CopyTarget(AtomAnimationClip clip, FreeControllerV3AnimationTarget origTarget)
        {
            var newTarget = clip.Add(origTarget.animatableRef);
            newTarget.SetParent(origTarget.parentAtomId, origTarget.parentRigidbodyId);
            newTarget.weight = origTarget.weight;
            newTarget.controlPosition = origTarget.controlPosition;
            newTarget.controlRotation = origTarget.controlPosition;
            return newTarget;
        }

        public void DeleteAnimation(AtomAnimationClip clip)
        {
            try
            {
                if (clip == null) return;
                if (_animation.clips.Count == 1)
                {
                    SuperController.LogError("Timeline: Cannot delete the only animation.");
                    return;
                }
                _animation.RemoveClip(clip);
                foreach (var matchingClip in _animation.clips)
                {
                    if (matchingClip.nextAnimationName == clip.animationName)
                    {
                        matchingClip.nextAnimationName = null;
                        matchingClip.nextAnimationTime = 0;
                    }
                }
            }
            catch (Exception exc)
            {
                SuperController.LogError($"Timeline.{nameof(ManageAnimationsScreen)}.{nameof(DeleteAnimation)}: {exc}");
            }
        }
    }
}
