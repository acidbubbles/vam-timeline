using System;
using System.Linq;
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
    public class Serializer
    {
        public Serializer()
        {
        }

        public AtomAnimation DeserializeAnimation(Atom atom, string val)
        {
            if (string.IsNullOrEmpty(val)) return null;

            var animationJSON = JSON.Parse(val);
            var animation = new AtomAnimation(atom)
            {
                BlendDuration = DeserializeFloat(animationJSON["BlendDuration"], 1f)
            };
            JSONArray clipsJSON = animationJSON["Clips"].AsArray;
            if (clipsJSON == null || clipsJSON.Count == 0) throw new NullReferenceException("Saved state does not have clips");
            foreach (JSONClass clipJSON in clipsJSON)
            {
                var clip = new AtomAnimationClip(clipJSON["AnimationName"].Value)
                {
                    Speed = DeserializeFloat(clipJSON["Speed"], 1f),
                    AnimationLength = DeserializeFloat(clipJSON["AnimationLength"], 1f)
                };
                var animationPatternUID = clipJSON["AnimationPattern"]?.Value;
                if (!string.IsNullOrEmpty(animationPatternUID))
                {
                    var animationPattern = SuperController.singleton.GetAtomByUid(animationPatternUID)?.GetComponentInChildren<AnimationPattern>();
                    if (animationPattern == null)
                        SuperController.LogError($"Animation Pattern '{animationPatternUID}' linked to animation '{clip.AnimationName}' of atom '{atom.uid}' was not found in scene");
                    else
                        clip.AnimationPattern = animationPattern;
                }
                JSONArray controllersJSON = clipJSON["Controllers"].AsArray;
                if (controllersJSON == null) throw new NullReferenceException("Saved state does not have controllers");
                foreach (JSONClass controllerJSON in controllersJSON)
                {
                    var controllerName = controllerJSON["Controller"].Value;
                    var controller = atom.freeControllers.Single(fc => fc.name == controllerName);
                    if (atom == null) throw new NullReferenceException($"Atom '{atom.uid}' does not have a controller '{controllerName}'");
                    var fcAnimation = clip.Add(controller);
                    DeserializeCurve(fcAnimation.X, controllerJSON["X"]);
                    DeserializeCurve(fcAnimation.X, controllerJSON["X"]);
                    DeserializeCurve(fcAnimation.Y, controllerJSON["Y"]);
                    DeserializeCurve(fcAnimation.Z, controllerJSON["Z"]);
                    DeserializeCurve(fcAnimation.RotX, controllerJSON["RotX"]);
                    DeserializeCurve(fcAnimation.RotY, controllerJSON["RotY"]);
                    DeserializeCurve(fcAnimation.RotZ, controllerJSON["RotZ"]);
                    DeserializeCurve(fcAnimation.RotW, controllerJSON["RotW"]);
                }
                animation.AddClip(clip);
            }
            animation.Initialize();
            animation.RebuildAnimation();
            return animation;
        }

        private void DeserializeCurve(AnimationCurve curve, JSONNode curveJSON)
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

        private float DeserializeFloat(JSONNode node, float defaultVal = 0)
        {
            if (node == null || string.IsNullOrEmpty(node.Value))
                return defaultVal;
            return float.Parse(node.Value);
        }

        public string SerializeAnimation(AtomAnimation animation)
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
                if (clip.AnimationPattern != null)
                    clipJSON.Add("AnimationPattern", clip.AnimationPattern.containingAtom.uid);
                var controllersJSON = new JSONArray();
                clipJSON.Add("Controllers", controllersJSON);
                foreach (var controller in clip.Controllers)
                {
                    var controllerJSON = new JSONClass
                    {
                        { "Controller", controller.Controller.name },
                        { "X", SerializeCurve(controller.X) },
                        { "Y", SerializeCurve(controller.Y) },
                        { "Z", SerializeCurve(controller.Z) },
                        { "RotX", SerializeCurve(controller.RotX) },
                        { "RotY", SerializeCurve(controller.RotY) },
                        { "RotZ", SerializeCurve(controller.RotZ) },
                        { "RotW", SerializeCurve(controller.RotW) }
                    };
                    controllersJSON.Add(controllerJSON);
                }
                clipsJSON.Add(clipJSON);
            }
            return animationJSON.ToString();
        }

        private JSONNode SerializeCurve(AnimationCurve curve)
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
    }
}
