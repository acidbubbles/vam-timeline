using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class MocapReduceSettings
    {
        public float maxFramesPerSecond;
        public float minPosDelta;
        public float minRotDelta;
    }

    public class MocapReduceOperations : MocapOperationsBase
    {
        public class ReducerBucket
        {
            public int from;
            public int to;
            public int keyWithLargestPositionDistance = -1;
            public float largestPositionDistance;
            public int keyWithLargestRotationAngle = -1;
            public float largestRotationAngle;
        }

        private readonly MocapReduceSettings _settings;

        public MocapReduceOperations(Atom containingAtom, AtomAnimation animation, AtomAnimationClip clip, MocapReduceSettings settings)
            : base(containingAtom, animation, clip)
        {
            _settings = settings;
        }

        protected override IEnumerable ProcessController(MotionAnimationClip clip, FreeControllerAnimationTarget target, FreeControllerV3 ctrl)
        {
            var minFrameDistance = 1f / _settings.maxFramesPerSecond;
            var maxIterations = (int)(_clip.animationLength * 10);

            var steps = clip.steps
                .Where(s => s.positionOn || s.rotationOn)
                .TakeWhile(s => s.timeStep <= _clip.animationLength)
                .GroupBy(s => s.timeStep.Snap(minFrameDistance).ToMilliseconds())
                .Select(g =>
                {
                    var step = g.OrderBy(s => Math.Abs(g.Key - s.timeStep)).First();
                    return ControllerKeyframe.FromStep((g.Key / 1000f).Snap(), step, ctrl);
                })
                .ToList();

            if (steps.Count < 2) yield break;

            target.SetKeyframe(0f, steps[0].position, steps[0].rotation, CurveTypeValues.SmoothLocal);
            target.SetKeyframe(_clip.animationLength, steps[steps.Count - 1].position, steps[steps.Count - 1].rotation, CurveTypeValues.SmoothLocal);
            target.ComputeCurves();

            var buckets = new List<ReducerBucket>
            {
                Scan(steps, target, 1, steps.Count - 2)
            };

            for (var iteration = 0; iteration < maxIterations; iteration++)
            {
                // Scan for largest difference with curve
                var bucketWithLargestPositionDistance = -1;
                var keyWithLargestPositionDistance = -1;
                var largestPositionDistance = 0f;
                var bucketWithLargestRotationAngle = -1;
                var keyWithLargestRotationAngle = -1;
                var largestRotationAngle = 0f;
                for (var bucketIndex = 0; bucketIndex < buckets.Count; bucketIndex++)
                {
                    var bucket = buckets[bucketIndex];
                    if (bucket.largestPositionDistance > largestPositionDistance)
                    {
                        largestPositionDistance = bucket.largestPositionDistance;
                        keyWithLargestPositionDistance = bucket.keyWithLargestPositionDistance;
                        bucketWithLargestPositionDistance = bucketIndex;
                    }
                    if (bucket.largestRotationAngle > largestRotationAngle)
                    {
                        largestRotationAngle = bucket.largestRotationAngle;
                        keyWithLargestRotationAngle = bucket.keyWithLargestRotationAngle;
                        bucketWithLargestRotationAngle = bucketIndex;
                    }
                }

                // Cannot find large enough diffs, exit
                if (keyWithLargestRotationAngle == -1 && keyWithLargestPositionDistance == -1) break;
                var posInRange = largestPositionDistance >= _settings.minPosDelta;
                var rotInRange = largestRotationAngle >= _settings.minRotDelta;
                if (!posInRange && !rotInRange) break;

                // This is an attempt to compare translations and rotations
                var normalizedPositionDistance = largestPositionDistance / 0.4f;
                var normalizedRotationAngle = largestRotationAngle / 180f;
                var selectPosOverRot = (normalizedPositionDistance > normalizedRotationAngle) && posInRange;
                var keyToApply = selectPosOverRot ? keyWithLargestPositionDistance : keyWithLargestRotationAngle;

                var step = steps[keyToApply];
                var key = target.SetKeyframe(step.time, step.position, step.rotation);
                target.SmoothNeighbors(key);

                int bucketToSplitIndex;
                if (selectPosOverRot)
                    bucketToSplitIndex = bucketWithLargestPositionDistance;
                else
                    bucketToSplitIndex = bucketWithLargestRotationAngle;

                if (bucketToSplitIndex > -1)
                {
                    // Split buckets and exclude the scanned keyframe, we never have to scan it again.
                    var bucketToSplit = buckets[bucketToSplitIndex];
                    buckets.RemoveAt(bucketToSplitIndex);
                    if (bucketToSplit.to - keyToApply + 1 > 2)
                        buckets.Insert(bucketToSplitIndex, Scan(steps, target, keyToApply + 1, bucketToSplit.to));
                    if (keyToApply - 1 - bucketToSplit.from > 2)
                        buckets.Insert(bucketToSplitIndex, Scan(steps, target, bucketToSplit.from, keyToApply - 1));
                    }

                yield return 0;
            }
        }

        private ReducerBucket Scan(List<ControllerKeyframe> steps, FreeControllerAnimationTarget target, int from, int to)
        {
            var bucket = new ReducerBucket
            {
                from = from,
                to = to
            };
            for (var i = from; i <= to; i++)
            {
                var step = steps[i];
                var positionDiff = Vector3.Distance(
                    new Vector3(
                        target.x.Evaluate(step.time),
                        target.y.Evaluate(step.time),
                        target.z.Evaluate(step.time)
                    ),
                    step.position
                );
                if (positionDiff > bucket.largestPositionDistance)
                {
                    bucket.largestPositionDistance = positionDiff;
                    bucket.keyWithLargestPositionDistance = i;
                }

                var rotationAngle = Vector3.Angle(
                    new Quaternion(
                        target.rotX.Evaluate(step.time),
                        target.rotY.Evaluate(step.time),
                        target.rotZ.Evaluate(step.time),
                        target.rotW.Evaluate(step.time)
                    ).eulerAngles,
                    step.rotation.eulerAngles
                    );
                if (rotationAngle > bucket.largestRotationAngle)
                {
                    bucket.largestRotationAngle = rotationAngle;
                    bucket.keyWithLargestRotationAngle = i;
                }
            }
            return bucket;
        }
    }
}
