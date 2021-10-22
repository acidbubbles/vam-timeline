using System;
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

        public void CopyToBranch(int key, int curveType = CurveTypeValues.Undefined, float time = -1)
        {
            if (time < -Mathf.Epsilon)
                time = source.x.keys[key].time;
            branch.SetSnapshot(time, source.GetSnapshot(source.x.keys[key].time));
            var branchKey = branch.x.KeyframeBinarySearch(time);
            if (branchKey == -1) return;
            if (curveType != CurveTypeValues.Undefined)
                branch.ChangeCurveByKey(branchKey, curveType, false);
            branch.RecomputeKey(branchKey);
        }

        public void AverageToBranch(float keyTime, int fromKey, int toKey)
        {
            var position = Vector3.zero;
            var rotationSum = Vector4.zero;
            var firstRotation = source.GetKeyframeRotation(fromKey);
            var duration = source.x.GetKeyframeByKey(toKey).time - source.x.GetKeyframeByKey(fromKey).time;
            for (var key = fromKey; key < toKey; key++)
            {
                var frameDuration = source.x.GetKeyframeByKey(key + 1).time - source.x.GetKeyframeByKey(key).time;
                var weight = frameDuration / duration;
                position += source.GetKeyframePosition(key) * weight;
                QuaternionUtil.AverageQuaternion(ref rotationSum, source.GetKeyframeRotation(key), firstRotation, weight);
            }
            branch.SetKeyframeByTime(keyTime, position, source.GetKeyframeRotation(fromKey), CurveTypeValues.SmoothLocal);

        }

        public void FlattenToBranch(int sectionStart, int sectionEnd)
        {
            var avgPos = Vector3.zero;
            var cumulativeRotation = Vector4.zero;
            var firstRotation = source.GetKeyframeRotation(sectionStart);
            var div = 0f;
            for (var i = sectionStart; i <= sectionEnd; i++)
            {
                avgPos += source.GetKeyframePosition(i);
                QuaternionUtil.AverageQuaternion(ref cumulativeRotation, source.GetKeyframeRotation(i), firstRotation, 1f);
                div += 1f;
            }
            avgPos /= div;
            var avgRot =  QuaternionUtil.FromVector(cumulativeRotation);

            var branchStart = branch.SetKeyframeByTime(source.x.GetKeyframeByKey(sectionStart).time, avgPos, avgRot, CurveTypeValues.FlatLinear);
            var branchEnd = branch.SetKeyframeByTime(source.x.GetKeyframeByKey(sectionEnd).time, avgPos, avgRot, CurveTypeValues.LinearFlat);
            branch.RecomputeKey(branchStart);
            branch.RecomputeKey(branchEnd);
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

        public override float GetComparableNormalizedValue(int key)
        {
            var time = source.x.keys[key].time;

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
            var normalizedPositionDistance = positionDiff / Mathf.Clamp(settings.minMeaningfulDistance, Mathf.Epsilon, Mathf.Infinity);
            var normalizedRotationDot = rotationDot / Mathf.Clamp(settings.minMeaningfulRotation, Mathf.Epsilon, Mathf.Infinity);
            var delta = normalizedPositionDistance + normalizedRotationDot;
            return delta;
        }
    }
}
