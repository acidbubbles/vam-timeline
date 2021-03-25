using UnityEngine;

namespace VamTimeline
{
    public class ControllerTargetReduceProcessor : TargetReduceProcessorBase<FreeControllerAnimationTarget>, ITargetReduceProcessor
    {
        ICurveAnimationTarget ITargetReduceProcessor.target => base.target;

        public ControllerTargetReduceProcessor(FreeControllerAnimationTarget target, ReduceSettings settings)
            : base(target, settings)
        {
        }

        public void CopyToBranch(int key)
        {
            var time = target.x.keys[key].time;
            branch.SetSnapshot(time, target.GetSnapshot(time));
            var branchKey = branch.x.KeyframeBinarySearch(time);
            branch.SmoothNeighbors(branchKey);
        }

        public void AverageToBranch(float keyTime, int fromKey, int toKey)
        {
            var position = Vector3.zero;
            var rotationCum = Vector4.zero;
            var firstRotation = target.GetKeyframeRotation(fromKey);
            var duration = target.x.GetKeyframeByKey(toKey).time - target.x.GetKeyframeByKey(fromKey).time;
            for (var key = fromKey; key < toKey; key++)
            {
                var frameDuration = target.x.GetKeyframeByKey(key + 1).time - target.x.GetKeyframeByKey(key).time;
                var weight = frameDuration / duration;
                position += target.GetKeyframePosition(key) * weight;
                QuaternionUtil.AverageQuaternion(ref rotationCum, target.GetKeyframeRotation(key), firstRotation, weight);
            }
            branch.SetKeyframe(keyTime, position, target.GetKeyframeRotation(fromKey), CurveTypeValues.SmoothLocal);

        }

        public override ReducerBucket CreateBucket(int from, int to)
        {
            var bucket = base.CreateBucket(from, to);
            for (var i = from; i <= to; i++)
            {
                var time = target.x.keys[i].time;

                var positionDiff = Vector3.Distance(
                    branch.EvaluatePosition(time),
                    target.EvaluatePosition(time)
                );
                var rotationAngle = Quaternion.Angle(
                    branch.EvaluateRotation(time),
                    target.EvaluateRotation(time)
                );
                // This is an attempt to compare translations and rotations
                // TODO: Normalize the values, investigate how to do this with settings
                var normalizedPositionDistance = settings.minMeaningfulDistance > 0 ? positionDiff / settings.minMeaningfulDistance : 1f;
                var normalizedRotationAngle = settings.minMeaningfulRotation > 0 ? rotationAngle / settings.minMeaningfulRotation : 1f;
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
