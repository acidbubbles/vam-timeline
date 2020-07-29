using System;
using UnityEngine;

namespace VamTimeline
{
    public class VamAnimationCurve
    {
        public VamKeyframe[] keys;
        public int length => keys.Length;

        public VamKeyframe this[int key] => keys[key];

        public float Evaluate(float time)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
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
