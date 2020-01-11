using System;
using System.Collections.Generic;
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
    public class AtomAnimationSerializer
    {
        private readonly Atom _atom;

        public AtomAnimationSerializer(Atom atom)
        {
            _atom = atom;
        }

        #region Deserialize

        public AtomAnimation DeserializeAnimation(string val)
        {
            if (string.IsNullOrEmpty(val)) return null;

            var animationJSON = JSON.Parse(val);
            var animation = new AtomAnimation(_atom);
            // Legacy
            var defaultBlendDuration = DeserializeFloat(animationJSON["BlendDuration"], AtomAnimationClip.DefaultBlendDuration);
            JSONArray clipsJSON = animationJSON["Clips"].AsArray;
            if (clipsJSON == null || clipsJSON.Count == 0) throw new NullReferenceException("Saved state does not have clips");
            foreach (JSONClass clipJSON in clipsJSON)
            {
                var clip = new AtomAnimationClip(clipJSON["AnimationName"].Value)
                {
                    Speed = DeserializeFloat(clipJSON["Speed"], 1f),
                    BlendDuration = DeserializeFloat(clipJSON["BlendDuration"], defaultBlendDuration),
                    Loop = DeserializeBool(clipJSON["Loop"], true),
                    EnsureQuaternionContinuity = DeserializeBool(clipJSON["EnsureQuaternionContinuity"], true),
                    NextAnimationName = clipJSON["NextAnimationName"]?.Value,
                    NextAnimationTime = DeserializeFloat(clipJSON["NextAnimationTime"], 0)
                };
                clip.CropOrExtendLength(DeserializeFloat(clipJSON["AnimationLength"], AtomAnimationClip.DefaultAnimationLength));
                DeserializeClip(clip, clipJSON);
                animation.AddClip(clip);
            }
            animation.Initialize();
            animation.RebuildAnimation();
            return animation;
        }

        private void DeserializeClip(AtomAnimationClip clip, JSONClass clipJSON)
        {
            var animationPatternUID = clipJSON["AnimationPattern"]?.Value;
            if (!string.IsNullOrEmpty(animationPatternUID))
            {
                var animationPattern = SuperController.singleton.GetAtomByUid(animationPatternUID)?.GetComponentInChildren<AnimationPattern>();
                if (animationPattern == null)
                    SuperController.LogError($"Animation Pattern '{animationPatternUID}' linked to animation '{clip.AnimationName}' of atom '{_atom.uid}' was not found in scene");
                else
                    clip.AnimationPattern = animationPattern;
            }

            JSONArray controllersJSON = clipJSON["Controllers"].AsArray;
            if (controllersJSON != null)
            {
                foreach (JSONClass controllerJSON in controllersJSON)
                {
                    var controllerName = controllerJSON["Controller"].Value;
                    var controller = _atom.freeControllers.Single(fc => fc.name == controllerName);
                    if (controller == null) throw new NullReferenceException($"Atom '{_atom.uid}' does not have a controller '{controllerName}'");
                    var target = new FreeControllerAnimationTarget(controller, clip.AnimationLength);
                    clip.TargetControllers.Add(target);
                    DeserializeCurve(target.X, controllerJSON["X"]);
                    DeserializeCurve(target.X, controllerJSON["X"]);
                    DeserializeCurve(target.Y, controllerJSON["Y"]);
                    DeserializeCurve(target.Z, controllerJSON["Z"]);
                    DeserializeCurve(target.RotX, controllerJSON["RotX"]);
                    DeserializeCurve(target.RotY, controllerJSON["RotY"]);
                    DeserializeCurve(target.RotZ, controllerJSON["RotZ"]);
                    DeserializeCurve(target.RotW, controllerJSON["RotW"]);
                }
            }

            JSONArray paramsJSON = clipJSON["FloatParams"].AsArray;
            var morphs = GetMorphs();
            if (paramsJSON != null)
            {
                foreach (JSONClass paramJSON in paramsJSON)
                {
                    var storableId = paramJSON["Storable"].Value;
                    var floatParamName = paramJSON["Name"].Value;
                    if (storableId == "geometry")
                    {
                        // This allows loading an animation even though the animatable option was checked off (e.g. loading a pose)
                        var morph = morphs.FirstOrDefault(m => m.jsonFloat.name == floatParamName);
                        if (morph == null)
                        {
                            SuperController.LogError($"VamTimeline: Atom '{_atom.uid}' does not have a morph (geometry) '{floatParamName}'");
                            continue;
                        }
                        if (!morph.animatable)
                            morph.animatable = true;
                    }
                    var storable = _atom.containingAtom.GetStorableByID(storableId);
                    if (storable == null)
                    {
                        SuperController.LogError($"VamTimeline: Atom '{_atom.uid}' does not have a storable '{storableId}'");
                        continue;
                    }
                    var jsf = storable.GetFloatJSONParam(floatParamName);
                    if (jsf == null)
                    {
                        SuperController.LogError($"VamTimeline: Atom '{_atom.uid}' does not have a param '{storableId}/{floatParamName}'");
                        continue;
                    }
                    var target = new FloatParamAnimationTarget(storable, jsf);
                    clip.TargetFloatParams.Add(target);
                    DeserializeCurve(target.Value, paramJSON["Value"]);
                }
            }
        }

        private IEnumerable<DAZMorph> GetMorphs()
        {
            var geometry = _atom.GetStorableByID("geometry");
            if (geometry == null) yield break;
            var character = geometry as DAZCharacterSelector;
            if (character == null) yield break;
            var morphControl = character.morphsControlUI;
            if (morphControl == null) yield break;
            foreach (var morphDisplayName in morphControl.GetMorphDisplayNames())
            {
                var morph = morphControl.GetMorphByDisplayName(morphDisplayName);
                if (morph == null) continue;
                yield return morph;
            }
        }

        private void DeserializeCurve(AnimationCurve curve, JSONNode curveJSON)
        {
            foreach (JSONNode keyframeJSON in curveJSON["keys"].AsArray)
            {
                var keyframe = new Keyframe
                {
                    time = (float)(Math.Round(DeserializeFloat(keyframeJSON["time"]) * 1000f) / 1000f),
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

        protected bool DeserializeBool(JSONNode node, bool defaultVal)
        {
            if (node == null || string.IsNullOrEmpty(node.Value))
                return defaultVal;
            return bool.Parse(node.Value);
        }

        #endregion

        #region Serialize

        public string SerializeAnimation(AtomAnimation animation)
        {
            var animationJSON = new JSONClass();
            var clipsJSON = new JSONArray();
            animationJSON.Add("Clips", clipsJSON);
            foreach (var clip in animation.Clips)
            {
                var clipJSON = new JSONClass
                {
                    { "AnimationName", clip.AnimationName },
                    { "Speed", clip.Speed.ToString() },
                    { "AnimationLength", clip.AnimationLength.ToString() },
                    { "BlendDuration", clip.BlendDuration.ToString() },
                    { "Loop", clip.Loop.ToString() },
                    { "EnsureQuaternionContinuity", clip.EnsureQuaternionContinuity.ToString() }
                };
                if (clip.NextAnimationName != null)
                    clipJSON["NextAnimationName"] = clip.NextAnimationName;
                if (clip.NextAnimationTime != 0)
                    clipJSON["NextAnimationTime"] = clip.NextAnimationTime.ToString();

                SerializeClip(clip, clipJSON);
                clipsJSON.Add(clipJSON);
            }
            return animationJSON.ToString();
        }

        private void SerializeClip(AtomAnimationClip clip, JSONClass clipJSON)
        {
            if (clip.AnimationPattern != null)
                clipJSON.Add("AnimationPattern", clip.AnimationPattern.containingAtom.uid);

            var controllersJSON = new JSONArray();
            clipJSON.Add("Controllers", controllersJSON);
            foreach (var controller in clip.TargetControllers)
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

            var paramsJSON = new JSONArray();
            clipJSON.Add("FloatParams", paramsJSON);
            foreach (var target in clip.TargetFloatParams)
            {
                var paramJSON = new JSONClass
                    {
                        { "Storable", target.Storable.name },
                        { "Name", target.FloatParam.name },
                        { "Value", SerializeCurve(target.Value) },
                    };
                paramsJSON.Add(paramJSON);
            }
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

        #endregion
    }
}
