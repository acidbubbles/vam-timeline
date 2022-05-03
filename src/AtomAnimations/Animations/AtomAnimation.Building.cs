using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public partial class AtomAnimation
    {
        #region Animation Rebuilding

        private IEnumerator RebuildDeferred()
        {
            yield return new WaitForEndOfFrame();
            while (isPlaying)
                yield return 0;
            RebuildAnimationNow();
        }

        public void RebuildAnimationNow()
        {
            if (_animationRebuildInProgress) throw new InvalidOperationException("A rebuild is already in progress. This is usually caused by by RebuildAnimation triggering dirty (internal error).");
            _animationRebuildRequestPending = false;
            _animationRebuildInProgress = true;
            try
            {
                RebuildAnimationNowImpl();
            }
            catch (Exception exc)
            {
                SuperController.LogError($"Timeline.{nameof(AtomAnimation)}.{nameof(RebuildAnimationNow)}: " + exc);
            }
            finally
            {
                _animationRebuildInProgress = false;
            }

            onAnimationRebuilt.Invoke();
        }

        private void RebuildAnimationNowImpl()
        {
            var sw = Stopwatch.StartNew();
            foreach (var layer in index.clipsGroupedByLayer)
            {
                AtomAnimationClip last = null;
                foreach (var clip in layer)
                {
                    clip.Validate();
                    clip.Rebuild(last);
                    last = clip;
                }
            }
            foreach (var clip in clips)
            {
                RebuildTransition(clip);
            }
            foreach (var clip in clips)
            {
                if (!clip.IsDirty()) continue;

                foreach (var target in clip.GetAllTargets())
                {
                    target.dirty = false;
                    target.onAnimationKeyframesRebuilt.Invoke();
                }

                clip.onAnimationKeyframesRebuilt.Invoke();
            }
            if (sw.ElapsedMilliseconds > 1000)
            {
                SuperController.LogError($"Timeline.{nameof(RebuildAnimationNowImpl)}: Suspiciously long animation rebuild ({sw.Elapsed})");
            }
        }

        private void RebuildTransition(AtomAnimationClip clip)
        {
            if (clip.autoTransitionPrevious)
            {
                var previous = clips.FirstOrDefault(c => c.nextAnimationName == clip.animationName);
                if (previous != null && (previous.IsDirty() || clip.IsDirty()))
                {
                    CopySourceFrameToClip(previous, previous.animationLength, clip, 0f);
                }
            }
            if (clip.autoTransitionNext)
            {
                var next = GetClip(clip.animationSegment, clip.animationLayer, clip.nextAnimationName);
                if (next != null && (next.IsDirty() || clip.IsDirty()))
                {
                    CopySourceFrameToClip(next, 0f, clip, clip.animationLength);
                }
            }
        }

        private static void CopySourceFrameToClip(AtomAnimationClip source, float sourceTime, AtomAnimationClip clip, float clipTime)
        {
            foreach (var sourceTarget in source.targetControllers)
            {
                if (!sourceTarget.EnsureParentAvailable()) continue;
                var currentTarget = clip.targetControllers.FirstOrDefault(t => t.TargetsSameAs(sourceTarget));
                if (currentTarget == null) continue;
                if (!currentTarget.EnsureParentAvailable()) continue;
                // TODO: If there's a parent for position but not rotation or vice versa there will be problems
                // ReSharper disable Unity.NoNullCoalescing
                var sourceParent = sourceTarget.GetPositionParentRB()?.transform ?? sourceTarget.animatableRef.controller.control.parent;
                var currentParent = currentTarget.GetPositionParentRB()?.transform ?? currentTarget.animatableRef.controller.control.parent;
                // ReSharper restore Unity.NoNullCoalescing
                if (sourceParent == currentParent)
                {
                    currentTarget.SetCurveSnapshot(clipTime, sourceTarget.GetCurveSnapshot(sourceTime), false);
                    currentTarget.ChangeCurveByTime(clipTime, CurveTypeValues.Linear, false);
                }
                else
                {
                    var position = sourceParent.TransformPoint(sourceTarget.EvaluatePosition(sourceTime));
                    var rotation = Quaternion.Inverse(sourceParent.rotation) * sourceTarget.EvaluateRotation(sourceTime);
                    currentTarget.SetKeyframeByTime(clipTime, currentParent.TransformPoint(position), Quaternion.Inverse(currentParent.rotation) * rotation, CurveTypeValues.Linear, false);
                }
            }
            foreach (var sourceTarget in source.targetFloatParams)
            {
                var currentTarget = clip.targetFloatParams.FirstOrDefault(t => t.TargetsSameAs(sourceTarget));
                if (currentTarget == null) continue;
                currentTarget.value.SetKeySnapshot(clipTime, sourceTarget.value.GetKeyframeAt(sourceTime));
                currentTarget.ChangeCurveByTime(clipTime, CurveTypeValues.Linear, false);
            }
        }

        #endregion
    }
}
