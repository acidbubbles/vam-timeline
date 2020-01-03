using System;
using SimpleJSON;
using UnityEngine;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public interface IAnimationSerializer<TAnimation, TAnimationClip>
        where TAnimationClip : class, IAnimationClip
        where TAnimation : class, IAnimation<TAnimationClip>
    {
        TAnimation CreateDefaultAnimation();
        TAnimation DeserializeAnimation(string val);
        string SerializeAnimation(TAnimation animation);
    }

    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public abstract class AnimationSerializerBase<TAnimation, TAnimationClip> : IAnimationSerializer<TAnimation, TAnimationClip>
        where TAnimationClip : class, IAnimationClip
        where TAnimation : class, IAnimation<TAnimationClip>
    {
        protected AnimationSerializerBase()
        {
        }

        public TAnimation DeserializeAnimation(string val)
        {
            if (string.IsNullOrEmpty(val)) return null;

            var animationJSON = JSON.Parse(val);
            var animation = CreateDefaultAnimation();
            animation.BlendDuration = DeserializeFloat(animationJSON["BlendDuration"], 1f);
            JSONArray clipsJSON = animationJSON["Clips"].AsArray;
            if (clipsJSON == null || clipsJSON.Count == 0) throw new NullReferenceException("Saved state does not have clips");
            foreach (JSONClass clipJSON in clipsJSON)
            {
                var clip = CreateDefaultAnimationClip(clipJSON["AnimationName"].Value);
                clip.Speed = DeserializeFloat(clipJSON["Speed"], 1f);
                clip.AnimationLength = DeserializeFloat(clipJSON["AnimationLength"], 1f);
                DeserializeClip(clip, clipJSON);
                animation.AddClip(clip);
            }
            animation.Initialize();
            animation.RebuildAnimation();
            return animation;
        }

        protected void DeserializeCurve(AnimationCurve curve, JSONNode curveJSON)
        {
            foreach (JSONNode keyframeJSON in curveJSON["keys"].AsArray)
            {
                var keyframe = new Keyframe
                {
                    time = DeserializeFloat(keyframeJSON["time"]),
                    value = DeserializeFloat(keyframeJSON["value"]),
                    inTangent = DeserializeFloat(keyframeJSON["inTangent"]),
                    outTangent = DeserializeFloat(keyframeJSON["outTangent"])
                };
                curve.AddKey(keyframe);
            }
        }

        protected float DeserializeFloat(JSONNode node, float defaultVal = 0)
        {
            if (node == null || string.IsNullOrEmpty(node.Value))
                return defaultVal;
            return float.Parse(node.Value);
        }

        public string SerializeAnimation(TAnimation animation)
        {
            var animationJSON = new JSONClass
            {
                { "BlendDuration", animation.BlendDuration.ToString() }
            };
            var clipsJSON = new JSONArray();
            animationJSON.Add("Clips", clipsJSON);
            foreach (var clip in animation.Clips)
            {
                var clipJSON = new JSONClass
                {
                    { "AnimationName", clip.AnimationName },
                    { "Speed", clip.Speed.ToString() },
                    { "AnimationLength", clip.AnimationLength.ToString() }
                };
                SerializeClip(clip, clipJSON);
                clipsJSON.Add(clipJSON);
            }
            return animationJSON.ToString();
        }

        protected JSONNode SerializeCurve(AnimationCurve curve)
        {
            var curveJSON = new JSONClass();
            var keyframesJSON = new JSONArray();
            curveJSON.Add("keys", keyframesJSON);

            foreach (var keyframe in curve.keys)
            {
                var keyframeJSON = new JSONClass
                {
                    { "time", keyframe.time.ToString() },
                    { "value", keyframe.value.ToString() },
                    { "inTangent", keyframe.inTangent.ToString() },
                    { "outTangent", keyframe.outTangent.ToString() }
                };
                keyframesJSON.Add(keyframeJSON);
            }

            return curveJSON;
        }

        public abstract TAnimation CreateDefaultAnimation();
        protected abstract TAnimationClip CreateDefaultAnimationClip(string animationName);
        protected abstract void DeserializeClip(TAnimationClip clip, JSONClass clipJSON);
        protected abstract void SerializeClip(TAnimationClip clip, JSONClass clipJSON);
    }
}
