using System;
using System.Collections.Generic;
using System.Globalization;
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

        #region Deserialize JSON

        public AtomAnimation DeserializeAnimation(AtomAnimation animation, JSONClass animationJSON)
        {
            if (animation == null)
            {
                animation = new AtomAnimation(_atom)
                {
                    Speed = DeserializeFloat(animationJSON["Speed"], 1f),
                    InterpolationTimeout = DeserializeFloat(animationJSON["InterpolationTimeout"], 0.25f),
                    InterpolationSpeed = DeserializeFloat(animationJSON["InterpolationSpeed"], 1f),
                };
            }
            JSONArray clipsJSON = animationJSON["Clips"].AsArray;
            if (clipsJSON == null || clipsJSON.Count == 0) throw new NullReferenceException("Saved state does not have clips");
            foreach (JSONClass clipJSON in clipsJSON)
            {
                string animationName = clipJSON["AnimationName"].Value;
                if (animation.Clips.Any(c => c.AnimationName == animationName))
                {
                    SuperController.LogError($"VamTimeline: Imported clip '{animationName}' already exists and will be overwritten");
                    var clipToRemove = animation.Clips.First(c => c.AnimationName == animationName);
                    animation.Clips.Remove(clipToRemove);
                    clipToRemove.Dispose();
                }
                var clip = new AtomAnimationClip(animationName)
                {
                    BlendDuration = DeserializeFloat(clipJSON["BlendDuration"], AtomAnimationClip.DefaultBlendDuration),
                    Loop = DeserializeBool(clipJSON["Loop"], true),
                    Transition = DeserializeBool(clipJSON["Transition"], false),
                    EnsureQuaternionContinuity = DeserializeBool(clipJSON["EnsureQuaternionContinuity"], true),
                    NextAnimationName = clipJSON["NextAnimationName"]?.Value,
                    NextAnimationTime = DeserializeFloat(clipJSON["NextAnimationTime"], 0),
                    AutoPlay = DeserializeBool(clipJSON["AutoPlay"], false)
                };
                clip.AnimationLength = DeserializeFloat(clipJSON["AnimationLength"]).Snap();
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
                    clip.Add(target);
                    DeserializeCurve(target.X, controllerJSON["X"], clip.AnimationLength, target.Settings);
                    DeserializeCurve(target.Y, controllerJSON["Y"], clip.AnimationLength);
                    DeserializeCurve(target.Z, controllerJSON["Z"], clip.AnimationLength);
                    DeserializeCurve(target.RotX, controllerJSON["RotX"], clip.AnimationLength);
                    DeserializeCurve(target.RotY, controllerJSON["RotY"], clip.AnimationLength);
                    DeserializeCurve(target.RotZ, controllerJSON["RotZ"], clip.AnimationLength);
                    DeserializeCurve(target.RotW, controllerJSON["RotW"], clip.AnimationLength);
                    AnimationCurve leadCurve = target.GetLeadCurve();
                    for (var key = 0; key < leadCurve.length; key++)
                    {
                        var time = leadCurve[key].time;
                        var ms = time.ToMilliseconds();
                        if (!target.Settings.ContainsKey(ms))
                            target.Settings.Add(ms, new KeyframeSettings { CurveType = CurveTypeValues.LeaveAsIs });
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
                    clip.Add(target);
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

        private void DeserializeCurve(AnimationCurve curve, JSONNode curveJSON, float length, SortedDictionary<int, KeyframeSettings> keyframeSettings = null)
        {
            if (curveJSON is JSONArray)
                DeserializeCurveFromArray(curve, (JSONArray)curveJSON, keyframeSettings);
            if (curveJSON is JSONClass)
                DeserializeCurveFromClassLegacy(curve, curveJSON);
            else
                DeserializeCurveFromStringLegacy(curve, curveJSON, keyframeSettings);

            if (curve.length < 2)
            {
                // Attempt repair
                var keyframe = curve.length > 0 ? curve[0] : new Keyframe { value = 0 };
                if (curve.length > 0)
                    curve.RemoveKey(0);
                keyframe.time = 0f;
                curve.AddKey(keyframe);
                keyframe.time = length;
                curve.AddKey(keyframe);
                if (keyframeSettings != null)
                {
                    keyframeSettings.Clear();
                    keyframeSettings.Add(0, new KeyframeSettings { CurveType = CurveTypeValues.Smooth });
                    keyframeSettings.Add(length.ToMilliseconds(), new KeyframeSettings { CurveType = CurveTypeValues.Smooth });
                }
            }
        }

        private void DeserializeCurveFromArray(AnimationCurve curve, JSONArray curveJSON, SortedDictionary<int, KeyframeSettings> keyframeSettings = null)
        {
            if (curveJSON.Count == 0) return;

            var last = -1f;
            foreach (JSONClass keyframeJSON in curveJSON)
            {
                try
                {
                    var time = float.Parse(keyframeJSON["t"], CultureInfo.InvariantCulture).Snap();
                    if (time == last) continue;
                    last = time;
                    var value = DeserializeFloat(keyframeJSON["v"]);
                    curve.AddKey(new Keyframe
                    {
                        time = time,
                        value = value,
                        inTangent = DeserializeFloat(keyframeJSON["ti"]),
                        outTangent = DeserializeFloat(keyframeJSON["to"])
                    });
                    if (keyframeSettings != null)
                        keyframeSettings.Add(time.ToMilliseconds(), new KeyframeSettings { CurveType = CurveTypeValues.FromInt(int.Parse(keyframeJSON["c"])) });
                }
                catch (IndexOutOfRangeException exc)
                {
                    throw new InvalidOperationException($"Failed to read curve: {keyframeJSON}", exc);
                }
            }
        }

        private void DeserializeCurveFromStringLegacy(AnimationCurve curve, JSONNode curveJSON, SortedDictionary<int, KeyframeSettings> keyframeSettings = null)
        {
            var strFrames = curveJSON.Value.Split(';').Where(x => x != "").ToList();
            if (strFrames.Count == 0) return;

            var last = -1f;
            foreach (var keyframe in strFrames)
            {
                var parts = keyframe.Split(',');
                try
                {
                    var time = float.Parse(parts[0], CultureInfo.InvariantCulture).Snap();
                    if (time == last) continue;
                    last = time;
                    var value = DeserializeFloat(parts[1]);
                    curve.AddKey(new Keyframe
                    {
                        time = time,
                        value = value,
                        inTangent = DeserializeFloat(parts[3]),
                        outTangent = DeserializeFloat(parts[4])
                    });
                    if (keyframeSettings != null)
                        keyframeSettings.Add(time.ToMilliseconds(), new KeyframeSettings { CurveType = CurveTypeValues.FromInt(int.Parse(parts[2])) });
                }
                catch (IndexOutOfRangeException exc)
                {
                    throw new InvalidOperationException($"Failed to read curve: {keyframe}", exc);
                }
            }
        }

        private void DeserializeCurveFromClassLegacy(AnimationCurve curve, JSONNode curveJSON)
        {
            var keysJSON = curveJSON["keys"].AsArray;
            if (keysJSON.Count == 0) return;

            var last = -1f;
            foreach (JSONNode keyframeJSON in keysJSON)
            {
                var time = DeserializeFloat(keyframeJSON["time"]).Snap();
                if (time == last) continue;
                last = time;
                var value = DeserializeFloat(keyframeJSON["value"]);
                var keyframe = new Keyframe
                {
                    time = time,
                    value = value,
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

        public JSONClass SerializeAnimation(AtomAnimation animation, string animationNameFilter = null)
        {
            var animationJSON = new JSONClass
            {
                { "Speed", animation.Speed.ToString(CultureInfo.InvariantCulture) },
                { "InterpolationTimeout", animation.InterpolationTimeout.ToString(CultureInfo.InvariantCulture) },
                { "InterpolationSpeed", animation.InterpolationSpeed.ToString(CultureInfo.InvariantCulture) }
            };
            var clipsJSON = new JSONArray();
            animationJSON.Add("Clips", clipsJSON);
            foreach (var clip in animation.Clips.Where(c => animationNameFilter == null || c.AnimationName == animationNameFilter))
            {
                var clipJSON = new JSONClass
                {
                    { "AnimationName", clip.AnimationName },
                    { "AnimationLength", clip.AnimationLength.ToString(CultureInfo.InvariantCulture) },
                    { "BlendDuration", clip.BlendDuration.ToString(CultureInfo.InvariantCulture) },
                    { "Loop", clip.Loop ? "1" : "0" },
                    { "Transition", clip.Transition ? "1" : "0" },
                    { "EnsureQuaternionContinuity", clip.EnsureQuaternionContinuity ? "1" : "0" }
                };
                if (clip.NextAnimationName != null)
                    clipJSON["NextAnimationName"] = clip.NextAnimationName;
                if (clip.NextAnimationTime != 0)
                    clipJSON["NextAnimationTime"] = clip.NextAnimationTime.ToString(CultureInfo.InvariantCulture);
                if (clip.AutoPlay)
                    clipJSON["AutoPlay"] = "1";

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

        private JSONNode SerializeCurve(AnimationCurve curve, SortedDictionary<int, KeyframeSettings> settings = null)
        {
            var curveJSON = new JSONArray();

            for (var key = 0; key < curve.length; key++)
            {
                var keyframe = curve[key];
                var ms = keyframe.time.ToMilliseconds();
                var curveEntry = new JSONClass
                {
                    ["t"] = keyframe.time.ToString(CultureInfo.InvariantCulture),
                    ["v"] = keyframe.value.ToString(CultureInfo.InvariantCulture),
                    ["c"] = settings == null ? "0" : (settings.ContainsKey(ms) ? CurveTypeValues.ToInt(settings[ms].CurveType).ToString() : "0"),
                    ["ti"] = keyframe.inTangent.ToString(CultureInfo.InvariantCulture),
                    ["to"] = keyframe.outTangent.ToString(CultureInfo.InvariantCulture)
                };
                curveJSON.Add(curveEntry);
            }

            return curveJSON;
        }

        #endregion

        #region Static serializers

        public static JSONClass SerializeQuaternion(Quaternion localRotation)
        {
            var jc = new JSONClass();
            jc["x"].AsFloat = localRotation.x;
            jc["y"].AsFloat = localRotation.y;
            jc["z"].AsFloat = localRotation.z;
            jc["w"].AsFloat = localRotation.w;
            return jc;
        }

        public static JSONClass SerializeVector3(Vector3 localPosition)
        {
            var jc = new JSONClass();
            jc["x"].AsFloat = localPosition.x;
            jc["y"].AsFloat = localPosition.y;
            jc["z"].AsFloat = localPosition.z;
            return jc;
        }

        public static Quaternion DeserializeQuaternion(JSONClass jc)
        {
            return new Quaternion
            (
                jc["x"].AsFloat,
                jc["y"].AsFloat,
                jc["z"].AsFloat,
                jc["w"].AsFloat
            );
        }

        public static Vector3 DeserializeVector3(JSONClass jc)
        {
            return new Vector3
            (
                jc["x"].AsFloat,
                jc["y"].AsFloat,
                jc["z"].AsFloat
            );
        }

        #endregion
    }
}
