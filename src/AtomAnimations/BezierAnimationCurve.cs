using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace VamTimeline
{
    public class BezierAnimationCurve
    {
        public float duration => keys.Count == 0 ? -1 : keys[keys.Count - 1].time;
        public List<BezierKeyframe> keys = new List<BezierKeyframe>();
        public int length => keys.Count;
        public bool loop;

        private int _lastIndex;
        private IBezierAnimationCurveSmoothing _compute;

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
            return keys[key];
        }

        public float Evaluate(float time)
        {
            if (keys.Count < 2) throw new InvalidOperationException($"Cannot evalue curve, at least two keyframes are needed");
            BezierKeyframe current;
            BezierKeyframe next;
            // Attempt last evaluated portion
            if (_lastIndex < keys.Count - 1)
            {
                current = keys[_lastIndex];
                next = _lastIndex < keys.Count - 1 ? keys[_lastIndex + 1] : BezierKeyframe.NullKeyframe;
                if (time >= current.time && time < next.time)
                {
                    return ComputeValue(current, next, time);
                }
                // Attempt next portion
                if (next.HasValue() && _lastIndex < keys.Count - 2)
                {
                    current = next;
                    next = _lastIndex < keys.Count - 2 ? keys[_lastIndex + 2] : BezierKeyframe.NullKeyframe;
                    if (time >= current.time && time < next.time)
                    {
                        _lastIndex++;
                        return ComputeValue(current, next, time);
                    }
                }
            }
            // Attempt first portion
            next = keys[1];
            if (time < next.time)
            {
                _lastIndex = 0;
                return ComputeValue(keys[0], next, time);
            }

            // Search for portion
            var key = KeyframeBinarySearch(time, true);
            if (key == -1) return keys[keys.Count - 1].value;
            if (key == 0) return keys[0].value;
            current = keys[key];
            if (time < current.time)
                current = keys[--key];
            next = key < keys.Count - 1 ? keys[key + 1] : BezierKeyframe.NullKeyframe;
            _lastIndex = key;
            return ComputeValue(current, next, time);
        }

        public int SetKeyframe(float time, float value, int curveType)
        {
            time = time.Snap();
            if (keys.Count == 0) return AddKey(time, value, curveType);
            var key = KeyframeBinarySearch(time);
            if (key != -1) return SetKeyframeByKey(key, value, curveType);
            key = AddKey(time, value, curveType);
            if (key == -1) throw new InvalidOperationException($"Cannot add keyframe at time {time}. Keys: {string.Join(", ", keys.Select(k => k.time.ToString()).ToArray())}.");
            return key;
        }

        [MethodImpl(256)]
        public int SetKeyframeByKey(int key, float value, int curveType)
        {
            var keyframe = GetKeyframeByKey(key);
            keyframe.value = value;
            keyframe.curveType = curveType;
            SetKeyframeByKey(key, keyframe);
            return key;
        }

        [MethodImpl(256)]
        public int SetKeyframeByKey(int key, BezierKeyframe keyframe)
        {
            keys[key] = keyframe;
            return key;
        }

        public int AddKey(float time, float value, int curveType)
        {
            return AddKey(new BezierKeyframe(time, value, curveType));
        }

        public int AddKey(BezierKeyframe keyframe)
        {
            if (keyframe.time > duration)
            {
                keys.Add(keyframe);
                return keys.Count - 1;
            }
            var nearestKey = KeyframeBinarySearch(keyframe.time, true);
            var nearestKeyframe = keys[nearestKey];
            if (Mathf.Approximately(nearestKeyframe.time, keyframe.time)) return -1;
            if (nearestKeyframe.time < keyframe.time)
            {
                var key = nearestKey + 1;
                keys.Insert(key, keyframe);
                return key;
            }
            keys.Insert(nearestKey, keyframe);
            return nearestKey;
        }

        public void RemoveKey(int v)
        {
            keys.RemoveAt(v);
        }

        public void AddEdgeFramesIfMissing(float animationLength, int curveType)
        {
            if (keys.Count == 0)
            {
                AddKey(0, 0, curveType);
                AddKey(animationLength, 0, curveType);
                return;
            }
            if (keys.Count == 1)
            {
                var keyframe = GetKeyframeByKey(0);
                keyframe.time = 0;
                AddKey(animationLength, keyframe.value, keyframe.curveType);
                return;
            }
            {
                var keyframe = GetKeyframeByKey(0);
                if (keyframe.time > 0)
                {
                    if (keys.Count > 2)
                    {
                        AddKey(0, keyframe.value, curveType);
                    }
                    else
                    {
                        keyframe.time = 0;
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
                    }
                    else
                    {
                        keyframe.time = animationLength;
                    }
                }
            }
        }

        public void ComputeCurves()
        {
            var keysCount = keys.Count;
            if (keysCount == 0)
                return;
            if (keysCount == 1)
            {
                var key = keys[0];
                if (key.curveType != CurveTypeValues.LeaveAsIs)
                {
                    key.controlPointIn = key.value;
                    key.controlPointOut = key.value;
                    keys[0] = key;
                }
                return;
            }
            if (keysCount == 2)
            {
                var first = keys[0];
                if (first.curveType != CurveTypeValues.LeaveAsIs)
                {
                    first.controlPointIn = first.value;
                    first.controlPointOut = first.value;
                    keys[0] = first;
                }
                var last = keys[1];
                if (last.curveType != CurveTypeValues.LeaveAsIs)
                {
                    last.controlPointIn = last.value;
                    last.controlPointOut = last.value;
                    keys[1] = last;
                }
                return;
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
                            current.controlPointIn = current.value - ((current.value - previous.value) / 3f);
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
                            current.controlPointIn = current.value - ((current.value - next.value) / 1.4f);
                            current.controlPointOut = current.value + ((previous.value - current.value) / 1.8f);
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
                        continue;
                }

                keys[key] = current;
            }
        }

        private static void LinearInterpolation(BezierKeyframe previous, ref BezierKeyframe current, BezierKeyframe next)
        {
            if (previous.HasValue())
                current.controlPointIn = current.value - ((current.value - previous.value) / 3f);
            else
                current.controlPointIn = current.value;
            if (next.HasValue())
                current.controlPointOut = current.value + ((next.value - current.value) / 3f);
            else
                current.controlPointOut = current.value;
        }

        private static void SmoothLocalInterpolation(BezierKeyframe previous, float previousTime, ref BezierKeyframe current, BezierKeyframe next, float nextTime)
        {
            if (next.HasValue() && previous.HasValue())
            {
                var bothSegmentsDuration = nextTime - previousTime;
                var inRatio = (current.time - previousTime) / bothSegmentsDuration;
                var outRatio = (nextTime - current.time) / bothSegmentsDuration;
                var inHandle = (current.value - previous.value) / 3f;
                var outHandle = (next.value - current.value) / 3f;
                var avg = (inHandle + outHandle) / 2f;
                if (inRatio > outRatio)
                {
                    current.controlPointIn = current.value - avg;
                    current.controlPointOut = current.value + avg * (outRatio / inRatio);
                }
                else
                {
                    current.controlPointIn = current.value - avg * (inRatio / outRatio);
                    current.controlPointOut = current.value + avg;
                }
            }
            else if (previous.IsNull())
            {
                current.controlPointIn = current.value;
                current.controlPointOut = current.value + ((next.value - current.value) / 3f);
            }
            else if (next.IsNull())
            {
                current.controlPointIn = current.value - ((current.value - previous.value) / 3f);
                current.controlPointOut = current.value;
            }
        }

        [MethodImpl(256)]
        public float ComputeValue(BezierKeyframe current, BezierKeyframe next, float time)
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
            float mt = 1f - t;
            float mt2 = mt * mt;
            float mt3 = mt2 * mt;
            float t2 = t * t;
            float t3 = t2 * t;
            return w0 * mt3 + 3f * w1 * mt2 * t + 3f * w2 * mt * t2 + w3 * t3;
        }

        [MethodImpl(256)]
        public int KeyframeBinarySearch(float time, bool returnClosest = false)
        {
            if (time == 0) return 0;
            var timeSmall = time - 0.0001f;
            var timeLarge = time + 0.0001f;

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
                    return middle;
                }
            }
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
            var avg = keys[left].time + ((keys[right].time - keys[left].time) / 2f);
            if (time - avg < 0) return left; else return right;
        }

        public void Reverse()
        {
            if (length < 2) return;

            var currentLength = duration;

            keys.Reverse();

            for (int i = 0; i < keys.Count; i++)
            {
                var keyframe = GetKeyframeByKey(i);
                keyframe.time = currentLength - keyframe.time.Snap();
                SetKeyframeByKey(i, keyframe);
            }
        }

        public void SmoothNeighbors(int key)
        {
            var previous2 = key > 1 ? key - 2 : -1;
            var previous = key > 0 ? key - 1 : -1;
            var current = key;
            var next = key < keys.Count - 1 ? key + 1 : -1;
            var next2 = key < keys.Count - 2 ? key + 2 : -1;
            var currentKeyframe = keys[current];
            if (previous != -1)
            {
                var previousKeyframe = GetKeyframeByKey(previous);
                SmoothLocalInterpolation(keys[previous2], keys[previous2].time, ref previousKeyframe, currentKeyframe, currentKeyframe.time);
                SetKeyframeByKey(previous, previousKeyframe);
            }
            SmoothLocalInterpolation(keys[previous], keys[previous].time, ref currentKeyframe, keys[next], keys[next].time);
            SetKeyframeByKey(current, currentKeyframe);
            if (next != -1)
            {
                var nextKeyframe = GetKeyframeByKey(next);
                SmoothLocalInterpolation(currentKeyframe, currentKeyframe.time, ref nextKeyframe, keys[next2], keys[next2].time);
                SetKeyframeByKey(next, nextKeyframe);
            }
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
