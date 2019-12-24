using System;
using System.Linq;
using SimpleJSON;
using UnityEngine;

namespace AcidBubbles.VamTimeline
{
    /// <summary>
    /// VaM Timeline Controller
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
            var json = JSON.Parse(val);
            var animation = new AtomAnimation(atom);
            animation.AnimationLength = DeserializeFloat(json["AnimationLength"], 1f);
            animation.Speed = DeserializeFloat(json["Speed"], 1f);
            JSONArray controllersJSON = json["Controllers"].AsArray;
            if (controllersJSON == null) throw new NullReferenceException("Saved state does not have controllers");
            foreach (JSONClass controllerJSON in controllersJSON)
            {
                var controllerName = controllerJSON["Controller"].Value;
                var controller = atom.freeControllers.Single(fc => fc.name == controllerName);
                if (atom == null) throw new NullReferenceException($"Atom '{atom.uid}' does not have a controller '{controllerName}'");
                var fcAnimation = animation.Add(controller);
                DeserializeCurve(fcAnimation.X, controllerJSON["X"]);
                DeserializeCurve(fcAnimation.X, controllerJSON["X"]);
                DeserializeCurve(fcAnimation.Y, controllerJSON["Y"]);
                DeserializeCurve(fcAnimation.Z, controllerJSON["Z"]);
                DeserializeCurve(fcAnimation.RotX, controllerJSON["RotX"]);
                DeserializeCurve(fcAnimation.RotY, controllerJSON["RotY"]);
                DeserializeCurve(fcAnimation.RotZ, controllerJSON["RotZ"]);
                DeserializeCurve(fcAnimation.RotW, controllerJSON["RotW"]);
            }
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
            var animationJSON = new JSONClass();
            animationJSON.Add("AnimationLength", animation.AnimationLength.ToString());
            animationJSON.Add("Speed", animation.Speed.ToString());
            var controllersJSON = new JSONArray();
            animationJSON.Add("Controllers", controllersJSON);
            foreach (var controller in animation.Controllers)
            {
                var controllerJSON = new JSONClass();
                controllerJSON.Add("Controller", controller.Controller.name);
                controllerJSON.Add("X", SerializeCurve(controller.X));
                controllerJSON.Add("Y", SerializeCurve(controller.Y));
                controllerJSON.Add("Z", SerializeCurve(controller.Z));
                controllerJSON.Add("RotX", SerializeCurve(controller.RotX));
                controllerJSON.Add("RotY", SerializeCurve(controller.RotY));
                controllerJSON.Add("RotZ", SerializeCurve(controller.RotZ));
                controllerJSON.Add("RotW", SerializeCurve(controller.RotW));
                controllersJSON.Add(controllerJSON);
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
                var keyframeJSON = new JSONClass();
                keyframeJSON.Add("time", keyframe.time.ToString());
                keyframeJSON.Add("value", keyframe.value.ToString());
                keyframeJSON.Add("inTangent", keyframe.inTangent.ToString());
                keyframeJSON.Add("outTangent", keyframe.outTangent.ToString());
                keyframesJSON.Add(keyframeJSON);
            }

            return curveJSON;
        }
    }
}
