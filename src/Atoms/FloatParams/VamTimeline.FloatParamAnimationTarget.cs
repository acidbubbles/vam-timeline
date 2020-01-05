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
    public class FloatParamAnimationTarget : IAnimationTarget
    {
        private float _animationLength;
        public readonly JSONStorable Storable;
        public readonly JSONStorableFloat FloatParam;
        public AnimationCurve Value = new AnimationCurve();

        public string Name => Storable != null ? $"{Storable.name}/{FloatParam.name}" : FloatParam.name;

        public FloatParamAnimationTarget(JSONStorable storable, JSONStorableFloat jsf, float animationLength)
        {
            Storable = storable;
            FloatParam = jsf;
            _animationLength = animationLength;
        }

        public void SetLength(float length)
        {
            Value.SetLength(length);
            _animationLength = length;
        }

        public IEnumerable<AnimationCurve> GetCurves()
        {
            return new[] { Value };
        }

        public void SetKeyframe(float time, float value)
        {
            if (time == 0f)
            {
                Value.SetKeyframe(0, value);
                Value.SetKeyframe(_animationLength, value);
            }
            else
            {
                Value.SetKeyframe(time, value);
            }

            Value.FlatAllFrames();
        }

        public IEnumerable<float> GetAllKeyframesTime()
        {
            return Value.keys.Take(Value.keys.Length - 1).Select(k => k.time);
        }

        public void RenderDebugInfo(StringBuilder display, float time)
        {
            display.AppendLine($"{FloatParam.name}");
            foreach (var keyframe in Value.keys)
            {
                display.AppendLine($"  {(keyframe.time == time ? "+" : "-")} {keyframe.time:0.00}s: {keyframe.value:0.00}");
                display.AppendLine($"    Tngt in: {keyframe.inTangent:0.00} out: {keyframe.outTangent:0.00}");
                display.AppendLine($"    Wght in: {keyframe.inWeight:0.00} out: {keyframe.outWeight:0.00} {keyframe.weightedMode}");
            }
        }
    }
}
