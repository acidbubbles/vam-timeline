using System;
using UnityEngine;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public static class AnimationCurveExtensions
    {
        #region Control

        public static void SetLength(this AnimationCurve curve, float length)
        {
            if (length > curve.keys[curve.keys.Length - 1].time)
            {
                for (var i = 0; i < curve.keys.Length - 1; i++)
                {
                    if (curve.keys[i].time < length) continue;
                    curve.RemoveKey(i);
                }
            }

            var last = curve.keys[curve.keys.Length - 1];
            last.time = length;
            curve.MoveKey(curve.keys.Length - 1, last);
        }

        #endregion

        #region Keyframes control

        public static int SetKeyframe(this AnimationCurve curve, float time, float value)
        {
            var key = curve.AddKey(time, value);
            Keyframe keyframe;
            if (key == -1)
            {
                key = Array.FindIndex(curve.keys, k => k.time == time);
                if (key == -1) throw new InvalidOperationException($"Cannot AddKey at time {time}, but no keys exist at this position");
                keyframe = curve.keys[key];
                keyframe.value = value;
                curve.MoveKey(key, keyframe);
            }
            return key;
        }

        #endregion

        #region Curves

        public static void ChangeCurve(this AnimationCurve curve, float time, string curveType)
        {
            var key = Array.FindIndex(curve.keys, k => k.time == time);
            if (key == -1) return;
            var keyframe = curve.keys[key];
            var before = curve.keys[key - 1];
            var next = curve.keys[key + 1];

            switch (curveType)
            {
                case null:
                case "":
                    return;
                case CurveTypeValues.Flat:
                    keyframe.inTangent = 0f;
                    keyframe.outTangent = 0f;
                    curve.MoveKey(key, keyframe);
                    break;
                case CurveTypeValues.Linear:
                    keyframe.inTangent = CalculateLinearTangent(before, keyframe);
                    keyframe.outTangent = CalculateLinearTangent(keyframe, next);
                    curve.MoveKey(key, keyframe);
                    break;
                case CurveTypeValues.Bounce:
                    keyframe.inTangent = CalculateTangent(before, keyframe);
                    keyframe.outTangent = CalculateTangent(keyframe, next);
                    curve.MoveKey(key, keyframe);
                    break;
                case CurveTypeValues.Smooth:
                    curve.SmoothTangents(key, 0f);
                    break;
                case CurveTypeValues.LinearFlat:
                    keyframe.inTangent = CalculateTangent(before, keyframe);
                    keyframe.outTangent = 0f;
                    break;
                case CurveTypeValues.FlatLinear:
                    keyframe.inTangent = 0f;
                    keyframe.outTangent = CalculateTangent(keyframe, next);
                    break;
                default:
                    throw new NotSupportedException($"Curve type {curveType} is not supported");
            }
        }

        public static void SmoothAllFrames(this AnimationCurve curve)
        {
            if (curve.keys.Length == 2)
            {
                var first = curve.keys[0];
                first.inTangent = 0f;
                first.outTangent = 0f;
                first.inWeight = 0.33f;
                first.outWeight = 0.33f;
                curve.MoveKey(0, first);
                var last = curve.keys[1];
                last.inTangent = 0f;
                last.outTangent = 0f;
                last.inWeight = 0.33f;
                last.outWeight = 0.33f;
                curve.MoveKey(1, last);
                return;
            }

            // First and last frame will be recalculated in loop smoothing
            for (int k = 1; k < curve.keys.Length - 1; k++)
            {
                var keyframe = curve.keys[k];
                var inTangent = CalculateLinearTangent(curve.keys[k - 1], keyframe);
                var outTangent = CalculateLinearTangent(keyframe, curve.keys[k + 1]);
                var tangent = inTangent + outTangent / 2f;
                keyframe.inTangent = tangent;
                keyframe.outTangent = tangent;
                keyframe.inWeight = 0.33f;
                keyframe.outWeight = 0.33f;
                curve.MoveKey(k, keyframe);
            }

            var cloneFirstToLastKeyframe = curve.keys[0];
            cloneFirstToLastKeyframe.time = curve.keys[curve.keys.Length - 1].time;
            curve.MoveKey(curve.keys.Length - 1, cloneFirstToLastKeyframe);
        }

        public static void FlatAllFrames(this AnimationCurve curve)
        {
            for (int k = 1; k < curve.keys.Length; k++)
            {
                var keyframe = curve.keys[k];
                keyframe.inTangent = 0f;
                keyframe.outTangent = 0f;
                keyframe.inWeight = 0.33f;
                keyframe.outWeight = 0.33f;
                curve.MoveKey(k, keyframe);
            }
        }

        public static void SmoothLoop(this AnimationCurve curve)
        {
            if (curve.keys.Length == 0) return;

            var keyframe = curve.keys[0];

            if (curve.keys.Length <= 2)
            {
                keyframe.inTangent = 0f;
                keyframe.outTangent = 0f;
            }
            else
            {

                var inTangent = CalculateLinearTangent(keyframe, curve.keys[1]);
                var outTangent = CalculateLinearTangent(curve.keys[curve.keys.Length - 2].value, keyframe.value, curve.keys[curve.keys.Length - 2].time, curve.keys[curve.keys.Length - 1].time);
                var tangent = inTangent + outTangent / 2f;
                keyframe.inTangent = tangent;
                keyframe.outTangent = tangent;
            }

            keyframe.inWeight = 0.33f;
            keyframe.outWeight = 0.33f;
            curve.MoveKey(0, keyframe);

            keyframe.time = curve.keys[curve.keys.Length - 1].time;
            curve.MoveKey(curve.keys.Length - 1, keyframe);
        }

        private static float CalculateTangent(Keyframe from, Keyframe to, float strength = 0.8f)
        {
            var tangent = CalculateLinearTangent(from, to);
            if (tangent > 0)
                return strength;
            else if (tangent < 0)
                return -strength;
            else
                return 0;
        }

        private static float CalculateLinearTangent(Keyframe from, Keyframe to)
        {
            return (float)((from.value - (double)to.value) / (from.time - (double)to.time));
        }

        private static float CalculateLinearTangent(float fromValue, float toValue, float fromTime, float toTime)
        {
            return (float)((fromValue - (double)toValue) / (fromTime - (double)toTime));
        }

        #endregion

        #region Snapshots

        public static void SetKeySnapshot(this AnimationCurve curve, float time, Keyframe keyframe)
        {
            var index = Array.FindIndex(curve.keys, k => k.time == time);
            if (index == -1)
                index = curve.AddKey(time, keyframe.value);
            keyframe.time = time;
            curve.MoveKey(index, keyframe);

            if (time == 0f)
            {
                keyframe.time = curve.keys[curve.keys.Length - 1].time;
                curve.MoveKey(curve.keys.Length - 1, keyframe);
            }
        }

        #endregion
    }
}
