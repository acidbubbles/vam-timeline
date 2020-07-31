using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    /// <see>https://pomax.github.io/bezierinfo/</see>
    public class BezierAnimationCurve
    {
        // TODO: Instead of a keys array, work with four independent arrays: time, value, in, out
        public List<VamKeyframe> keys = new List<VamKeyframe>();
        public int length => keys.Count;
        // TODO: Use correctly
        public bool loop;
        public float[] _computeValues;
        public float[] r;
        public float[] a;
        public float[] b;
        public float[] c;

        public VamKeyframe GetFirstFrame()
        {
            return keys[0];
        }

        public VamKeyframe GetLastFrame()
        {
            // TODO: Add a animationLength property
            return keys[keys.Count - 1];
        }

        public VamKeyframe GetKeyframeAt(float time)
        {
            if (keys.Count == 0) return null;
            var key = this.KeyframeBinarySearch(time);
            if (key == -1) return null;
            return keys[key];
        }

        public VamKeyframe GetKeyframe(int key)
        {
            if (key == -1) throw new ArgumentException("Expected a key, received -1", nameof(key));
            return keys[key];
        }

        public float Evaluate(float time)
        {
            if (keys.Count < 2) throw new NotSupportedException("Must contain at least two keyframes");
            // TODO: Support looping
            // TODO: Remember the largest time and use it everywhere
            // TODO: Remember the last checked time and bisect from there?
            time = Mathf.Clamp(time, 0, keys[keys.Count - 1].time);
            // TODO: Bisect or better, no linear search here!
            var key = keys.FindIndex(k => k.time > time);
            if (key == -1) return keys[keys.Count - 1].value;
            if (key == 0) return keys[0].value;
            var from = keys[key - 1];
            var to = keys[key];
            // TODO: Worth precalculating?
            var t = (time - from.time) / (to.time - from.time);
            return ComputeBezierValue(key - 1, t);
        }

        public void MoveKey(int key, VamKeyframe keyframe)
        {
            if (keys[key].time == keyframe.time)
            {
                keys[key] = keyframe;
            }
            else
            {
                keys.RemoveAt(key);
                AddKey(keyframe);
            }
        }

        // TODO: Clean this up and only use SetKeyframe, not Add/Move.
        public int SetKeyframe(float time, float value, int curveType)
        {
            time = time.Snap();
            if (keys.Count == 0) return AddKey(time, value, curveType);
            var key = this.KeyframeBinarySearch(time);
            if (key != -1)
                return SetKeyframeByKey(key, value, curveType);
            key = AddKey(time, value);
            if (key == -1)
                throw new InvalidOperationException($"Cannot add keyframe at time {time}. Keys: {string.Join(", ", keys.Select(k => k.time.ToString()).ToArray())}.");
            return key;
        }

        public int SetKeyframeByKey(int key, float value, int curveType)
        {
            var keyframe = GetKeyframe(key);
            keyframe.value = value;
            keyframe.curveType = curveType;
            MoveKey(key, keyframe);
            return key;
        }

        public int AddKey(float time, float value, int curveType = 0)
        {
            return AddKey(new VamKeyframe(time, value, curveType));
        }

        public int AddKey(VamKeyframe keyframe)
        {
            // TODO Avoid double browsing
            if (keys.FindIndex(k => k.time == keyframe.time) > -1) return -1;
            var key = keys.FindIndex(k => k.time > keyframe.time);
            if (key == -1)
            {
                keys.Add(keyframe);
                return keys.Count - 1;
            }
            else
            {
                keys.Insert(key, keyframe);
                return key;
            }
        }

        public void RemoveKey(int v)
        {
            keys.RemoveAt(v);
        }

        public void AddEdgeFramesIfMissing(float animationLength)
        {
            if (length == 0)
            {
                AddKey(0, 0);
                AddKey(animationLength, 0);
                return;
            }
            if (length == 1)
            {
                var keyframe = GetKeyframe(0);
                keyframe.time = 0;
                MoveKey(0, keyframe);
                AddKey(animationLength, keyframe.value);
                return;
            }
            {
                var keyframe = GetKeyframe(0);
                if (keyframe.time > 0)
                {
                    if (length > 2)
                    {
                        AddKey(0, keyframe.value);
                    }
                    else
                    {
                        keyframe.time = 0;
                        MoveKey(0, keyframe);
                    }
                }
            }
            {
                var keyframe = GetKeyframe(length - 1);
                if (keyframe.time < animationLength)
                {
                    if (length > 2)
                    {
                        AddKey(animationLength, keyframe.value);
                    }
                    else
                    {
                        keyframe.time = animationLength;
                        MoveKey(length - 1, keyframe);
                    }
                }
            }
        }

        #region From Virt-A-Mate's CubicBezierCurve

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
                    case 2:
                        if (previous != null)
                            current.controlPointIn = current.value - ((current.value - previous.value) / 3f);
                        if (next != null)
                            current.controlPointOut = current.value + ((next.value - current.value) / 3f);
                        // TODO: Implement linear
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

            if (loop) keysCount -= 1;
            var valuesCount = loop ? keysCount + 1 : keysCount - 1;
            if (_computeValues == null || _computeValues.Length < valuesCount + 1)
            {
                _computeValues = new float[valuesCount + 1];
            }
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
            if (a == null || a.Length < valuesCount) a = new float[valuesCount];
            if (b == null || b.Length < valuesCount) b = new float[valuesCount];
            if (c == null || c.Length < valuesCount) c = new float[valuesCount];
            if (r == null || r.Length < valuesCount) r = new float[valuesCount];

            a[0] = 0f;
            b[0] = 2f;
            c[0] = 1f;
            r[0] = _computeValues[0] + 2f * _computeValues[1];
            for (var i = 1; i < valuesCount - 1; i++)
            {
                a[i] = 1f;
                b[i] = 4f;
                c[i] = 1f;
                r[i] = 4f * _computeValues[i] + 2f * _computeValues[i + 1];
            }
            a[valuesCount - 1] = 2f;
            b[valuesCount - 1] = 7f;
            c[valuesCount - 1] = 0f;
            r[valuesCount - 1] = 8f * _computeValues[valuesCount - 1] + _computeValues[valuesCount];
            for (var i = 1; i < valuesCount; i++)
            {
                var n = a[i] / b[i - 1];
                b[i] -= n * c[i - 1];
                r[i] -= n * r[i - 1];
            }
            if (loop)
            {
                var vector = r[valuesCount - 1] / b[valuesCount - 1];
                keys[valuesCount - 2].controlPointOut = (r[valuesCount - 1] - c[valuesCount - 1] * vector) / b[valuesCount - 1];
                for (var i = valuesCount - 3; i >= 0; i--)
                {
                    keys[i].controlPointOut = (r[i + 1] - c[i + 1] * keys[i + 1].controlPointOut) / b[i + 1];
                }
            }
            else
            {
                keys[valuesCount].controlPointOut = keys[valuesCount].value;
                keys[valuesCount - 1].controlPointOut = r[valuesCount - 1] / b[valuesCount - 1];
                for (var i = valuesCount - 2; i >= 0; i--)
                {
                    keys[i].controlPointOut = (r[i] - c[i] * keys[i + 1].controlPointOut) / b[i];
                }
            }
            if (loop)
            {
                for (var i = 0; i < valuesCount - 1; i++)
                {
                    keys[i].controlPointIn = 2f * _computeValues[i + 1] - keys[i].controlPointOut;
                }
                return;
            }
            keys[0].controlPointIn = keys[0].value;
            for (var i = 1; i < valuesCount; i++)
            {
                keys[i].controlPointIn = 2f * _computeValues[i] - keys[i].controlPointOut;
            }
            keys[valuesCount].controlPointIn = 0.5f * (_computeValues[valuesCount] + keys[valuesCount - 1].controlPointOut);
        }

        public float ComputeBezierValue(int key, float t)
        {
            var w0 = keys[key].value;
            var w1 = keys[key].controlPointOut;
            var keysCount = loop ? keys.Count - 1 : keys.Count;
            if (keysCount == 1)
            {
                return w0;
            }
            float w2;
            float w3;
            if (key == keysCount - 1)
            {
                if (!loop)
                {
                    return w0;
                }
                w2 = keys[0].controlPointIn;
                w3 = keys[0].value;
            }
            else
            {
                w2 = keys[key + 1].controlPointIn;
                w3 = keys[key + 1].value;
            }

            // See https://pomax.github.io/bezierinfo/#how-to-implement-the-weighted-basis-function
            float mt = 1f - t;
            float mt2 = mt * mt;
            float mt3 = mt2 * mt;
            float t2 = t * t;
            float t3 = t2 * t;
            return w0 * mt3 + 3f * w1 * mt2 * t + 3f * w2 * mt * t2 + w3 * t3;
        }

        #endregion
    }

    public struct VamKeyframeCompute
    {
    }

    public class VamKeyframe
    {
        public float time;
        public float value;
        public float controlPointIn;
        public float controlPointOut;
        public int curveType;

        public VamKeyframe()
        {
        }

        public VamKeyframe(float time, float value, int curveType)
            : this(time, value, curveType, value, value)
        {

        }

        public VamKeyframe(float time, float value, int curveType, float controlPointIn, float controlPointOut)
        {
            this.time = time;
            this.value = value;
            this.curveType = curveType;
            this.controlPointIn = controlPointIn;
            this.controlPointOut = controlPointOut;
        }

        public VamKeyframe Clone()
        {
            // TODO: Untangle AnimationCurve struct references
            return new VamKeyframe
            {
                time = time,
                value = value,
                curveType = curveType,
                controlPointIn = controlPointIn,
                controlPointOut = controlPointOut
            };
        }
    }
}
