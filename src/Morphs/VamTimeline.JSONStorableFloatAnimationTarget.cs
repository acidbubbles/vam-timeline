using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class JSONStorableFloatAnimationTarget : IAnimationTarget
    {
        private float _animationLength;
        private readonly JSONStorableFloat _jsf;
        public AnimationCurve Value = new AnimationCurve();

        public string Name => _jsf.name;

        public JSONStorableFloatAnimationTarget(JSONStorableFloat jsf, float animationLength)
        {
            _jsf = jsf;
            _animationLength = animationLength;
        }

        public void SetLength(float length)
        {
            Value.SetLength(length);
            _animationLength = length;
        }

        public IEnumerable<float> GetAllKeyframesTime()
        {
            return Value.keys.Take(Value.keys.Length - 1).Select(k => k.time);
        }

        public void RenderDebugInfo(StringBuilder display, float time)
        {
            throw new NotImplementedException();
        }
    }
}
