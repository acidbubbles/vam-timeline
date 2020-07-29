using System;
using System.Runtime.CompilerServices;

namespace VamTimeline
{
    public static class AnimationCurveExtensions
    {
        #region Keyframes control

        public static void Reverse(this BezierAnimationCurve curve)
        {
            if (curve.length < 2) return;
            var currentLength = curve.GetKeyframe(curve.length - 1).time;

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

        #region Curves

        [MethodImpl(256)]
        public static int KeyframeBinarySearch(this BezierAnimationCurve curve, float time, bool returnClosest = false)
        {
            if (time == 0) return 0;
            if (time == curve.GetKeyframe(curve.length - 1).time) return curve.length - 1;
            var timeSmall = time - 0.0001f;
            var timeLarge = time + 0.0001f;

            var left = 0;
            var right = curve.length - 1;

            while (left <= right)
            {
                var middle = left + (right - left) / 2;

                var keyTime = curve.GetKeyframe(middle).time;
                if (keyTime > timeLarge)
                {
                    right = middle - 1;
                }
                else if (curve.GetKeyframe(middle).time < timeSmall)
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
            var avg = curve.GetKeyframe(left).time + ((curve.GetKeyframe(right).time - curve.GetKeyframe(left).time) / 2f);
            if (time - avg < 0) return left; else return right;
        }

        public static void SmoothNeighbors(this BezierAnimationCurve curve, int key)
        {
            throw new NotImplementedException();
            // if (key == -1) return;
            // curve.SmoothTangents(key, 1f);
            // if (key > 0) curve.SmoothTangents(key - 1, 1f);
            // if (key < curve.length - 1) curve.SmoothTangents(key + 1, 1f);
        }

        [MethodImpl(256)]
        public static float CalculateFixedMirrorTangent(VamKeyframe from, VamKeyframe to, float strength = 0.8f)
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
        public static float CalculateLinearTangent(VamKeyframe from, VamKeyframe to)
        {
            if (from == null || to == null) return 0f;
            return (float)((from.value - (double)to.value) / (from.time - (double)to.time));
        }

        [MethodImpl(256)]
        public static float CalculateLinearTangent(float fromValue, float toValue, float fromTime, float toTime)
        {
            return (float)((fromValue - (double)toValue) / (fromTime - (double)toTime));
        }

        #endregion

        #region Snapshots

        public static void SetKeySnapshot(this BezierAnimationCurve curve, float time, VamKeyframe keyframe)
        {
            if (curve.length == 0)
            {
                curve.AddKey(time, keyframe.value);
                return;
            }

            var index = curve.KeyframeBinarySearch(time);
            if (index == -1)
            {
                curve.AddKey(time, keyframe.value);
            }
            else
            {
                keyframe = keyframe.Clone();
                keyframe.time = time;
                curve.MoveKey(index, keyframe);
            }
        }

        #endregion
    }
}
