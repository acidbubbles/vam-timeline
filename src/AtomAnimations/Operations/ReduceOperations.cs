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
        private readonly ReduceSettings _settings;

        public ReduceOperations(AtomAnimationClip clip, ReduceSettings settings)
        {
            _clip = clip;
            _settings = settings;
        }

        public IEnumerator ReduceKeyframes(List<ICurveAnimationTarget> targets, Action<ReduceProgress> progress, Action callback)
        {
            SuperController.LogMessage($"Timeline: Reducing {targets.Count} targets. Please wait...");

            var originalTime = Time.realtimeSinceStartup;
            var originalFrames = 0;
            var reducedFrames = 0;

            var steps = targets.Count;
            var startTime = Time.realtimeSinceStartup;
            var done = 0;

            foreach (var target in targets.OfType<FreeControllerV3AnimationTarget>())
            {
                if (Input.GetKey(KeyCode.Escape)) continue;
                var initialFrames = target.x.length;
                var initialTime = Time.realtimeSinceStartup;
                originalFrames += initialFrames;
                target.StartBulkUpdates();
                try
                {
                    var enumerator = Process(new ControllerTargetReduceProcessor(target, _settings));
                    while (enumerator.MoveNext() && !Input.GetKey(KeyCode.Escape))
                        yield return enumerator.Current;
                    SuperController.LogMessage($"Timeline: Reduced {target.animatableRef.name} from {initialFrames} frames to {target.x.length} frames in {Time.realtimeSinceStartup - initialTime:0.00}s");
                    reducedFrames += target.x.length;
                }
                finally
                {
                    target.EndBulkUpdates();
                    target.dirty = true;
                    progress?.Invoke(new ReduceProgress
                    {
                        startTime = startTime,
                        nowTime = Time.realtimeSinceStartup,
                        stepsTotal = steps,
                        stepsDone = ++done
                    });
                }
                yield return 0;
                yield return 0;
                yield return 0;
            }

            foreach (var target in targets.OfType<JSONStorableFloatAnimationTarget>())
            {
                if (Input.GetKey(KeyCode.Escape)) continue;
                var initialFrames = target.value.length;
                var initialTime = Time.realtimeSinceStartup;
                originalFrames += initialFrames;
                target.StartBulkUpdates();
                try
                {
                    var enumerator = Process(new FloatParamTargetReduceProcessor(target, _settings));
                    while (enumerator.MoveNext() && !Input.GetKey(KeyCode.Escape))
                        yield return enumerator.Current;
                    reducedFrames += target.value.length;
                    SuperController.LogMessage($"Timeline: Reduced {target.name} from {initialFrames} frames to {target.value.length} frames in {Time.realtimeSinceStartup - initialTime:0.00}s");
                }
                finally
                {
                    target.EndBulkUpdates();
                    target.dirty = true;
                    progress?.Invoke(new ReduceProgress
                    {
                        startTime = startTime,
                        nowTime = Time.realtimeSinceStartup,
                        stepsTotal = steps,
                        stepsDone = ++done
                    });
                }
                yield return 0;
                yield return 0;
                yield return 0;
            }

            SuperController.LogMessage($"Timeline: Reduction complete from {originalFrames} frames to {reducedFrames} frames ({(originalFrames == 0 ? 0 : reducedFrames / (float)originalFrames * 100f):0.00}%) in {Time.realtimeSinceStartup - originalTime:0.00}s");
            callback?.Invoke();
        }

        private IEnumerator Process(ITargetReduceProcessor processor)
        {
            var fps = (float) _settings.fps;
            var animationLength = _clip.animationLength;
            var maxIterations = (int)(animationLength * 10);

            // STEP 1: Fine flat sections
            if (_settings.removeFlats)
            {
                RemoveFlats(processor);
            }

            yield return 0;

            // STEP 2: Run the reduce algo
            if (_settings.simplify)
            {
                processor.Branch();

                var buckets = GenerateInitialSimplifyBuckets(processor);

                for (var iteration = 0; iteration < maxIterations; iteration++)
                {
                    if (!ProcessSimplifyIteration(processor, buckets))
                        break;

                    yield return 0;
                }

                processor.Commit();
            }

            yield return 0;

            // STEP 3: Average keyframes based on the desired FPS
            if (fps < 50)
            {
                AverageToFPS(processor, fps, animationLength, _settings.round);
            }

            yield return 0;
        }

        private static void RemoveFlats(ITargetReduceProcessor processor)
        {
            processor.Branch();
            var lead = processor.target.GetLeadCurve();
            var lastKey = lead.keys.Count - 1;
            var sectionStart = 0;
            for (var key = 1; key < lastKey; key++)
            {
                if (processor.IsStable(sectionStart, key)) continue;

                var keysCount = key - sectionStart;
                var duration = lead.keys[key].time - lead.keys[sectionStart].time;
                if (keysCount > 4 && duration > 0.5f)
                {
                    processor.FlattenToBranch(sectionStart, key - 1);
                }

                processor.CopyToBranch(key);
                sectionStart = key;
            }

            processor.Commit();
        }

        private List<ReducerBucket> GenerateInitialSimplifyBuckets(ITargetReduceProcessor processor)
        {
            var buckets = new List<ReducerBucket>();
            var leadCurve = processor.target.GetLeadCurve();
            if (_settings.removeFlats)
            {
                var sectionStart = 1;
                for (var key = 1; key < leadCurve.length - 1; key++)
                {
                    var curveType = leadCurve.keys[key].curveType;
                    if (curveType == CurveTypeValues.FlatLinear)
                    {
                        // Bucket from the section start until the key before. This one will be skipped.
                        buckets.Add(processor.CreateBucket(sectionStart, key - 1));
                        processor.CopyToBranch(key);
                        processor.CopyToBranch(key + 1);
                        // Also skip the next one (end of linear section)
                        sectionStart = ++key;
                    }
                }

                if (sectionStart < leadCurve.length - 3)
                    buckets.Add(processor.CreateBucket(sectionStart, leadCurve.length - 2));
            }
            else
            {
                buckets.Add(processor.CreateBucket(1, leadCurve.length - 2));
            }

            return buckets;
        }

        private static bool ProcessSimplifyIteration(ITargetReduceProcessor processor, IList<ReducerBucket> buckets)
        {
            // Scan for largest difference with curve
            var bucketWithLargestDelta = -1;
            var keyWithLargestDelta = -1;
            var largestDelta = 0f;
            for (var bucketIndex = 0; bucketIndex < buckets.Count; bucketIndex++)
            {
                var bucket = buckets[bucketIndex];
                if (bucket.largestDelta > largestDelta)
                {
                    largestDelta = bucket.largestDelta;
                    keyWithLargestDelta = bucket.keyWithLargestDelta;
                    bucketWithLargestDelta = bucketIndex;
                }
            }

            // Cannot find large enough diffs, exit
            if (keyWithLargestDelta == -1) return false;
            if (largestDelta < 1f) return false;

            processor.CopyToBranch(keyWithLargestDelta);

            var bucketToSplitIndex = bucketWithLargestDelta;

            if (bucketToSplitIndex > -1)
            {
                // Split buckets and exclude the scanned keyframe, we never have to scan it again.
                var bucketToSplit = buckets[bucketToSplitIndex];
                buckets.RemoveAt(bucketToSplitIndex);
                if (bucketToSplit.to - keyWithLargestDelta + 1 > 2)
                    buckets.Insert(bucketToSplitIndex, processor.CreateBucket(keyWithLargestDelta + 1, bucketToSplit.to));
                if (keyWithLargestDelta - 1 - bucketToSplit.from > 2)
                    buckets.Insert(bucketToSplitIndex, processor.CreateBucket(bucketToSplit.from, keyWithLargestDelta - 1));
            }

            return true;
        }

        private static void AverageToFPS(ITargetReduceProcessor processor, float fps, float animationLength, bool round)
        {
            var frameDistance = Mathf.Max(1f / fps, 0.001f);
            var halfFrameDistance = frameDistance / 2f;
            var lead = processor.target.GetLeadCurve();
            var toKey = 0;
            processor.Branch();
            for (var keyTime = -halfFrameDistance; keyTime <= animationLength; keyTime += frameDistance)
            {
                var fromKey = toKey;
                var fromNormalized = processor.GetComparableNormalizedValue(fromKey);
                var mostMeaningfulKey = fromKey;
                var specialKey = -1;
                var maxDelta = 0f;
                while (toKey < lead.length - 1)
                {
                    var key = lead.keys[toKey];
                    if (key.time >= keyTime + frameDistance) break;
                    var delta = Mathf.Abs(fromNormalized - processor.GetComparableNormalizedValue(toKey));
                    if (delta > maxDelta)
                    {
                        mostMeaningfulKey = toKey;
                        maxDelta = delta;
                    }
                    if (key.curveType != CurveTypeValues.SmoothLocal)
                        specialKey = toKey;
                    toKey++;
                }

                var time = (round ? keyTime + halfFrameDistance : lead.keys[mostMeaningfulKey].time).Snap();
                if (specialKey > -1)
                {
                    processor.CopyToBranch(specialKey, CurveTypeValues.Undefined, time);
                }
                else if (toKey - fromKey > 0)
                {
                    processor.AverageToBranch(time, fromKey, toKey);
                }
            }
            processor.Commit();
        }
    }
}
