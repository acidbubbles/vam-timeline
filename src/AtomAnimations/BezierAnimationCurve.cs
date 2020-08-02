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

        private float[] _computeValues;
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
            {
                key++;
                current = keys[key];
            }
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

        public void AddEdgeFramesIfMissing(float animationLength)
        {
            if (keys.Count == 0)
            {
                AddKey(0, 0, CurveTypeValues.Smooth);
                AddKey(animationLength, 0, CurveTypeValues.Smooth);
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
                        AddKey(0, keyframe.value, CurveTypeValues.Smooth);
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
                        AddKey(animationLength, keyframe.value, CurveTypeValues.Smooth);
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
            AutoComputeControlPoints();
            for (var key = 0; key < keys.Count; key++)
            {
                var previous = key >= 1 ? keys[key - 1] : (loop ? keys[keys.Count - 2] : null);
                var current = keys[key];
                var next = key < keys.Count - 1 ? keys[key + 1] : (loop ? keys[1] : null);

                switch (current.curveType)
                {
                    case CurveTypeValues.Linear:
                        if (previous != null)
                            current.controlPointIn = current.value - ((current.value - previous.value) / 3f);
                        if (next != null)
                            current.controlPointOut = current.value + ((next.value - current.value) / 3f);
                        break;
                    case CurveTypeValues.Flat:
                    case CurveTypeValues.FlatLong:
                    case CurveTypeValues.LinearFlat:
                    case CurveTypeValues.FlatLinear:
                        current.controlPointIn = current.value;
                        current.controlPointIn = current.value;
                        break;
                    default:
                        // TODO: Implement others
                        continue;
                }
            }
        }

        public void AutoComputeControlPoints()
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

            // Adapted from Virt-A-Mate's implementation with permission from MeshedVR
            // https://www.particleincell.com/wp-content/uploads/2012/06/bezier-spline.js
            // https://www.particleincell.com/2012/bezier-splines/
            if (loop) keysCount -= 1;
            var valuesCount = loop ? keysCount + 1 : keysCount - 1;
            if (_computeValues == null || _computeValues.Length < valuesCount + 1) _computeValues = new float[valuesCount + 1];
            if (_a == null || _a.Length < valuesCount) _a = new float[valuesCount];
            if (_b == null || _b.Length < valuesCount) _b = new float[valuesCount];
            if (_c == null || _c.Length < valuesCount) _c = new float[valuesCount];
            if (_r == null || _r.Length < valuesCount) _r = new float[valuesCount];

            if (loop)
            {
                _computeValues[0] = keys[keysCount - 1].value;
                for (var i = 1; i < valuesCount; i++)
                {
                    _computeValues[i] = keys[i - 1].value;
                }
                _computeValues[valuesCount] = keys[0].value;
            }
            else
            {
                for (var i = 0; i < keysCount; i++)
                {
                    _computeValues[i] = keys[i].value;
                }
            }

            _a[0] = 0f;
            _b[0] = 2f;
            _c[0] = 1f;
            _r[0] = _computeValues[0] + 2f * _computeValues[1];
            for (var i = 1; i < valuesCount - 1; i++)
            {
                _a[i] = 1f;
                _b[i] = 4f;
                _c[i] = 1f;
                _r[i] = 4f * _computeValues[i] + 2f * _computeValues[i + 1];
            }
            _a[valuesCount - 1] = 2f;
            _b[valuesCount - 1] = 7f;
            _c[valuesCount - 1] = 0f;
            _r[valuesCount - 1] = 8f * _computeValues[valuesCount - 1] + _computeValues[valuesCount];
            for (var i = 1; i < valuesCount; i++)
            {
                var n = _a[i] / _b[i - 1];
                _b[i] -= n * _c[i - 1];
                _r[i] -= n * _r[i - 1];
            }

            if (loop)
            {
                var vector = _r[valuesCount - 1] / _b[valuesCount - 1];
                keys[valuesCount - 2].controlPointOut = (_r[valuesCount - 1] - _c[valuesCount - 1] * vector) / _b[valuesCount - 1];
                for (var i = valuesCount - 3; i >= 0; i--)
                {
                    keys[i].controlPointOut = (_r[i + 1] - _c[i + 1] * keys[i + 1].controlPointOut) / _b[i + 1];
                }
            }
            else
            {
                keys[valuesCount].controlPointOut = keys[valuesCount].value;
                keys[valuesCount - 1].controlPointOut = _r[valuesCount - 1] / _b[valuesCount - 1];
                for (var i = valuesCount - 2; i >= 0; i--)
                {
                    keys[i].controlPointOut = (_r[i] - _c[i] * keys[i + 1].controlPointOut) / _b[i];
                }
            }

            if (loop)
            {
                for (var i = 0; i < valuesCount - 1; i++)
                {
                    keys[i].controlPointIn = 2f * _computeValues[i + 1] - keys[i].controlPointOut;
                }
            }
            else
            {
                keys[0].controlPointIn = keys[0].value;
                for (var i = 1; i < valuesCount; i++)
                {
                    keys[i].controlPointIn = 2f * _computeValues[i] - keys[i].controlPointOut;
                }
                keys[valuesCount].controlPointIn = 0.5f * (_computeValues[valuesCount] + keys[valuesCount - 1].controlPointOut);
            }

            if (loop)
            {
                keys[keys.Count - 1].controlPointIn = keys[0].controlPointIn;
                keys[keys.Count - 1].controlPointOut = keys[0].controlPointOut;
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

                // TODO: Use the times array
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
            throw new NotImplementedException();
            // if (key == -1) return;
            // SmoothTangents(key, 1f);
            // if (key > 0) curve.SmoothTangents(key - 1, 1f);
            // if (key < curve.length - 1) curve.SmoothTangents(key + 1, 1f);
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
