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

        public static void StretchLength(this AnimationCurve curve, float length)
        {
            int lastKey = curve.keys.Length - 1;
            var curveLength = curve.keys[lastKey].time;
            if (length == curveLength) return;
            var ratio = length / curveLength;
            if (Math.Abs(ratio) < float.Epsilon) return;
            int from;
            int to;
            int direction;
            if (ratio < 1f)
            {
                from = 0;
                to = lastKey + 1;
                direction = 1;
            }
            else
            {
                from = lastKey;
                to = -1;
                direction = -1;
            }
            for (var key = from; key != to; key += direction)
            {
                var keyframe = curve.keys[key];
                var time = keyframe.time *= ratio;
                time = (float)(Math.Round(time * 1000f) / 1000f);
                keyframe.time = time;

                curve.MoveKey(key, keyframe);
            }

            // Sanity check
            if (curve.keys[lastKey].time > length + float.Epsilon)
            {
                SuperController.LogError($"VamTimeline: Problem while resizing animation. Expected length {length} but was {curve.keys[lastKey].time}");
            }

            // Ensure exact match
            var lastframe = curve.keys[lastKey];
            lastframe.time = length;
            curve.MoveKey(lastKey, lastframe);
        }

        public static void CropOrExtendLengthEnd(this AnimationCurve curve, float length)
        {
            float currentLength = curve.keys[curve.keys.Length - 1].time;
            if (length < currentLength)
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

        public static void CropOrExtendLengthBegin(this AnimationCurve curve, float length)
        {
            var currentLength = curve.keys[curve.keys.Length - 1].time;
            var lengthDiff = length - currentLength;
            for (var i = curve.keys.Length - 1; i >= 0; i--)
            {
                var keyframe = curve.keys[i];
                keyframe.time += lengthDiff;
                if (keyframe.time < 0)
                    curve.RemoveKey(i);
                else
                    curve.MoveKey(i, keyframe);
            }

            var first = curve.keys[0];
            first.time = 0f;
            curve.MoveKey(0, first);
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
            var before = key > 0 ? (Keyframe?)curve.keys[key - 1] : null;
            var next = key < curve.keys.Length - 1 ? (Keyframe?)curve.keys[key + 1] : null;

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
                    keyframe.inTangent = CalculateFixedMirrorTangent(before, keyframe);
                    keyframe.outTangent = CalculateFixedMirrorTangent(keyframe, next);
                    curve.MoveKey(key, keyframe);
                    break;
                case CurveTypeValues.Smooth:
                    curve.SmoothTangents(key, 0f);
                    break;
                case CurveTypeValues.LinearFlat:
                    keyframe.inTangent = CalculateLinearTangent(before, keyframe);
                    keyframe.outTangent = 0f;
                    curve.MoveKey(key, keyframe);
                    break;
                case CurveTypeValues.FlatLinear:
                    keyframe.inTangent = 0f;
                    keyframe.outTangent = CalculateLinearTangent(keyframe, next);
                    curve.MoveKey(key, keyframe);
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

                var inTangent = CalculateLinearTangent(curve.keys[curve.keys.Length - 2].value, keyframe.value, curve.keys[curve.keys.Length - 2].time, curve.keys[curve.keys.Length - 1].time);
                var outTangent = CalculateLinearTangent(keyframe, curve.keys[1]);
                var tangent = (inTangent + outTangent) / 2f;
                keyframe.inTangent = tangent;
                keyframe.outTangent = tangent;
            }

            keyframe.inWeight = 0.33f;
            keyframe.outWeight = 0.33f;
            curve.MoveKey(0, keyframe);

            keyframe.time = curve.keys[curve.keys.Length - 1].time;
            curve.MoveKey(curve.keys.Length - 1, keyframe);
        }

        private static float CalculateFixedMirrorTangent(Keyframe? from, Keyframe? to, float strength = 0.8f)
        {
            var tangent = CalculateLinearTangent(from, to);
            if (tangent > 0)
                return strength;
            else if (tangent < 0)
                return -strength;
            else
                return 0;
        }

        private static float CalculateLinearTangent(Keyframe? from, Keyframe? to)
        {
            if (from == null || to == null) return 0f;
            return (float)((from.Value.value - (double)to.Value.value) / (from.Value.time - (double)to.Value.time));
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
