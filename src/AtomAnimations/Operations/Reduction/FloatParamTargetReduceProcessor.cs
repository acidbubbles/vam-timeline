using UnityEngine;

namespace VamTimeline
{
    public class FloatParamTargetReduceProcessor : TargetReduceProcessorBase<FloatParamAnimationTarget>, ITargetReduceProcessor
    {
        ICurveAnimationTarget ITargetReduceProcessor.target => base.source;

        public FloatParamTargetReduceProcessor(FloatParamAnimationTarget source, ReduceSettings settings)
            : base(source, settings)
        {
        }


        public void CopyToBranch(int sourceKey, int curveType = CurveTypeValues.Undefined)
        {
            var sourceFrame = source.value.keys[sourceKey];
            var branchKey = branch.value.SetKeyframe(sourceFrame.time, sourceFrame.value, CurveTypeValues.SmoothLocal);
            if(curveType != CurveTypeValues.Undefined)
                branch.ChangeCurve(branchKey, curveType);
            branch.value.RecomputeKey(branchKey);
        }

        public void AverageToBranch(float keyTime, int fromKey, int toKey)
        {
            var value = 0f;
            var duration = source.value.GetKeyframeByKey(toKey).time - source.value.GetKeyframeByKey(fromKey).time;
            for (var key = fromKey; key < toKey; key++)
            {
                var frameDuration = source.value.GetKeyframeByKey(key + 1).time - source.value.GetKeyframeByKey(key).time;
                var weight = frameDuration / duration;
                value += source.value.GetKeyframeByKey(key).value * weight;
            }

            branch.SetKeyframe(keyTime, value, false);
        }

        public bool IsStable(int key1, int key2)
        {
            if (settings.minMeaningfulFloatParamRangeRatio <= 0) return false;
            var value1 = source.value.GetKeyframeByKey(key1).value;
            var value2 = source.value.GetKeyframeByKey(key2).value;
            return Mathf.Abs(value2 - value1) / (source.animatableRef.floatParam.max - source.animatableRef.floatParam.min) < (settings.minMeaningfulFloatParamRangeRatio / 10f);
        }

        public override ReducerBucket CreateBucket(int from, int to)
        {
            var bucket = base.CreateBucket(from, to);
            for (var i = from; i <= to; i++)
            {
                var time = source.value.keys[i].time;
                // TODO: Normalize the delta values based on range
                float delta;
                if (settings.minMeaningfulFloatParamRangeRatio > 0)
                    delta = Mathf.Abs(
                        branch.value.Evaluate(time) -
                        source.value.Evaluate(time)
                    ) / (source.animatableRef.floatParam.max - source.animatableRef.floatParam.min) / settings.minMeaningfulFloatParamRangeRatio;
                else
                    delta = 1f;
                if (delta > bucket.largestDelta)
                {
                    bucket.largestDelta = delta;
                    bucket.keyWithLargestDelta = i;
                }
            }

            return bucket;
        }
    }
}
