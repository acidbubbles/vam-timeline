using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace VamTimeline
{
    public class BezierAnimationCurve
    {
        private const float _epsilon = 0.0001f;
        public float duration => keys.Count == 0 ? -1 : keys[keys.Count - 1].time;
        public List<BezierKeyframe> keys;
        public int length => keys.Count;
        public bool loop;

        private int _lastReferencedKey;
        private IBezierAnimationCurveSmoothing _compute;

        public BezierAnimationCurve()
        {
            keys = new List<BezierKeyframe>();
        }

        public BezierAnimationCurve(IEnumerable<BezierKeyframe> keys)
        {
            this.keys = new List<BezierKeyframe>(keys);
        }

        [MethodImpl(256)]
        public BezierKeyframe GetFirstFrame()
        {
            return keys[0];
        }

        [MethodImpl(256)]
        public BezierKeyframe GetLastFrame()
        {
            return keys[keys.Count - 1];
        }

        [MethodImpl(256)]
        public void SetLastFrame(BezierKeyframe keyframe)
        {
            keys[keys.Count - 1] = keyframe;
        }

        [MethodImpl(256)]
        public BezierKeyframe GetKeyframeAt(float time)
        {
            if (keys.Count == 0) return BezierKeyframe.NullKeyframe;
            var key = KeyframeBinarySearch(time);
            if (key == -1) return BezierKeyframe.NullKeyframe;
            return keys[key];
        }

        [MethodImpl(256)]
        public BezierKeyframe GetKeyframeByKey(int key)
        {
            if (key == -1) throw new ArgumentException("Expected a key, received -1", nameof(key));
            if (key > keys.Count - 1) throw new Exception("WRONG");
            return keys[key];
        }

        public float Evaluate(float time)
        {
            if (keys.Count < 2) throw new InvalidOperationException("Cannot evaluate curve, at least two keyframes are needed");
            // Search for portion
            var key = KeyframeBinarySearch(time, true);
            switch (key)
            {
                case -1:
                    return keys[keys.Count - 1].value;
                case 0:
                    return keys[0].value;
            }
            var current = keys[key];
            if (time < current.time)
                current = keys[--key];
            var next = key < keys.Count - 1 ? keys[key + 1] : BezierKeyframe.NullKeyframe;
            return ComputeValue(current, next, time);
        }

        public int SetKeyframe(float time, float value, int curveType)
        {
            time = time.Snap();
            if (keys.Count == 0) return AddKey(time, value, curveType);
            var key = KeyframeBinarySearch(time);
            if (key != -1) return SetKeyframeByKey(key, value, curveType);
            key = AddKey(time, value, curveType);
            if (key == -1) throw new InvalidOperationException($"Cannot add keyframe at time {time}. Keys: {string.Join(", ", keys.Select(k => k.time.ToString(CultureInfo.InvariantCulture)).ToArray())}.");
            return key;
        }

        [MethodImpl(256)]
        public int SetKeyframeByKey(int key, float value, int curveType)
        {
            var keyframe = GetKeyframeByKey(key);
            keyframe.value = value;
            keyframe.curveType = curveType;
            SetKeyframeByKey(key, keyframe);
            _lastReferencedKey = key;
            return key;
        }

        [MethodImpl(256)]
        public int SetKeyframeByKey(int key, BezierKeyframe keyframe)
        {
            keys[key] = keyframe;
            return key;
        }

        [MethodImpl(256)]
        public int AddKey(float time, float value, int curveType)
        {
            return AddKey(new BezierKeyframe(time, value, curveType));
        }

        public int AddKey(BezierKeyframe keyframe)
        {
            if (keyframe.time > duration)
            {
                keys.Add(keyframe);
                return _lastReferencedKey = keys.Count - 1;
            }

            var nearestKey = KeyframeBinarySearch(keyframe.time, true);
            var nearestKeyframe = keys[nearestKey];
            if (Mathf.Abs(nearestKeyframe.time - keyframe.time) < _epsilon) return -1;
            if (nearestKeyframe.time < keyframe.time)
            {
                var key = nearestKey + 1;
                keys.Insert(key, keyframe);
                _lastReferencedKey = key;
                return key;
            }
            keys.Insert(nearestKey, keyframe);
            _lastReferencedKey = nearestKey;
            return nearestKey;
        }

        public void RemoveKey(int v)
        {
            keys.RemoveAt(v);
        }

        public bool AddEdgeFramesIfMissing(float animationLength, int curveType)
        {
            switch (keys.Count)
            {
                case 0:
                    AddKey(0, 0, curveType);
                    AddKey(animationLength, 0, curveType);
                    return true;
                case 1:
                    {
                        var keyframe = GetKeyframeByKey(0);
                        keyframe.time = 0;
                        keys[0] = keyframe;
                        AddKey(animationLength, keyframe.value, keyframe.curveType);
                        return true;
                    }
            }

            var dirty = false;
            {
                var keyframe = GetKeyframeByKey(0);
                if (keyframe.time > 0)
                {
                    if (keys.Count > 2)
                    {
                        AddKey(0, keyframe.value, curveType);
                        dirty = true;
                    }
                    else if (keyframe.time != 0)
                    {
                        keyframe.time = 0;
                        keys[0] = keyframe;
                        dirty = true;
                    }
                }
            }
            {
                var keyframe = GetLastFrame();
                if (keyframe.time < animationLength)
                {
                    if (length > 2)
                    {
                        AddKey(animationLength, keyframe.value, curveType);
                        dirty = true;
                    }
                    else if (keyframe.time != animationLength)
                    {
                        keyframe.time = animationLength;
                        keys[length - 1] = keyframe;
                        dirty = true;
                    }
                }
            }
            return dirty;
        }

        public void ComputeCurves()
        {
            var keysCount = keys.Count;
            switch (keysCount)
            {
                case 0:
                    return;
                case 1:
                    {
                        var key = keys[0];
                        if (key.curveType == CurveTypeValues.LeaveAsIs) return;
                        key.controlPointIn = key.value;
                        key.controlPointOut = key.value;
                        keys[0] = key;
                        return;
                    }
                case 2:
                    {
                        var first = keys[0];
                        if (first.curveType != CurveTypeValues.LeaveAsIs)
                        {
                            first.controlPointIn = first.value;
                            first.controlPointOut = first.value;
                            keys[0] = first;
                        }
                        var last = keys[1];
                        if (last.curveType == CurveTypeValues.LeaveAsIs) return;
                        last.controlPointIn = last.value;
                        last.controlPointOut = last.value;
                        keys[1] = last;
                        return;
                    }
            }

            var globalSmoothing = false;
            if (keys.Any(k => k.curveType == CurveTypeValues.SmoothGlobal) && keys.Count > 3)
            {
                if (_compute == null || _compute.looping != loop)
                {
                    _compute = loop
                        ? (IBezierAnimationCurveSmoothing)new BezierAnimationCurveSmoothingLooping()
                        : new BezierAnimationCurveSmoothingNonLooping();
                }
                _compute.AutoComputeControlPoints(keys);
                globalSmoothing = true;
            }

            for (var key = 0; key < keysCount; key++)
            {
                ComputeKey(key, keysCount, globalSmoothing);
            }
        }

        private void ComputeKey(int key, int keysCount, bool globalSmoothing)
        {
            BezierKeyframe previous;
            float previousTime;
            if (key >= 1)
            {
                previous = keys[key - 1];
                previousTime = previous.time;
            }
            else if (loop)
            {
                previous = keys[keysCount - 2];
                previousTime = previous.time - keys[keysCount - 1].time;
            }
            else
            {
                previous = BezierKeyframe.NullKeyframe;
                previousTime = 0f;
            }

            var current = keys[key];
            BezierKeyframe next;
            float nextTime;
            if (key < keysCount - 1)
            {
                next = keys[key + 1];
                nextTime = next.time;
            }
            else if (loop)
            {
                next = keys[1];
                nextTime = current.time + next.time;
            }
            else
            {
                next = BezierKeyframe.NullKeyframe;
                nextTime = keys[keysCount - 1].time;
            }

            var curveType = current.curveType;
            if (curveType == CurveTypeValues.CopyPrevious && previous.HasValue())
            {
                current.value = previous.value;
                curveType = previous.curveType == CurveTypeValues.CopyPrevious ? CurveTypeValues.SmoothLocal : previous.curveType;
            }

            switch (curveType)
            {
                case CurveTypeValues.Linear:
                    LinearInterpolation(previous, ref current, next);
                    break;
                case CurveTypeValues.SmoothLocal:
                    SmoothLocalInterpolation(previous, previousTime, ref current, next, nextTime);
                    break;
                case CurveTypeValues.SmoothGlobal:
                    if (!globalSmoothing) SmoothLocalInterpolation(previous, previousTime, ref current, next, nextTime);
                    break;
                case CurveTypeValues.LinearFlat:
                    if (previous.HasValue())
                        current.controlPointIn = current.value - (current.value - previous.value) / 3f;
                    else
                        current.controlPointIn = current.value;
                    current.controlPointOut = current.value;
                    break;
                case CurveTypeValues.Flat:
                case CurveTypeValues.FlatLong:
                case CurveTypeValues.FlatLinear:
                    current.controlPointIn = current.value;
                    current.controlPointOut = current.value;
                    break;
                case CurveTypeValues.Bounce:
                    if (previous.HasValue() && next.HasValue())
                    {
                        current.controlPointIn = current.value - (current.value - next.value) / 1.4f;
                        current.controlPointOut = current.value + (previous.value - current.value) / 1.8f;
                    }
                    else
                    {
                        current.controlPointIn = current.value;
                        current.controlPointOut = current.value;
                    }

                    break;
                case CurveTypeValues.LeaveAsIs:
                    break;
                default:
                    return;
            }

            keys[key] = current;
        }

        private static void LinearInterpolation(BezierKeyframe previous, ref BezierKeyframe current, BezierKeyframe next)
        {
            if (previous.HasValue())
                current.controlPointIn = current.value - (current.value - previous.value) / 3f;
            else
                current.controlPointIn = current.value;
            if (next.HasValue())
                current.controlPointOut = current.value + (next.value - current.value) / 3f;
            else
                current.controlPointOut = current.value;
        }

        private static void SmoothLocalInterpolation(BezierKeyframe previous, float previousTime, ref BezierKeyframe current, BezierKeyframe next, float nextTime)
        {
            if (next.HasValue() && previous.HasValue())
            {
                var inHandle = (current.value - previous.value) / 3f;
                var outHandle = (next.value - current.value) / 3f;
                var bothSegmentsDuration = nextTime - previousTime;
                var inRatio = (current.time - previousTime) / bothSegmentsDuration;
                var outRatio = (nextTime - current.time) / bothSegmentsDuration;
                var avg = inHandle * inRatio + outHandle * outRatio;
                if (inRatio > outRatio)
                {
                    current.controlPointIn = current.value - avg;
                    current.controlPointOut = current.value + avg * outRatio;
                }
                else
                {
                    current.controlPointIn = current.value - avg * inRatio;
                    current.controlPointOut = current.value + avg;
                }
            }
            else if (previous.IsNull())
            {
                current.controlPointIn = current.value;
                current.controlPointOut = current.value + (next.value - current.value) / 3f;
            }
            else if (next.IsNull())
            {
                current.controlPointIn = current.value - (current.value - previous.value) / 3f;
                current.controlPointOut = current.value;
            }
        }

        [MethodImpl(256)]
        public static float ComputeValue(BezierKeyframe current, BezierKeyframe next, float time)
        {
            if (next.IsNull()) return current.value;
            var t = (time - current.time) / (next.time - current.time);
            switch (current.curveType)
            {
                case CurveTypeValues.Constant:
                    return current.value;
                case CurveTypeValues.Linear:
                case CurveTypeValues.FlatLinear:
                    {
                        return current.value + (next.value - current.value) * t;
                    }
            }

            var w0 = current.value;
            var w1 = current.controlPointOut;
            var w2 = next.controlPointIn;
            var w3 = next.value;

            // See https://pomax.github.io/bezierinfo/#how-to-implement-the-weighted-basis-function
            var mt = 1f - t;
            var mt2 = mt * mt;
            var mt3 = mt2 * mt;
            var t2 = t * t;
            var t3 = t2 * t;
            return w0 * mt3 + 3f * w1 * mt2 * t + 3f * w2 * mt * t2 + w3 * t3;
        }

        public int KeyframeBinarySearch(float time, bool returnClosest = false)
        {
            if (time == 0) return _lastReferencedKey = 0;

            var timeSmall = time - _epsilon;
            var timeLarge = time + _epsilon;

            // This is a micro optimization mostly for large animations to avoid binary searching during recording and sequential rewrites
            if (_lastReferencedKey > -1 && _lastReferencedKey < keys.Count)
            {
                var lastIndexTime = keys[_lastReferencedKey].time;

                // Valid last index
                // t: 0 (0s) 1 (2s) 2 (4s) 3 (5s), i: 2 (5s) time: 4s
                if (timeSmall > lastIndexTime)
                {
                    // Try the next few keyframes
                    var maxTime = time + 0.2f;
                    var maxKey = keys.Count - 1;
                    while(_lastReferencedKey < maxKey)
                    {
                        var nextKey = _lastReferencedKey + 1;
                        var nextTime = keys[nextKey].time;
                        if (time < nextTime - _epsilon)
                        {
                            if (!returnClosest) return -1;
                            var midNext = keys[_lastReferencedKey].time + (nextTime - keys[_lastReferencedKey].time) / 2f;
                            return time - midNext < 0 ? _lastReferencedKey : nextKey;
                        }
                        if (time <= nextTime + _epsilon)
                        {
                            return _lastReferencedKey = nextKey;
                        }
                        if (nextTime > maxTime) break;
                        _lastReferencedKey++;
                    }
                }
                else if (timeLarge > lastIndexTime)
                {
                    // Exact match
                    return _lastReferencedKey;
                }
            }

            // This is a MUCH more efficient way to search for a keyframe by time
            var left = 0;
            var right = keys.Count - 1;

            while (left <= right)
            {
                var middle = left + (right - left) / 2;

                var keyTime = keys[middle].time;
                if (keyTime > timeLarge)
                {
                    right = middle - 1;
                }
                else if (keyTime < timeSmall)
                {
                    left = middle + 1;
                }
                else
                {
                    return _lastReferencedKey = middle;
                }
            }
            _lastReferencedKey = left;
            if (!returnClosest)
            {
                return -1;
            }
            if (left > right)
            {
                var tmp = left;
                left = right;
                right = tmp;
            }
            if (right >= keys.Count) return left;
            var avg = keys[left].time + (keys[right].time - keys[left].time) / 2f;
            return time - avg < 0 ? left : right;
        }

        public void Reverse()
        {
            if (length < 2) return;

            var currentLength = duration;

            keys.Reverse();

            for (var i = 0; i < keys.Count; i++)
            {
                var keyframe = GetKeyframeByKey(i);
                keyframe.time = currentLength - keyframe.time.Snap();
                SetKeyframeByKey(i, keyframe);
            }
        }

        public void RecomputeKey(int key)
        {
            var keysCount = keys.Count;
            if (key > 0)
                ComputeKey(key - 1, keysCount, false);
            ComputeKey(key, keysCount, false);
            if(key < keysCount - 1)
                ComputeKey(key + 1, keysCount, false);
        }

        public void SetKeySnapshot(float time, BezierKeyframe keyframe)
        {
            keyframe.time = time;

            if (length == 0)
            {
                AddKey(keyframe);
                return;
            }

            var index = KeyframeBinarySearch(time);
            if (index == -1)
                AddKey(keyframe);
            else
                keys[index] = keyframe;
        }
    }
}
