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

            var steps = targets.Count;
            var startTime = Time.realtimeSinceStartup;
            var done = 0;

            foreach (var target in targets.OfType<FreeControllerAnimationTarget>())
            {
                var initialFrames = target.x.length;
                var initialTime = Time.realtimeSinceStartup;
                target.StartBulkUpdates();
                try
                {
                    var enumerator = Process(new ControllerTargetReduceProcessor(target, _settings));
                    while (enumerator.MoveNext())
                        yield return enumerator.Current;
                    SuperController.LogMessage($"Timeline: Reduced {target.controller.name} from {initialFrames} frames to {target.x.length} frames in {Time.realtimeSinceStartup - initialTime:0.00}s");
                }
                finally
                {
                    target.dirty = true;
                    target.EndBulkUpdates();
                    progress?.Invoke(new ReduceProgress
                    {
                        startTime = startTime,
                        nowTime = Time.realtimeSinceStartup,
                        stepsTotal = steps,
                        stepsDone = ++done
                    });
                }
                yield return 0;
            }

            foreach (var target in targets.OfType<FloatParamAnimationTarget>())
            {
                var initialFrames = target.value.length;
                var initialTime = Time.realtimeSinceStartup;
                target.StartBulkUpdates();
                try
                {
                    var enumerator = Process(new FloatParamTargetReduceProcessor(target, _settings));
                    while (enumerator.MoveNext())
                        yield return enumerator.Current;
                    SuperController.LogMessage($"Timeline: Reduced {target.GetShortName()} from {initialFrames} frames to {target.value.length} frames in {Time.realtimeSinceStartup - initialTime:0.00}s");
                }
                finally
                {
                    target.dirty = true;
                    target.EndBulkUpdates();
                    progress?.Invoke(new ReduceProgress
                    {
                        startTime = startTime,
                        nowTime = Time.realtimeSinceStartup,
                        stepsTotal = steps,
                        stepsDone = ++done
                    });
                }
                yield return 0;
            }

            callback?.Invoke();
        }

        private IEnumerator Process(ITargetReduceProcessor processor)
        {
            var maxFramesPerSecond = (float) _settings.fps;
            var minFrameDistance = Mathf.Max(1f / maxFramesPerSecond, 0.001f);
            var animationLength = _clip.animationLength;
            var maxIterations = (int)(animationLength * 10);

            // STEP 1: Average keyframes based on the desired FPS
            if (_settings.avgToSnap && maxFramesPerSecond <= 50)
            {
                var avgTimeRange = minFrameDistance / 2f;
                var lead = processor.target.GetLeadCurve();
                var toKey = 0;
                processor.Branch();
                for (var keyTime = 0f; keyTime <= animationLength; keyTime += minFrameDistance)
                {
                    var fromKey = toKey;
                    while (toKey < lead.length - 1 && lead.keys[toKey].time < keyTime + avgTimeRange)
                    {
                        toKey++;
                    }

                    if (toKey - fromKey > 0)
                        processor.AverageToBranch(keyTime.Snap(), fromKey, toKey);
                }
                processor.Commit();
            }

            // STEP 2: Fine flat sections
            if (_settings.removeFlats)
            {
                processor.Branch();
                var lead = processor.target.GetLeadCurve();
                var keysCount = lead.keys.Count - 1;
                var sectionStart = 0;
                for (var key = 1; key < keysCount; key++)
                {
                    if (!processor.IsStable(sectionStart, key))
                    {
                        var duration = lead.keys[key].time - lead.keys[sectionStart].time;
                        if (key - sectionStart > 3 && duration > 0.5f)
                        {
                            processor.CopyToBranch(sectionStart, CurveTypeValues.FlatLinear);
                            processor.CopyToBranch(key - 1, CurveTypeValues.LinearFlat);
                        }
                        processor.CopyToBranch(key);
                        sectionStart = ++key;
                    }
                }
                processor.Commit();
            }

            // STEP 3: Run the reduce algo
            if (_settings.simplify)
            {
                processor.Branch();

                var buckets = new List<ReducerBucket>
                {
                    processor.CreateBucket(1, processor.target.GetLeadCurve().length - 2)
                };

                for (var iteration = 0; iteration < maxIterations; iteration++)
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
                    if (keyWithLargestDelta == -1) break;
                    if (largestDelta < 1f) break; // TODO: Configurable pos and rot weight, pos and rot min change inside bucket scan

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

                    yield return 0;
                }

                processor.Commit();
            }
        }
    }
}
