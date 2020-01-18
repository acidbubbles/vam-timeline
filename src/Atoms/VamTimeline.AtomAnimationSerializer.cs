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

        public AtomAnimation DeserializeAnimation(JSONClass animationJSON)
        {
            var animation = new AtomAnimation(_atom)
            {
                Speed = DeserializeFloat(animationJSON["Speed"], 1f)
            };
            // Legacy
            var defaultBlendDuration = DeserializeFloat(animationJSON["BlendDuration"], AtomAnimationClip.DefaultBlendDuration);
            JSONArray clipsJSON = animationJSON["Clips"].AsArray;
            if (clipsJSON == null || clipsJSON.Count == 0) throw new NullReferenceException("Saved state does not have clips");
            foreach (JSONClass clipJSON in clipsJSON)
            {
                var clip = new AtomAnimationClip(clipJSON["AnimationName"].Value)
                {
                    BlendDuration = DeserializeFloat(clipJSON["BlendDuration"], defaultBlendDuration),
                    Loop = DeserializeBool(clipJSON["Loop"], true),
                    EnsureQuaternionContinuity = DeserializeBool(clipJSON["EnsureQuaternionContinuity"], true),
                    NextAnimationName = clipJSON["NextAnimationName"]?.Value,
                    NextAnimationTime = DeserializeFloat(clipJSON["NextAnimationTime"], 0)
                };
                clip.AnimationLength = DeserializeFloat(clipJSON["AnimationLength"]);
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
                    var target = new FreeControllerAnimationTarget(controller);
                    clip.TargetControllers.Add(target);
                    DeserializeCurve(target.X, controllerJSON["X"], clip.AnimationLength, target.Settings);
                    DeserializeCurve(target.X, controllerJSON["X"], clip.AnimationLength);
                    DeserializeCurve(target.Y, controllerJSON["Y"], clip.AnimationLength);
                    DeserializeCurve(target.Z, controllerJSON["Z"], clip.AnimationLength);
                    DeserializeCurve(target.RotX, controllerJSON["RotX"], clip.AnimationLength);
                    DeserializeCurve(target.RotY, controllerJSON["RotY"], clip.AnimationLength);
                    DeserializeCurve(target.RotZ, controllerJSON["RotZ"], clip.AnimationLength);
                    DeserializeCurve(target.RotW, controllerJSON["RotW"], clip.AnimationLength);
                    foreach (var time in target.X.keys.Select(k => k.time))
                    {
                        if (!target.Settings.ContainsKey(time))
                            target.Settings.Add(time, new KeyframeSettings { CurveType = CurveTypeValues.LeaveAsIs });
                    }
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
                    DeserializeCurve(target.Value, paramJSON["Value"], clip.AnimationLength);
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

        private void DeserializeCurve(AnimationCurve curve, JSONNode curveJSON, float length, SortedDictionary<float, KeyframeSettings> keyframeSettings = null)
        {
            if (curveJSON is JSONClass)
                DeserializeCurveLegacy(curve, curveJSON);
            else
                DeserializeCurveFromString(curve, curveJSON, keyframeSettings);

            if (curve.keys.Length < 2)
            {
                // Attempt repair
                var keyframe = curve.keys.Length > 0 ? curve.keys[0] : new Keyframe { value = 0 };
                curve.RemoveKey(0);
                keyframe.time = 0f;
                curve.AddKey(keyframe);
                keyframe.time = length;
                curve.AddKey(keyframe);
                if (keyframeSettings != null)
                {
                    keyframeSettings.Clear();
                    keyframeSettings.Add(0, new KeyframeSettings { CurveType = CurveTypeValues.Smooth });
                    keyframeSettings.Add(length, new KeyframeSettings { CurveType = CurveTypeValues.Smooth });
                }
            }
        }

        private void DeserializeCurveFromString(AnimationCurve curve, JSONNode curveJSON, SortedDictionary<float, KeyframeSettings> keyframeSettings = null)
        {
            var last = -1f;
            foreach (var keyframe in curveJSON.Value.Split(';').Where(x => x != ""))
            {
                var parts = keyframe.Split(',');
                try
                {
                    var time = float.Parse(parts[0], CultureInfo.InvariantCulture).Snap();
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
                    if (keyframeSettings != null)
                        keyframeSettings.Add(time, new KeyframeSettings { CurveType = CurveTypeValues.FromInt(int.Parse(parts[2])) });
                }
                catch (IndexOutOfRangeException exc)
                {
                    throw new InvalidOperationException($"Failed to read curve: {keyframe}", exc);
                }
            }
        }

        private void DeserializeCurveLegacy(AnimationCurve curve, JSONNode curveJSON)
        {
            var last = -1f;
            foreach (JSONNode keyframeJSON in curveJSON["keys"].AsArray)
            {
                var time = DeserializeFloat(keyframeJSON["time"]).Snap();
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

        public JSONClass SerializeAnimation(AtomAnimation animation)
        {
            var animationJSON = new JSONClass
            {
                { "Speed", animation.Speed.ToString(CultureInfo.InvariantCulture) }
            };
            var clipsJSON = new JSONArray();
            animationJSON.Add("Clips", clipsJSON);
            foreach (var clip in animation.Clips)
            {
                var clipJSON = new JSONClass
                {
                    { "AnimationName", clip.AnimationName },
                    // TODO: Speed should be an animation setting, not a clip setting
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
            return animationJSON;
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
                        { "X", SerializeCurve(controller.X, controller.Settings) },
                        { "Y", SerializeCurve(controller.Y, controller.Settings) },
                        { "Z", SerializeCurve(controller.Z, controller.Settings) },
                        { "RotX", SerializeCurve(controller.RotX, controller.Settings) },
                        { "RotY", SerializeCurve(controller.RotY, controller.Settings) },
                        { "RotZ", SerializeCurve(controller.RotZ, controller.Settings) },
                        { "RotW", SerializeCurve(controller.RotW, controller.Settings) }
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

        private JSONNode SerializeCurve(AnimationCurve curve, SortedDictionary<float, KeyframeSettings> settings = null)
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
                sb.Append(settings == null ? "0" : (settings.ContainsKey(keyframe.time) ? CurveTypeValues.ToInt(settings[keyframe.time].CurveType).ToString() : "0"));
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
