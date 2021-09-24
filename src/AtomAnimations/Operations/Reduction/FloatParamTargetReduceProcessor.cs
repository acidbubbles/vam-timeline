using UnityEngine;

namespace VamTimeline
{
    public class FloatParamTargetReduceProcessor : TargetReduceProcessorBase<JSONStorableFloatAnimationTarget>, ITargetReduceProcessor
    {
        ICurveAnimationTarget ITargetReduceProcessor.target => source;

        public FloatParamTargetReduceProcessor(JSONStorableFloatAnimationTarget source, ReduceSettings settings)
            : base(source, settings)
        {
        }


        public void CopyToBranch(int sourceKey, int curveType = CurveTypeValues.Undefined, float time = -1)
        {
            var sourceFrame = source.value.keys[sourceKey];
            if (time < -Mathf.Epsilon)
                time = sourceFrame.time;
            var branchKey = branch.value.SetKeyframe(time, sourceFrame.value, CurveTypeValues.SmoothLocal);
            if(curveType != CurveTypeValues.Undefined)
                branch.ChangeCurveByTime(branchKey, curveType);
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

        public void FlattenToBranch(int sectionStart, int sectionEnd)
        {
            var avg = 0f;
            var div = 0f;
            for (var i = sectionStart; i <= sectionEnd; i++)
            {
                avg += source.value.GetKeyframeByKey(i).value;
                div += 1f;
            }
            avg /= div;

            var branchStart = branch.value.SetKeyframe(source.value.GetKeyframeByKey(sectionStart).time, avg, CurveTypeValues.FlatLinear);
            var branchEnd = branch.value.SetKeyframe(source.value.GetKeyframeByKey(sectionEnd).time, avg, CurveTypeValues.LinearFlat);
            branch.value.RecomputeKey(branchStart);
            branch.value.RecomputeKey(branchEnd);
        }

        public bool IsStable(int key1, int key2)
        {
            if (settings.minMeaningfulFloatParamRangeRatio <= 0) return false;
            var value1 = source.value.GetKeyframeByKey(key1).value;
            var value2 = source.value.GetKeyframeByKey(key2).value;
            return Mathf.Abs(value2 - value1) / (source.animatableRef.floatParam.max - source.animatableRef.floatParam.min) < (settings.minMeaningfulFloatParamRangeRatio / 10f);
        }

        public override float GetComparableNormalizedValue(int key)
        {
            var time = source.value.keys[key].time;
            // TODO: Normalize the delta values based on range
            float delta;
            if (settings.minMeaningfulFloatParamRangeRatio > 0)
                delta = Mathf.Abs(
                    branch.value.Evaluate(time) -
                    source.value.Evaluate(time)
                ) / (source.animatableRef.floatParam.max - source.animatableRef.floatParam.min) / settings.minMeaningfulFloatParamRangeRatio;
            else
                delta = 1f;
            return delta;
        }
    }
}
