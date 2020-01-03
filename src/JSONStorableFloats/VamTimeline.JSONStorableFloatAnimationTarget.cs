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
        public readonly JSONStorableFloat Storable;
        public AnimationCurve Value = new AnimationCurve();

        public string Name => Storable.name;

        public JSONStorableFloatAnimationTarget(JSONStorableFloat jsf, float animationLength)
        {
            Storable = jsf;
            _animationLength = animationLength;
        }

        public void SetLength(float length)
        {
            Value.SetLength(length);
            _animationLength = length;
        }

        public void SetKeyframe(float time, float value)
        {
            if (time == 0f)
            {
                SetFlatKeyframe(0, value);
                SetFlatKeyframe(_animationLength, value);
            }
            else
            {
                SetFlatKeyframe(time, value);
            }
        }

        private void SetFlatKeyframe(float time, float value)
        {
            var key = Value.SetKeyframe(time, value);
            var keyframe = Value.keys[key];
            keyframe.inTangent = 0f;
            keyframe.outTangent = 0f;
            Value.MoveKey(key, keyframe);
        }

        public void ReapplyCurvesToClip(AnimationClip clip)
        {
            clip.SetCurve("", typeof(JSONStorableFloat), "val", Value);
        }

        public IEnumerable<float> GetAllKeyframesTime()
        {
            return Value.keys.Take(Value.keys.Length - 1).Select(k => k.time);
        }

        public void RenderDebugInfo(StringBuilder display, float time)
        {
            display.AppendLine($"{Storable.name}");
            foreach (var keyframe in Value.keys)
            {
                display.AppendLine($"  {(keyframe.time == time ? "+" : "-")} {keyframe.time:0.00}s: {keyframe.value:0.00}");
                display.AppendLine($"    Tngt in: {keyframe.inTangent:0.00} out: {keyframe.outTangent:0.00}");
                display.AppendLine($"    Wght in: {keyframe.inWeight:0.00} out: {keyframe.outWeight:0.00} {keyframe.weightedMode}");
            }
        }
    }
}
