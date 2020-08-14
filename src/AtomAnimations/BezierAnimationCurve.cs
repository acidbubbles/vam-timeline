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

        private float[] _k;
        private float[] _p1;
        private float[] _p2;
        private float[] _r;
        private float[] _a;
        private float[] _b;
        private float[] _c;

        private int _lastIndex;

        public BezierKeyframe GetFirstFrame()
        {
            return keys[0];
        }

        public BezierKeyframe GetLastFrame()
        {
            return keys[keys.Count - 1];
        }

        public BezierKeyframe GetKeyframeAt(float time)
        {
            if (keys.Count == 0) return null;
            var key = KeyframeBinarySearch(time);
            if (key == -1) return null;
            return keys[key];
        }

        public BezierKeyframe GetKeyframe(int key)
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
                next = _lastIndex < keys.Count - 1 ? keys[_lastIndex + 1] : null;
                if (time >= current.time && time < next.time)
                {
                    return ComputeValue(current, next, time);
                }
                // Attempt next portion
                if (next != null && _lastIndex < keys.Count - 2)
                {
                    current = next;
                    next = _lastIndex < keys.Count - 2 ? keys[_lastIndex + 2] : null;
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
            next = key < keys.Count - 1 ? keys[key + 1] : null;
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

        public int SetKeyframeByKey(int key, float value, int curveType)
        {
            var keyframe = GetKeyframe(key);
            keyframe.value = value;
            keyframe.curveType = curveType;
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
                var keyframe = GetKeyframe(0);
                keyframe.time = 0;
                AddKey(animationLength, keyframe.value, keyframe.curveType);
                return;
            }
            {
                var keyframe = GetKeyframe(0);
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
                key.controlPointIn = key.value;
                key.controlPointOut = key.value;
                keys[0] = key;
                return;
            }
            if (keysCount == 2 && !loop)
            {
                var first = keys[0];
                first.controlPointIn = first.value;
                first.controlPointOut = first.value;
                keys[0] = first;
                var last = keys[1];
                last.controlPointIn = last.value;
                last.controlPointOut = last.value;
                keys[1] = last;
                return;
            }

            if (keys.Any(k => k.curveType == CurveTypeValues.Auto))
                AutoComputeControlPoints();

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
                    previous = null;
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
                    next = null;
                    nextTime = keys[keysCount - 1].time;
                }

                switch (current.curveType)
                {
                    case CurveTypeValues.Linear:
                        if (previous != null)
                            current.controlPointIn = current.value - ((current.value - previous.value) / 3f);
                        else
                            current.controlPointIn = current.value;
                        if (next != null)
                            current.controlPointOut = current.value + ((next.value - current.value) / 3f);
                        else
                            current.controlPointOut = current.value;
                        break;
                    case CurveTypeValues.Smooth:
                        {
                            if (next != null && previous != null)
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
                            else if (previous == null)
                            {
                                current.controlPointIn = current.value;
                                current.controlPointOut = current.value + ((next.value - current.value) / 3f);
                            }
                            else if (next == null)
                            {
                                current.controlPointIn = current.value - ((current.value - previous.value) / 3f);
                                current.controlPointOut = current.value;
                            }
                        }
                        break;
                    case CurveTypeValues.LinearFlat:
                        if (previous != null)
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
                        if (previous != null && next != null)
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
                    default:
                        continue;
                }
            }
        }

        public void AutoComputeControlPoints()
        {
            // Adapted from Virt-A-Mate's implementation with permission from MeshedVR
            // Original implementation: https://www.particleincell.com/wp-content/uploads/2012/06/bezier-spline.js
            // Based on: https://www.particleincell.com/2012/bezier-splines/
            // Using improvements on near keyframes: http://www.jacos.nl/jacos_html/spline/
            var n = keys.Count - 1;
            if (_k == null || _k.Length < keys.Count)
            {
                _k = new float[keys.Count];
                _p1 = new float[keys.Count];
                _p2 = new float[keys.Count];
                // rhs vector
                _a = new float[n];
                _b = new float[n];
                _c = new float[n];
                _r = new float[n];
            }
            for (var i = 0; i < keys.Count; i++)
            {
                _k[i] = keys[i].value;
            }

            // leftmost segment
            _a[0] = 0f; // outside the matrix
            _b[0] = 2f;
            _c[0] = 1f;
            _r[0] = _k[0] + 2f * _k[1];

            // internal segments
            for (var i = 1; i < n - 1; i++)
            {
                _a[i] = 1f;
                _b[i] = 4f;
                _c[i] = 1f;
                _r[i] = 4f * _k[i] + 2f * _k[i + 1];
            }

            // right segment
            _a[n - 1] = 2f;
            _b[n - 1] = 7f;
            _c[n - 1] = 0f;
            _r[n - 1] = 8f * _k[n - 1] + _k[n];

            // solves Ax=b with the Thomas algorithm
            for (var i = 1; i < n; i++)
            {
                var m = _a[i] / _b[i - 1];
                _b[i] -= m * _c[i - 1];
                _r[i] -= m * _r[i - 1];
            }

            _p1[n - 1] = _r[n - 1] / _b[n - 1];
            for (var i = n - 2; i >= 0; --i)
            {
                _p1[i] = (_r[i] - _c[i] * _p1[i + 1]) / _b[i];
            }

            // we have p1, now compute p2
            for (var i = 0; i < n - 1; i++)
            {
                _p2[i] = 2f * _k[i + 1] - _p1[i + 1];
            }
            _p2[n - 1] = 0.5f * (_k[n] + _p1[n - 1]);

            // Assign the control points to the keyframes
            for (var i = 1; i < n - 1; i++)
            {
                keys[i].controlPointIn = _p2[i - 1];
                keys[i].controlPointOut = _p1[i];
            }
            if (loop)
            {
                var avgIn = (keys[0].controlPointIn + keys[keys.Count - 1].controlPointIn) / 2f;
                keys[0].controlPointIn = keys[keys.Count - 1].controlPointIn = avgIn;
                var avgOut = (keys[0].controlPointOut + keys[keys.Count - 1].controlPointOut) / 2f;
                keys[0].controlPointOut = keys[keys.Count - 1].controlPointOut = avgOut;
            }
        }

        [MethodImpl(256)]
        public float ComputeValue(BezierKeyframe current, BezierKeyframe next, float time)
        {
            if (next == null) return current.value;
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

            foreach (var key in keys)
            {
                key.time = currentLength - key.time.Snap();
            }
        }

        public void SmoothNeighbors(int key)
        {
            AutoComputeControlPoints();
        }

        public void SetKeySnapshot(float time, BezierKeyframe keyframe)
        {
            var clone = keyframe.Clone();
            clone.time = time;

            if (length == 0)
            {
                AddKey(clone);
                return;
            }

            var index = KeyframeBinarySearch(time);
            if (index == -1)
                AddKey(clone);
            else
                keys[index] = clone;
        }
    }
}
