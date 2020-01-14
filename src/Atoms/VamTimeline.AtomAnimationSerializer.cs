using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
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

        #region Deserialize JSON

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
                    if (controller == null)
                    {
                        SuperController.LogError($"VamTimeline: Atom '{_atom.uid}' does not have a controller '{controllerName}'");
                        continue;
                    }
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
            var last = -1f;

            if (curveJSON is JSONClass)
            {
                // Legacy
                foreach (JSONNode keyframeJSON in curveJSON["keys"].AsArray)
                {
                    var time = (float)(Math.Round(DeserializeFloat(keyframeJSON["time"]) * 1000f) / 1000f);
                    if (time == last) continue;
                    last = time;
                    var keyframe = new Keyframe
                    {
                        time = time,
                        value = DeserializeFloat(keyframeJSON["value"]),
                        inTangent = DeserializeFloat(keyframeJSON["inTangent"]),
                        outTangent = DeserializeFloat(keyframeJSON["outTangent"])
                    };
                    curve.AddKey(keyframe);
                }
                return;
            }
            else
            {
                foreach (var keyframe in curveJSON.Value.Split(';').Where(x => x != ""))
                {
                    var parts = keyframe.Split(',');
                    try
                    {
                        var time = (float)(Math.Round(float.Parse(parts[0], CultureInfo.InvariantCulture) * 1000f) / 1000f);
                        if (time == last) continue;
                        last = time;
                        curve.AddKey(new Keyframe
                        {
                            time = time,
                            value = DeserializeFloat(parts[1]),
                            // TODO: Load curve type
                            inTangent = DeserializeFloat(parts[3]),
                            outTangent = DeserializeFloat(parts[4])
                        });
                    }
                    catch (IndexOutOfRangeException exc)
                    {
                        throw new InvalidOperationException($"Failed to ready curve: {keyframe}", exc);
                    }
                }
            }
        }

        private float DeserializeFloat(JSONNode node, float defaultVal = 0)
        {
            if (node == null || string.IsNullOrEmpty(node.Value))
                return defaultVal;
            return float.Parse(node.Value, CultureInfo.InvariantCulture);
        }

        private bool DeserializeBool(JSONNode node, bool defaultVal)
        {
            if (node == null || string.IsNullOrEmpty(node.Value))
                return defaultVal;
            if (node.Value == "0") return false;
            if (node.Value == "1") return true;
            return bool.Parse(node.Value);
        }

        #endregion

        #region Serialize JSON

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
                    // TODO: Speed should be an animation setting, not a clip setting
                    { "Speed", clip.Speed.ToString(CultureInfo.InvariantCulture) },
                    { "AnimationLength", clip.AnimationLength.ToString(CultureInfo.InvariantCulture) },
                    { "BlendDuration", clip.BlendDuration.ToString(CultureInfo.InvariantCulture) },
                    { "Loop", clip.Loop ? "1" : "0" },
                    { "EnsureQuaternionContinuity", clip.EnsureQuaternionContinuity ? "1" : "0" }
                };
                if (clip.NextAnimationName != null)
                    clipJSON["NextAnimationName"] = clip.NextAnimationName;
                if (clip.NextAnimationTime != 0)
                    clipJSON["NextAnimationTime"] = clip.NextAnimationTime.ToString(CultureInfo.InvariantCulture);

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

        private JSONNode SerializeCurve(AnimationCurve curve)
        {
            // TODO: Use US locale to avoid commas in floats
            // TODO: Serialize as: time,value,type,inTangent,outTangent;...
            // e.g.: 0,12.345,1,-0.18,0.18;
            var sb = new StringBuilder();

            foreach (var keyframe in curve.keys)
            {
                sb.Append(keyframe.time.ToString(CultureInfo.InvariantCulture));
                sb.Append(',');
                sb.Append(keyframe.value.ToString(CultureInfo.InvariantCulture));
                sb.Append(',');
                sb.Append('0');
                sb.Append(',');
                sb.Append(keyframe.inTangent.ToString(CultureInfo.InvariantCulture));
                sb.Append(',');
                sb.Append(keyframe.outTangent.ToString(CultureInfo.InvariantCulture));
                sb.Append(';');
            }

            return sb.ToString();
        }

        #endregion
    }
}
