using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SimpleJSON;
using UnityEngine;

namespace VamTimeline
{
    public class AtomAnimationSerializer
    {
        private readonly Atom _atom;

        public AtomAnimationSerializer(Atom atom)
        {
            _atom = atom;
        }

        #region Deserialize JSON

        public void DeserializeAnimation(AtomAnimation animation, JSONClass animationJSON)
        {
            if (animation == null) throw new ArgumentNullException(nameof(animation));

            animation.speed = DeserializeFloat(animationJSON["Speed"], 1f);

            var clipsJSON = animationJSON["Clips"].AsArray;
            if (clipsJSON == null || clipsJSON.Count == 0) throw new NullReferenceException("Saved state does not have clips");
            foreach (JSONClass clipJSON in clipsJSON)
            {
                var clip = DeserializeClip(clipJSON);
                animation.AddClip(clip);
            }
            animation.Initialize();
            animation.RebuildAnimationNow();
        }

        public AtomAnimationClip DeserializeClip(JSONClass clipJSON)
        {
            var animationName = clipJSON["AnimationName"].Value;
            var animationLayer = DeserializeString(clipJSON["AnimationLayer"], AtomAnimationClip.DefaultAnimationLayer);
            var clip = new AtomAnimationClip(animationName, animationLayer)
            {
                blendDuration = DeserializeFloat(clipJSON["BlendDuration"], AtomAnimationClip.DefaultBlendDuration),
                loop = DeserializeBool(clipJSON["Loop"], true),
                transition = DeserializeBool(clipJSON["Transition"], false),
                ensureQuaternionContinuity = DeserializeBool(clipJSON["EnsureQuaternionContinuity"], true),
                nextAnimationName = clipJSON["NextAnimationName"]?.Value,
                nextAnimationTime = DeserializeFloat(clipJSON["NextAnimationTime"], 0),
                autoPlay = DeserializeBool(clipJSON["AutoPlay"], false),
                speed = DeserializeFloat(clipJSON["Speed"], 1),
                weight = DeserializeFloat(clipJSON["Weight"], 1),
            };
            clip.animationLength = DeserializeFloat(clipJSON["AnimationLength"]).Snap();
            DeserializeClip(clip, clipJSON);
            return clip;
        }

        private void DeserializeClip(AtomAnimationClip clip, JSONClass clipJSON)
        {
            var animationPatternUID = clipJSON["AnimationPattern"]?.Value;
            if (!string.IsNullOrEmpty(animationPatternUID))
            {
                var animationPattern = SuperController.singleton.GetAtomByUid(animationPatternUID)?.GetComponentInChildren<AnimationPattern>();
                if (animationPattern == null)
                    SuperController.LogError($"Animation Pattern '{animationPatternUID}' linked to animation '{clip.animationName}' of atom '{_atom.uid}' was not found in scene");
                else
                    clip.animationPattern = animationPattern;
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
                        SuperController.LogError($"Timeline: Atom '{_atom.uid}' does not have a controller '{controllerName}'");
                        continue;
                    }
                    var target = new FreeControllerAnimationTarget(controller);
                    clip.Add(target);
                    DeserializeCurve(target.x, controllerJSON["X"], clip.animationLength, target.settings);
                    DeserializeCurve(target.y, controllerJSON["Y"], clip.animationLength);
                    DeserializeCurve(target.z, controllerJSON["Z"], clip.animationLength);
                    DeserializeCurve(target.rotX, controllerJSON["RotX"], clip.animationLength);
                    DeserializeCurve(target.rotY, controllerJSON["RotY"], clip.animationLength);
                    DeserializeCurve(target.rotZ, controllerJSON["RotZ"], clip.animationLength);
                    DeserializeCurve(target.rotW, controllerJSON["RotW"], clip.animationLength);
                    AddMissingKeyframeSettings(target);
                    target.AddEdgeFramesIfMissing(clip.animationLength);
                }
            }

            JSONArray floatParamsJSON = clipJSON["FloatParams"].AsArray;
            if (floatParamsJSON != null)
            {
                foreach (JSONClass paramJSON in floatParamsJSON)
                {
                    var storableId = paramJSON["Storable"].Value;
                    var floatParamName = paramJSON["Name"].Value;
                    var target = new FloatParamAnimationTarget(_atom, storableId, floatParamName);
                    clip.Add(target);
                    DeserializeCurve(target.value, paramJSON["Value"], clip.animationLength, target.settings);
                    AddMissingKeyframeSettings(target);
                    target.AddEdgeFramesIfMissing(clip.animationLength);
                }
            }

            JSONArray triggersJSON = clipJSON["Triggers"].AsArray;
            if (triggersJSON != null)
            {
                foreach (JSONClass triggerJSON in triggersJSON)
                {
                    var target = new TriggersAnimationTarget
                    {
                        name = DeserializeString(triggerJSON["Name"], "Trigger")
                    };
                    foreach (JSONClass entryJSON in triggerJSON["Triggers"].AsArray)
                    {
                        var trigger = new AtomAnimationTrigger();
                        trigger.RestoreFromJSON(entryJSON);
                        target.SetKeyframe(trigger.startTime, trigger);
                    }
                    target.AddEdgeFramesIfMissing(clip.animationLength);
                    clip.Add(target);
                }
            }
        }

        private static void AddMissingKeyframeSettings(ICurveAnimationTarget target)
        {
            VamAnimationCurve leadCurve = target.GetLeadCurve();
            for (var key = 0; key < leadCurve.length; key++)
            {
                var time = leadCurve.GetKeyframe(key).time;
                target.EnsureKeyframeSettings(time, CurveTypeValues.LeaveAsIs);
            }
        }

        private void DeserializeCurve(VamAnimationCurve curve, JSONNode curveJSON, float length, SortedDictionary<int, KeyframeSettings> keyframeSettings = null)
        {
            if (curveJSON is JSONArray)
                DeserializeCurveFromArray(curve, (JSONArray)curveJSON, keyframeSettings);
            if (curveJSON is JSONClass)
                DeserializeCurveFromClassLegacy(curve, curveJSON);
            else
                DeserializeCurveFromStringLegacy(curve, curveJSON, keyframeSettings);

            if (curve.length < 2)
            {
                SuperController.LogError("Repair");
                // Attempt repair
                var keyframe = curve.length > 0 ? curve.GetKeyframe(0) : new VamKeyframe(0, 0);
                if (curve.length > 0)
                    curve.RemoveKey(0);
                keyframe.time = 0f;
                curve.AddKey(keyframe);
                keyframe.time = length;
                curve.AddKey(keyframe);
                if (keyframeSettings != null)
                {
                    keyframeSettings.Clear();
                    keyframeSettings.Add(0, new KeyframeSettings { curveType = CurveTypeValues.Smooth });
                    keyframeSettings.Add(length.ToMilliseconds(), new KeyframeSettings { curveType = CurveTypeValues.Smooth });
                }
            }
        }

        private void DeserializeCurveFromArray(VamAnimationCurve curve, JSONArray curveJSON, SortedDictionary<int, KeyframeSettings> keyframeSettings = null)
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
                    curve.AddKey(new VamKeyframe
                    {
                        time = time,
                        value = value,
                        inTangent = DeserializeFloat(keyframeJSON["ti"]),
                        outTangent = DeserializeFloat(keyframeJSON["to"])
                    });
                    if (keyframeSettings != null)
                        keyframeSettings.Add(time.ToMilliseconds(), new KeyframeSettings { curveType = CurveTypeValues.FromInt(int.Parse(keyframeJSON["c"])) });
                }
                catch (IndexOutOfRangeException exc)
                {
                    throw new InvalidOperationException($"Failed to read curve: {keyframeJSON}", exc);
                }
            }
        }

        private void DeserializeCurveFromStringLegacy(VamAnimationCurve curve, JSONNode curveJSON, SortedDictionary<int, KeyframeSettings> keyframeSettings = null)
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
                    curve.AddKey(new VamKeyframe
                    {
                        time = time,
                        value = value,
                        inTangent = DeserializeFloat(parts[3]),
                        outTangent = DeserializeFloat(parts[4])
                    });
                    if (keyframeSettings != null)
                        keyframeSettings.Add(time.ToMilliseconds(), new KeyframeSettings { curveType = CurveTypeValues.FromInt(int.Parse(parts[2])) });
                }
                catch (IndexOutOfRangeException exc)
                {
                    throw new InvalidOperationException($"Failed to read curve: {keyframe}", exc);
                }
            }
        }

        private void DeserializeCurveFromClassLegacy(VamAnimationCurve curve, JSONNode curveJSON)
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
                var keyframe = new VamKeyframe
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

        private string DeserializeString(JSONNode node, string defaultVal)
        {
            if (node == null || string.IsNullOrEmpty(node.Value))
                return defaultVal;
            return node.Value;
        }

        #endregion

        #region Serialize JSON

        public JSONClass SerializeAnimation(AtomAnimation animation, string animationNameFilter = null)
        {
            var animationJSON = new JSONClass
            {
                { "Speed", animation.speed.ToString(CultureInfo.InvariantCulture) }
            };
            var clipsJSON = new JSONArray();
            foreach (var clip in animation.clips.Where(c => animationNameFilter == null || c.animationName == animationNameFilter))
            {
                clipsJSON.Add(SerializeClip(clip));
            }
            animationJSON.Add("Clips", clipsJSON);
            return animationJSON;
        }

        public JSONClass SerializeClip(AtomAnimationClip clip)
        {
            var clipJSON = new JSONClass
                {
                    { "AnimationName", clip.animationName },
                    { "AnimationLength", clip.animationLength.ToString(CultureInfo.InvariantCulture) },
                    { "BlendDuration", clip.blendDuration.ToString(CultureInfo.InvariantCulture) },
                    { "Loop", clip.loop ? "1" : "0" },
                    { "Transition", clip.transition ? "1" : "0" },
                    { "EnsureQuaternionContinuity", clip.ensureQuaternionContinuity ? "1" : "0" },
                    { "AnimationLayer", clip.animationLayer },
                    { "Speed", clip.speed.ToString(CultureInfo.InvariantCulture) },
                    { "Weight", clip.weight.ToString(CultureInfo.InvariantCulture) },
                };
            if (clip.nextAnimationName != null)
                clipJSON["NextAnimationName"] = clip.nextAnimationName;
            if (clip.nextAnimationTime != 0)
                clipJSON["NextAnimationTime"] = clip.nextAnimationTime.ToString(CultureInfo.InvariantCulture);
            if (clip.autoPlay)
                clipJSON["AutoPlay"] = "1";

            SerializeClip(clip, clipJSON);
            return clipJSON;
        }

        private void SerializeClip(AtomAnimationClip clip, JSONClass clipJSON)
        {
            if (clip.animationPattern != null)
                clipJSON.Add("AnimationPattern", clip.animationPattern.containingAtom.uid);

            var controllersJSON = new JSONArray();
            clipJSON.Add("Controllers", controllersJSON);
            foreach (var controller in clip.targetControllers)
            {
                var controllerJSON = new JSONClass
                    {
                        { "Controller", controller.controller.name },
                        { "X", SerializeCurve(controller.x, controller.settings) },
                        { "Y", SerializeCurve(controller.y, controller.settings) },
                        { "Z", SerializeCurve(controller.z, controller.settings) },
                        { "RotX", SerializeCurve(controller.rotX, controller.settings) },
                        { "RotY", SerializeCurve(controller.rotY, controller.settings) },
                        { "RotZ", SerializeCurve(controller.rotZ, controller.settings) },
                        { "RotW", SerializeCurve(controller.rotW, controller.settings) }
                    };
                controllersJSON.Add(controllerJSON);
            }

            var paramsJSON = new JSONArray();
            clipJSON.Add("FloatParams", paramsJSON);
            foreach (var target in clip.targetFloatParams)
            {
                var paramJSON = new JSONClass
                    {
                        { "Storable", target.storableId },
                        { "Name", target.floatParamName },
                        { "Value", SerializeCurve(target.value, target.settings) },
                    };
                paramsJSON.Add(paramJSON);
            }

            var triggersJSON = new JSONArray();
            clipJSON.Add("", triggersJSON);
            clipJSON.Add("Triggers", triggersJSON);
            foreach (var target in clip.targetTriggers)
            {
                var triggerJSON = new JSONClass()
                {
                    {"Name", target.name}
                };
                var entriesJSON = new JSONArray();
                foreach (var x in target.triggersMap.OrderBy(kvp => kvp.Key))
                {
                    entriesJSON.Add(x.Value.GetJSON());
                }
                triggerJSON["Triggers"] = entriesJSON;
                triggersJSON.Add(triggerJSON);
            }
        }

        private JSONNode SerializeCurve(VamAnimationCurve curve, SortedDictionary<int, KeyframeSettings> settings = null)
        {
            var curveJSON = new JSONArray();

            for (var key = 0; key < curve.length; key++)
            {
                var keyframe = curve.GetKeyframe(key);
                var ms = keyframe.time.ToMilliseconds();
                var curveEntry = new JSONClass
                {
                    ["t"] = keyframe.time.ToString(CultureInfo.InvariantCulture),
                    ["v"] = keyframe.value.ToString(CultureInfo.InvariantCulture),
                    ["c"] = settings == null ? "0" : (settings.ContainsKey(ms) ? CurveTypeValues.ToInt(settings[ms].curveType).ToString() : "0"),
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
