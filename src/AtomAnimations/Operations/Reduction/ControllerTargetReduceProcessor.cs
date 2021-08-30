using UnityEngine;

namespace VamTimeline
{
    public class ControllerTargetReduceProcessor : TargetReduceProcessorBase<FreeControllerV3AnimationTarget>, ITargetReduceProcessor
    {
        ICurveAnimationTarget ITargetReduceProcessor.target => source;

        public ControllerTargetReduceProcessor(FreeControllerV3AnimationTarget source, ReduceSettings settings)
            : base(source, settings)
        {
        }

        public void CopyToBranch(int key, int curveType = CurveTypeValues.Undefined)
        {
            var time = source.x.keys[key].time;
            branch.SetSnapshot(time, source.GetSnapshot(time));
            var branchKey = branch.x.KeyframeBinarySearch(time);
            if (branchKey == -1) return;
            if(curveType != CurveTypeValues.Undefined)
                branch.ChangeCurveByKey(branchKey, curveType, false);
            branch.RecomputeKey(branchKey);
        }

        public void AverageToBranch(float keyTime, int fromKey, int toKey)
        {
            var position = Vector3.zero;
            var rotationCum = Vector4.zero;
            var firstRotation = source.GetKeyframeRotation(fromKey);
            var duration = source.x.GetKeyframeByKey(toKey).time - source.x.GetKeyframeByKey(fromKey).time;
            for (var key = fromKey; key < toKey; key++)
            {
                var frameDuration = source.x.GetKeyframeByKey(key + 1).time - source.x.GetKeyframeByKey(key).time;
                var weight = frameDuration / duration;
                position += source.GetKeyframePosition(key) * weight;
                QuaternionUtil.AverageQuaternion(ref rotationCum, source.GetKeyframeRotation(key), firstRotation, weight);
            }
            branch.SetKeyframe(keyTime, position, source.GetKeyframeRotation(fromKey), CurveTypeValues.SmoothLocal);

        }

        public bool IsStable(int key1, int key2)
        {
            var positionDiff = Vector3.Distance(
                source.GetKeyframePosition(key1),
                source.GetKeyframePosition(key2)
            );
            if (positionDiff >= settings.minMeaningfulDistance / 10f) return false;
            var rotationDot = 1f - Mathf.Abs(Quaternion.Dot(
                source.GetKeyframeRotation(key1),
                source.GetKeyframeRotation(key2)
            ));
            if (rotationDot >= settings.minMeaningfulRotation / 10f) return false;
            return true;
        }

        public override ReducerBucket CreateBucket(int from, int to)
        {
            var bucket = base.CreateBucket(from, to);
            for (var i = from; i <= to; i++)
            {
                var time = source.x.keys[i].time;

                var positionDiff = Vector3.Distance(
                    branch.EvaluatePosition(time),
                    source.EvaluatePosition(time)
                );
                var rotationDot = 1f - Mathf.Abs(Quaternion.Dot(
                    branch.EvaluateRotation(time),
                    source.EvaluateRotation(time)
                ));
                // This is an attempt to compare translations and rotations
                // TODO: Normalize the values, investigate how to do this with settings
                var normalizedPositionDistance = settings.minMeaningfulDistance > 0 ? positionDiff / settings.minMeaningfulDistance : 1f;
                var normalizedRotationAngle = settings.minMeaningfulRotation > 0 ? rotationDot / settings.minMeaningfulRotation : 1f;
                var delta = normalizedPositionDistance + normalizedRotationAngle;
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
