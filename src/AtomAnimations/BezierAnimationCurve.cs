using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class BezierAnimationCurve
    {
        public List<VamKeyframe> keys = new List<VamKeyframe>();
        public int length => keys.Count;
        // TODO: Use correctly
        public bool loop;
        public float[] _cachedValues;
        public float[] r;
        public float[] a;
        public float[] b;
        public float[] c;

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
            return GetPositionFromPoint(key - 1, t);
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
        public int SetKeyframe(float time, float value)
        {
            time = time.Snap();
            if (keys.Count == 0) return AddKey(time, value);
            var key = this.KeyframeBinarySearch(time);
            if (key != -1)
                return SetKeyframeByKey(key, value);
            key = AddKey(time, value);
            if (key == -1)
                throw new InvalidOperationException($"Cannot add keyframe at time {time}. Keys: {string.Join(", ", keys.Select(k => k.time.ToString()).ToArray())}.");
            return key;
        }

        public int SetKeyframeByKey(int key, float value)
        {
            var keyframe = GetKeyframe(key);
            keyframe.value = value;
            MoveKey(key, keyframe);
            return key;
        }

        public int AddKey(float time, float value)
        {
            return AddKey(new VamKeyframe(time, value));
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
            var num2 = loop ? keysCount + 1 : keysCount - 1;
            if (_cachedValues == null || _cachedValues.Length < num2 + 1)
            {
                _cachedValues = new float[num2 + 1];
            }
            if (loop)
            {
                _cachedValues[0] = keys[keysCount - 1].value;
                for (var i = 1; i < num2; i++)
                {
                    _cachedValues[i] = keys[i - 1].value;
                }
                _cachedValues[num2] = keys[0].value;
            }
            else
            {
                for (var j = 0; j < keysCount; j++)
                {
                    _cachedValues[j] = keys[j].value;
                }
            }
            if (a == null || a.Length < num2)
            {
                a = new float[num2];
            }
            if (b == null || b.Length < num2)
            {
                b = new float[num2];
            }
            if (c == null || c.Length < num2)
            {
                c = new float[num2];
            }
            if (r == null || r.Length < num2)
            {
                r = new float[num2];
            }
            // TODO: Determine curve types here
            a[0] = 0f;
            b[0] = 2f;
            c[0] = 1f;
            r[0] = _cachedValues[0] + 2f * _cachedValues[1];
            for (var k = 1; k < num2 - 1; k++)
            {
                a[k] = 1f;
                b[k] = 4f;
                c[k] = 1f;
                r[k] = 4f * _cachedValues[k] + 2f * _cachedValues[k + 1];
            }
            a[num2 - 1] = 2f;
            b[num2 - 1] = 7f;
            c[num2 - 1] = 0f;
            r[num2 - 1] = 8f * _cachedValues[num2 - 1] + _cachedValues[num2];
            for (var l = 1; l < num2; l++)
            {
                var num3 = a[l] / b[l - 1];
                b[l] -= num3 * c[l - 1];
                r[l] -= num3 * r[l - 1];
            }
            if (loop)
            {
                var vector = r[num2 - 1] / b[num2 - 1];
                keys[num2 - 2].controlPointOut = (r[num2 - 1] - c[num2 - 1] * vector) / b[num2 - 1];
                for (var num4 = num2 - 3; num4 >= 0; num4--)
                {
                    keys[num4].controlPointOut = (r[num4 + 1] - c[num4 + 1] * keys[num4 + 1].controlPointOut) / b[num4 + 1];
                }
            }
            else
            {
                keys[num2].controlPointOut = keys[num2].value;
                keys[num2 - 1].controlPointOut = r[num2 - 1] / b[num2 - 1];
                for (var num5 = num2 - 2; num5 >= 0; num5--)
                {
                    keys[num5].controlPointOut = (r[num5] - c[num5] * keys[num5 + 1].controlPointOut) / b[num5];
                }
            }
            if (loop)
            {
                for (var m = 0; m < num2 - 1; m++)
                {
                    keys[m].controlPointIn = 2f * _cachedValues[m + 1] - keys[m].controlPointOut;
                }
                return;
            }
            keys[0].controlPointIn = keys[0].value;
            for (var n = 1; n < num2; n++)
            {
                keys[n].controlPointIn = 2f * _cachedValues[n] - keys[n].controlPointOut;
            }
            keys[num2].controlPointIn = 0.5f * (_cachedValues[num2] + keys[num2 - 1].controlPointOut);
        }

        public float GetPositionFromPoint(int fromPoint, float t)
        {
            var position = keys[fromPoint].value;
            var position2 = keys[fromPoint].controlPointOut;
            var keysCount = loop ? keys.Count - 1 : keys.Count;
            if (keysCount == 1)
            {
                return position;
            }
            float position3;
            float position4;
            if (fromPoint == keysCount - 1)
            {
                if (!loop)
                {
                    return position;
                }
                position3 = keys[0].controlPointIn;
                position4 = keys[0].value;
            }
            else
            {
                position3 = keys[fromPoint + 1].controlPointIn;
                position4 = keys[fromPoint + 1].value;
            }
            float num = 1f - t;
            float num2 = num * num;
            float d = num2 * num;
            float num3 = t * t;
            float d2 = num3 * t;
            return position * d + 3f * position2 * num2 * t + 3f * position3 * num * num3 + position4 * d2;
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

        public VamKeyframe()
        {
        }

        public VamKeyframe(float time, float value)
            : this(time, value, value, value)
        {

        }

        public VamKeyframe(float time, float value, float controlPointIn, float controlPointOut)
        {
            this.time = time;
            this.value = value;
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
                controlPointIn = controlPointIn,
                controlPointOut = controlPointOut
            };
        }
    }
}
