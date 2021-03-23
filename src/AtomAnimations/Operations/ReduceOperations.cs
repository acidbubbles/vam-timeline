using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class ReduceOperations
    {
        private readonly AtomAnimationClip _clip;

        public ReduceOperations(AtomAnimationClip clip)
        {
            _clip = clip;
        }

        public IEnumerator ReduceKeyframes(List<FloatParamAnimationTarget> targets, float maxFramesPerSecond, float minValueDelta)
        {
            /*
            foreach (var target in animationEditContext.GetAllOrSelectedTargets().OfType<FreeControllerAnimationTarget>())
            {
                target.StartBulkUpdates();
                try
                {
                    // ReduceKeyframes(target.X, target.Y, target.Z, target.RotX, target.RotY, target.RotZ, target.RotW);
                }
                finally
                {
                    target.dirty = true;
                    target.EndBulkUpdates();
                }
                yield return 0;
            }
            */

            foreach (var target in targets)
            {
                target.StartBulkUpdates();
                try
                {
                    ReduceTargetKeyframes(target, maxFramesPerSecond, minValueDelta);
                }
                finally
                {
                    target.dirty = true;
                    target.EndBulkUpdates();
                }
                yield return 0;
            }
        }

        private void ReduceTargetKeyframes(FloatParamAnimationTarget t, float maxFramesPerSecond, float minValueDelta)
        {
            var source = t.value;
            var minFrameDistance = 1f / maxFramesPerSecond;
            var maxIterations = (int)(source.GetKeyframeByKey(source.length - 1).time * 10);

            var steps = source.keys
                .GroupBy(s => s.time.Snap(minFrameDistance).ToMilliseconds())
                .Select(g =>
                {
                    var keyframe = g.OrderBy(s => Math.Abs(g.Key - s.time)).First();
                    return new BezierKeyframe((g.Key / 1000f).Snap(), keyframe.value, 0, 0, keyframe.curveType);
                })
                .ToList();

            var target = new BezierAnimationCurve();
            target.AddKey(0, source.GetFirstFrame().value, source.GetFirstFrame().curveType);
            target.AddKey(source.GetLastFrame().time, source.GetLastFrame().value, source.GetLastFrame().curveType);
            target.SmoothNeighbors(0);

            for (var iteration = 0; iteration < maxIterations; iteration++)
            {
                // Scan for largest difference with curve
                // TODO: Use the buckets strategy
                var keyWithLargestDiff = -1;
                var largestDiff = 0f;
                for (var i = 1; i < steps.Count - 1; i++)
                {
                    var diff = Mathf.Abs(target.Evaluate(steps[i].time) - steps[i].value);

                    if (diff > largestDiff)
                    {
                        largestDiff = diff;
                        keyWithLargestDiff = i;
                    }
                }

                // Cannot find large enough diffs, exit
                if (keyWithLargestDiff == -1) break;
                var inRange = largestDiff >= minValueDelta;
                if (!inRange) break;

                // This is an attempt to compare translations and rotations
                var keyToApply = keyWithLargestDiff;

                var step = steps[keyToApply];
                steps.RemoveAt(keyToApply);
                var key = target.SetKeyframe(step.time, step.value, step.curveType);
                target.SmoothNeighbors(key);
            }

            t.StartBulkUpdates();
            try
            {
                new KeyframesOperations(_clip).RemoveAll(t);
                for (var key = 0; key < target.length; key++)
                {
                    var keyframe = target.GetKeyframeByKey(key);
                    t.SetKeyframe(keyframe.time, keyframe.value);
                }
                t.AddEdgeFramesIfMissing(source.GetKeyframeByKey(source.length - 1).time);
            }
            finally
            {
                t.EndBulkUpdates();
            }
        }
    }
}
