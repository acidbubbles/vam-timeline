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
            var branchKey = branch.value.SetKeyframe(source.value.keys[sourceKey].time, source.value.keys[sourceKey].value, CurveTypeValues.SmoothLocal);
            if(curveType != CurveTypeValues.Undefined)
                branch.ChangeCurve(branchKey, curveType);
            branch.value.SmoothNeighbors(branchKey);
        }

        public void AverageToBranch(float keyTime, int fromKey, int toKey)
        {
            var timeSum = 0f;
            var valueSum = 0f;
            for (var key = fromKey; key < toKey; key++)
            {
                var frame = source.value.GetKeyframeByKey(key);
                valueSum += frame.value;
                timeSum += source.value.GetKeyframeByKey(key + 1).time - frame.time;
            }

            branch.SetKeyframe(keyTime, valueSum / timeSum, false);
        }

        public bool IsStable(int key1, int key2)
        {
            if (settings.minMeaningfulFloatParamRangeRatio <= 0) return false;
            var value1 = source.value.GetKeyframeByKey(key1).value;
            var value2 = source.value.GetKeyframeByKey(key2).value;
            return Mathf.Abs(value2 - value1) / (source.floatParam.max - source.floatParam.min) < (settings.minMeaningfulFloatParamRangeRatio / 10f);
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
                    ) / (source.floatParam.max - source.floatParam.min) / settings.minMeaningfulFloatParamRangeRatio;
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
