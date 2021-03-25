using UnityEngine;

namespace VamTimeline
{
    public class FloatParamTargetReduceProcessor : TargetReduceProcessorBase<FloatParamAnimationTarget>, ITargetReduceProcessor
    {
        ICurveAnimationTarget ITargetReduceProcessor.target => base.target;

        public FloatParamTargetReduceProcessor(FloatParamAnimationTarget target, ReduceSettings settings)
            : base(target, settings)
        {
        }


        public void CopyToBranch(int key)
        {
            var branchKey = branch.value.SetKeyframe(target.value.keys[key].time, target.value.keys[key].value, CurveTypeValues.SmoothLocal);
            branch.value.SmoothNeighbors(branchKey);
        }

        public void AverageToBranch(float keyTime, int fromKey, int toKey)
        {
            var timeSum = 0f;
            var valueSum = 0f;
            for (var key = fromKey; key < toKey; key++)
            {
                var frame = target.value.GetKeyframeByKey(key);
                valueSum += frame.value;
                timeSum += target.value.GetKeyframeByKey(key + 1).time - frame.time;
            }

            branch.SetKeyframe(keyTime, valueSum / timeSum, false);
        }

        public override ReducerBucket CreateBucket(int from, int to)
        {
            var bucket = base.CreateBucket(from, to);
            for (var i = from; i <= to; i++)
            {
                var time = target.value.keys[i].time;
                // TODO: Normalize the delta values based on range
                float delta;
                if (settings.minMeaningfulFloatParamRangeRatio > 0)
                    delta = Mathf.Abs(
                        branch.value.Evaluate(time) -
                        target.value.Evaluate(time)
                    ) / (target.floatParam.max - target.floatParam.min) / settings.minMeaningfulFloatParamRangeRatio;
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
