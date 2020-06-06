using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
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

        public static void StretchLength(this AnimationCurve curve, float newLength)
        {
            if (curve.length < 2) return;
            int lastKey = curve.length - 1;
            var curveLength = curve[lastKey].time;
            if (newLength == curveLength) return;
            var ratio = newLength / curveLength;
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
                var keyframe = curve[key];
                var time = keyframe.time *= ratio;
                keyframe.time = time.Snap();

                curve.MoveKey(key, keyframe);
            }

            // Sanity check
            if (curve[lastKey].time > newLength + 0.001f - float.Epsilon)
            {
                SuperController.LogError($"VamTimeline: Problem while resizing animation. Expected length {newLength} but was {curve[lastKey].time}");
            }

            // Ensure exact match
            var lastframe = curve[lastKey];
            lastframe.time = newLength;
            curve.MoveKey(lastKey, lastframe);
        }

        public static void CropOrExtendLengthEnd(this AnimationCurve curve, float newLength)
        {
            if (curve.length < 2) return;
            float currentLength = curve[curve.length - 1].time;
            if (newLength < currentLength)
            {
                curve.AddKey(newLength, curve.Evaluate(newLength));
                for (var i = curve.length - 1; i >= 0; i--)
                {
                    if (curve[i].time <= newLength) break;
                    curve.RemoveKey(i);
                }

                var last = curve[curve.length - 1];
                last.time = newLength;
                curve.MoveKey(curve.length - 1, last);
            }
            else if (newLength > currentLength)
            {
                curve.AddKey(newLength, curve[curve.length - 1].value);
            }
        }

        public static void CropOrExtendLengthBegin(this AnimationCurve curve, float newLength)
        {
            if (curve.length < 2) return;
            var currentLength = curve[curve.length - 1].time;
            var lengthDiff = newLength - currentLength;

            var keys = curve.keys.ToList();
            for (var i = keys.Count - 1; i >= 0; i--)
            {
                var keyframe = keys[i];
                float oldTime = keyframe.time;
                float newTime = oldTime + lengthDiff;

                if (newTime < 0)
                {
                    keys.RemoveAt(i);
                    continue;
                }

                keyframe.time = newTime.Snap();
                keys[i] = keyframe;
            }

            if (keys.Count == 0)
            {
                SuperController.LogError("VamTimeline: CropOrExtendLengthBegin resulted in an empty curve.");
                return;
            }

            if (lengthDiff > 0)
            {
                var first = curve[0];
                first.time = 0f;
                keys[0] = first;
            }
            else if (keys[0].time != 0)
            {
                keys.Insert(0, new Keyframe(0f, curve.Evaluate(-lengthDiff)));
            }

            var last = keys[keys.Count - 1];
            last.time = newLength;
            keys[keys.Count - 1] = last;

            curve.keys = keys.ToArray();
        }

        public static void CropOrExtendLengthAtTime(this AnimationCurve curve, float newLength, float time)
        {
            if (curve.length < 2) return;
            var lengthDiff = newLength - curve[curve.length - 1].time;

            var keys = curve.keys.ToList();
            for (var i = 0; i < keys.Count - 1; i++)
            {
                var keyframe = keys[i];
                if (keyframe.time <= time - float.Epsilon) continue;
                keyframe.time = (keyframe.time + lengthDiff).Snap();
                keys[i] = keyframe;
            }

            var last = keys[keys.Count - 1];
            last.time = newLength;
            keys[keys.Count - 1] = last;

            curve.keys = keys.ToArray();
        }

        public static void Reverse(this AnimationCurve curve)
        {
            if (curve.length < 2) return;
            var currentLength = curve[curve.length - 1].time;

            var keys = curve.keys.ToList();

            while (curve.length > 0)
                curve.RemoveKey(0);

            for (var i = 0; i < keys.Count; i++)
            {
                var key = keys[i];
                curve.AddKey((currentLength - key.time).Snap(), key.value);
            }
        }

        #endregion

        #region Keyframes control

        public static int SetKeyframe(this AnimationCurve curve, float time, float value)
        {
            var key = curve.AddKey(time, value);
            if (key != -1)
                return key;

            key = curve.KeyframeBinarySearch(time);
            if (key == -1) throw new InvalidOperationException($"Cannot find keyframe at time {time}, no keys exist at this position. Keys: {string.Join(", ", curve.keys.Select(k => k.time.ToString()).ToArray())}.");
            var keyframe = curve[key];
            keyframe.value = value;
            curve.MoveKey(key, keyframe);
            return key;
        }

        #endregion

        #region Curves

        [MethodImpl(256)]
        public static int KeyframeBinarySearch(this AnimationCurve curve, float time, bool returnClosest = false)
        {
            if (time == 0) return 0;
            if (time == curve[curve.length - 1].time) return curve.length - 1;

            var left = 0;
            var right = curve.length - 1;

            while (left <= right)
            {
                var middle = left + (right - left) / 2;

                var keyTime = curve[middle].time;
                if (keyTime > time)
                {
                    right = middle - 1;
                }
                else if (curve[middle].time < time)
                {
                    left = middle + 1;
                }
                else
                {
                    return middle;
                }
            }
            if (!returnClosest) return -1;
            if (left > right)
            {
                var tmp = left;
                left = right;
                right = tmp;
            }
            var avg = curve[left].time + ((curve[right].time - curve[left].time) / 2f);
            if (time - avg < 0) return left; else return right;
        }

        public static void SmoothAllFrames(this AnimationCurve curve)
        {
            if (curve.length == 2)
            {
                var first = curve[0];
                first.inTangent = 0f;
                first.outTangent = 0f;
                first.inWeight = 0.33f;
                first.outWeight = 0.33f;
                curve.MoveKey(0, first);
                var last = curve[1];
                last.inTangent = 0f;
                last.outTangent = 0f;
                last.inWeight = 0.33f;
                last.outWeight = 0.33f;
                curve.MoveKey(1, last);
                return;
            }

            // First and last frame will be recalculated in loop smoothing
            for (int k = 1; k < curve.length - 1; k++)
            {
                var keyframe = curve[k];
                var inTangent = CalculateLinearTangent(curve[k - 1], keyframe);
                var outTangent = CalculateLinearTangent(keyframe, curve[k + 1]);
                var tangent = inTangent + outTangent / 2f;
                keyframe.inTangent = tangent;
                keyframe.outTangent = tangent;
                keyframe.inWeight = 0.33f;
                keyframe.outWeight = 0.33f;
                curve.MoveKey(k, keyframe);
            }

            var cloneFirstToLastKeyframe = curve[0];
            cloneFirstToLastKeyframe.time = curve[curve.length - 1].time;
            curve.MoveKey(curve.length - 1, cloneFirstToLastKeyframe);
        }

        public static void FlatAllFrames(this AnimationCurve curve)
        {
            for (int k = 1; k < curve.length; k++)
            {
                var keyframe = curve[k];
                keyframe.inTangent = 0f;
                keyframe.outTangent = 0f;
                keyframe.inWeight = 0.33f;
                keyframe.outWeight = 0.33f;
                curve.MoveKey(k, keyframe);
            }
        }

        public static void SmoothLoop(this AnimationCurve curve)
        {
            if (curve.length == 0) return;

            var keyframe = curve[0];

            if (curve.length <= 2)
            {
                keyframe.inTangent = 0f;
                keyframe.outTangent = 0f;
            }
            else
            {
                var inTangent = CalculateLinearTangent(curve[curve.length - 2].value, keyframe.value, curve[curve.length - 2].time, curve[curve.length - 1].time);
                var outTangent = CalculateLinearTangent(keyframe, curve[1]);
                var tangent = (inTangent + outTangent) / 2f;
                keyframe.inTangent = tangent;
                keyframe.outTangent = tangent;
            }

            keyframe.inWeight = 0.33f;
            keyframe.outWeight = 0.33f;
            curve.MoveKey(0, keyframe);

            keyframe.time = curve[curve.length - 1].time;
            curve.MoveKey(curve.length - 1, keyframe);
        }

        [MethodImpl(256)]
        public static float CalculateFixedMirrorTangent(Keyframe? from, Keyframe? to, float strength = 0.8f)
        {
            var tangent = CalculateLinearTangent(from, to);
            if (tangent > 0)
                return strength;
            else if (tangent < 0)
                return -strength;
            else
                return 0;
        }

        [MethodImpl(256)]
        public static float CalculateLinearTangent(Keyframe? from, Keyframe? to)
        {
            if (from == null || to == null) return 0f;
            return (float)((from.Value.value - (double)to.Value.value) / (from.Value.time - (double)to.Value.time));
        }

        [MethodImpl(256)]
        public static float CalculateLinearTangent(float fromValue, float toValue, float fromTime, float toTime)
        {
            return (float)((fromValue - (double)toValue) / (fromTime - (double)toTime));
        }

        #endregion

        #region Snapshots

        public static void SetKeySnapshot(this AnimationCurve curve, float time, Keyframe keyframe)
        {
            var index = curve.KeyframeBinarySearch(time);
            if (index == -1)
                index = curve.AddKey(time, keyframe.value);
            keyframe.time = time;
            curve.MoveKey(index, keyframe);
        }

        #endregion
    }
}
