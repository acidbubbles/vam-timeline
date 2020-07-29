using System;
using System.Collections.Generic;
using UnityEngine;

namespace VamTimeline
{
    public class VamAnimationCurve
    {
        public List<VamKeyframe> keys = new List<VamKeyframe>();
        public int length => keys.Count;

        public VamKeyframe this[int key] => keys[key];

        public float Evaluate(float time)
        {
            if (keys.Count < 2) throw new NotSupportedException("Must contain at least two keyframes");
            // TODO: Support looping
            // TODO: Remember the largest time and use it everywhere
            // TODO: Remember the last checked time and bisect from there?
            time = Mathf.Clamp(time, 0, keys[keys.Count - 1].time);
            // TODO: Bisect or better, no linear search here!
            var key = keys.FindIndex(k => k.time > time);
            if (key == -1) key = 0;
            var from = keys[key];
            var to = keys[key + 1];
            // TODO: Worth precalculating?
            var t = (time - from.time) / (to.time - from.time);
            return Mathf.Lerp(from.value, to.value, t);
        }

        public void MoveKey(int v, VamKeyframe keyframe)
        {
            keys[v] = keyframe;
        }

        public int AddKey(float time, float value)
        {
            return AddKey(new VamKeyframe(time, value));
        }

        public int AddKey(VamKeyframe keyframe)
        {

            var key = keys.FindIndex(k => k.time > keyframe.time);
            if (key == -1)
            {
                keys.Add(keyframe);
                return keys.Count - 1;
            }
            else
            {
                keys.Insert(key - 1, keyframe);
                return key;
            }
        }

        public void RemoveKey(int v)
        {
            throw new NotImplementedException();
        }
    }

    public struct VamKeyframe
    {
        public float time;
        public float value;
        public float inTangent;
        public float outTangent;
        public float inWeight;
        public float outWeight;

        public VamKeyframe(float time, float value)
            : this(time, value, 0, 0)
        {

        }

        public VamKeyframe(float time, float value, int inTangent, int outTangent)
            : this(time, value, inTangent, outTangent, 0.33333f, 0.33333f)
        {

        }

        public VamKeyframe(float time, float value, int inTangent, int outTangent, float inWeight, float outWeight)
        {
            this.time = time;
            this.value = value;
            this.inTangent = inTangent;
            this.outTangent = outTangent;
            this.inWeight = inWeight;
            this.outWeight = outWeight;
        }
    }
}
