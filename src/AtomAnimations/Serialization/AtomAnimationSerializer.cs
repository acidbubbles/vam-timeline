using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using SimpleJSON;
using UnityEngine;

namespace VamTimeline
{
    public class AtomAnimationSerializer
    {
        public static class Modes
        {
            public const int Full = 0;
            public const int Readable = 1;
            public const int Optimized = 2;
        }

        public const int SerializeVersion = 230;

        private readonly Atom _atom;

        public AtomAnimationSerializer(Atom atom)
        {
            _atom = atom;
        }

        #region Deserialize JSON

        public void DeserializeAnimationEditContext(AtomAnimationEditContext animationEditContext, JSONClass animationEditContextJSON)
        {
            if (animationEditContext == null) throw new ArgumentNullException(nameof(animationEditContext));

            animationEditContext.autoKeyframeAllControllers = DeserializeBool(animationEditContextJSON["AutoKeyframeAllControllers"], false);
            animationEditContext.snap = DeserializeFloat(animationEditContextJSON["Snap"], 0.1f);
            animationEditContext.locked = DeserializeBool(animationEditContextJSON["Locked"], false);
            animationEditContext.showPaths = DeserializeBool(animationEditContextJSON["ShowPaths"], false);
        }

        public void DeserializeAnimation(AtomAnimation animation, JSONClass animationJSON)
        {
            if (animation == null) throw new ArgumentNullException(nameof(animation));

            var version = animationJSON.HasKey("SerializeVersion") ? animationJSON["SerializeVersion"].AsInt : 0;
            animation.serializeMode = animationJSON.HasKey("SerializeMode") ? animationJSON["SerializeMode"].AsInt : Modes.Full;
            animation.globalSpeed = DeserializeFloat(animationJSON["Speed"], 1f);
            animation.globalWeight = DeserializeFloat(animationJSON["Weight"], 1f);
            animation.master = DeserializeBool(animationJSON["Master"], false);
            animation.syncWithPeers = DeserializeBool(animationJSON["SyncWithPeers"], true);
            animation.syncSubsceneOnly = DeserializeBool(animationJSON["SyncSubsceneOnly"], false);
            animation.timeMode = DeserializeInt(animationJSON["TimeMode"], TimeModes.UnityTime);
            animation.liveParenting = DeserializeBool(animationJSON["LiveParenting"], false);
            animation.forceBlendTime = DeserializeBool(animationJSON["ForceBlendTime"], false);
            animation.pauseSequencing = DeserializeBool(animationJSON["PauseSequencing"], false);
            if (animationJSON.HasKey("FadeManager"))
                animation.fadeManager = DeserializeFadeManager(animationJSON["FadeManager"].AsObject);
            if (animationJSON.HasKey("GlobalTriggers"))
            {
                var globalTriggersJSON = animationJSON["GlobalTriggers"].AsObject;
                if (globalTriggersJSON.HasKey("OnClipsChanged"))
                    animation.clipListChangedTrigger.trigger.RestoreFromJSON(globalTriggersJSON["OnClipsChanged"].AsObject);
                if (globalTriggersJSON.HasKey("IsPlayingChanged"))
                    animation.isPlayingChangedTrigger.trigger.RestoreFromJSON(globalTriggersJSON["IsPlayingChanged"].AsObject);
            }

            animation.index.StartBulkUpdates();
            try
            {
                var clipsJSON = animationJSON["Clips"].AsArray;
                if (clipsJSON == null || clipsJSON.Count == 0) throw new NullReferenceException("Saved state does not have clips");
                foreach (JSONClass clipJSON in clipsJSON)
                {
                    var clip = DeserializeClip(clipJSON, animation.animatables, animation.logger, version);
                    animation.AddClip(clip);
                }
            }
            finally
            {
                animation.index.EndBulkUpdates();
            }
        }

        private static IFadeManager DeserializeFadeManager(JSONClass jc)
        {
            var fadeManager = VamOverlaysFadeManager.FromJSON(jc);
            if (fadeManager == null) return null;
            SuperController.singleton.StartCoroutine(TryConnectCo(fadeManager));
            return fadeManager;
        }

        private static IEnumerator TryConnectCo(IFadeManager fadeManager)
        {
            if (fadeManager.TryConnectNow()) yield break;
            yield return 0;
            fadeManager.TryConnectNow();
        }

        public AtomAnimationClip DeserializeClip(JSONClass clipJSON, AnimatablesRegistry targetsRegistry, Logger logger, int version)
        {
            var animationName = clipJSON["AnimationName"].Value;
            var animationLayer = DeserializeString(clipJSON["AnimationLayer"], AtomAnimationClip.DefaultAnimationLayer);
            var animationSegment = DeserializeString(clipJSON["AnimationSegment"], AtomAnimationClip.NoneAnimationSegment);
            bool? legacyTransition = null;
            if (clipJSON.HasKey("Transition"))
            {
                legacyTransition = DeserializeBool(clipJSON["Transition"], false);
            }

            var clip = new AtomAnimationClip(animationName, animationLayer, animationSegment, logger)
            {
                blendInDuration = DeserializeFloat(clipJSON["BlendDuration"], AtomAnimationClip.DefaultBlendDuration),
                loop = DeserializeBool(clipJSON["Loop"], true),
                autoTransitionPrevious = legacyTransition ?? DeserializeBool(clipJSON["AutoTransitionPrevious"], false),
                autoTransitionNext = legacyTransition ?? DeserializeBool(clipJSON["AutoTransitionNext"], false),
                preserveLoops = DeserializeBool(clipJSON["SyncTransitionTime"], false),
                preserveLength = DeserializeBool(clipJSON["SyncTransitionTimeNL"], false),
                ensureQuaternionContinuity = DeserializeBool(clipJSON["EnsureQuaternionContinuity"], true),
                nextAnimationName = clipJSON["NextAnimationName"]?.Value,
                nextAnimationTime = DeserializeFloat(clipJSON["NextAnimationTime"]),
                nextAnimationRandomizeWeight = DeserializeFloat(clipJSON["NextAnimationRandomizeWeight"], 1),
                nextAnimationTimeRandomize = DeserializeFloat(clipJSON["NextAnimationTimeRandomize"]),
                autoPlay = DeserializeBool(clipJSON["AutoPlay"], false),
                timeOffset = DeserializeFloat(clipJSON["TimeOffset"]),
                speed = DeserializeFloat(clipJSON["Speed"], 1),
                weight = DeserializeFloat(clipJSON["Weight"], 1),
                uninterruptible = DeserializeBool(clipJSON["Uninterruptible"], false),
                animationLength = DeserializeFloat(clipJSON["AnimationLength"]).Snap(),
                pose = clipJSON.HasKey("Pose") ? AtomPose.FromJSON(_atom, clipJSON["Pose"]) : null,
                applyPoseOnTransition = DeserializeBool(clipJSON["ApplyPoseOnTransition"], false),
                fadeOnTransition = DeserializeBool(clipJSON["FadeOnTransition"], false),
                animationSet = DeserializeString(clipJSON["AnimationSet"], null),
                nextAnimationPreventGroupExit = DeserializeBool(clipJSON["NextAnimationPreventGroupExit"], false),
            };
            if (clip.nextAnimationName != null && clip.nextAnimationRandomizeWeight == 0)
                clip.nextAnimationRandomizeWeight = 1f;
            DeserializeClip(clip, clipJSON, targetsRegistry, version);
            return clip;
        }

        private void DeserializeClip(AtomAnimationClip clip, JSONClass clipJSON, AnimatablesRegistry targetsRegistry, int version)
        {
            var animationPatternUid = clipJSON["AnimationPattern"]?.Value;
            if (!string.IsNullOrEmpty(animationPatternUid))
            {
                var animationPattern = SuperController.singleton.GetAtomByUid(animationPatternUid)?.GetComponentInChildren<AnimationPattern>();
                if (animationPattern == null)
                    SuperController.LogError($"Animation Pattern '{animationPatternUid}' linked to animation '{clip.animationName}' of atom '{_atom.uid}' was not found in scene");
                else
                    clip.animationPattern = animationPattern;
            }

            var audioSourceControlUid = clipJSON["AudioSourceControl"]?.Value;
            if (!string.IsNullOrEmpty(audioSourceControlUid))
            {
                var audioSourceControlAtom = SuperController.singleton.GetAtomByUid(audioSourceControlUid);
                var audioSourceControl = audioSourceControlAtom.GetStorableIDs().Select(audioSourceControlAtom.GetStorableByID).OfType<AudioSourceControl>().FirstOrDefault();
                if (audioSourceControl == null)
                    SuperController.LogError($"AudioSource '{audioSourceControlUid}' linked to animation '{clip.animationName}' of atom '{_atom.uid}' was not found in scene");
                else
                    clip.audioSourceControl = audioSourceControl;
            }

            var controllersJSON = clipJSON["Controllers"].AsArray;
            if (controllersJSON != null)
            {
                foreach (JSONClass controllerJSON in controllersJSON)
                {
                    var controllerAtomUid = controllerJSON["Atom"].Value;
                    var controllerName = controllerJSON["Controller"].Value;
                    Atom atom;
                    if (string.IsNullOrEmpty(controllerAtomUid))
                    {
                        atom = _atom;
                    }
                    else
                    {
                        atom = SuperController.singleton.GetAtomByUid(controllerAtomUid);
                        if (atom == null)
                        {
                            SuperController.LogError($"Timeline: Cannot import controller '{controllerName}' from atom '{controllerAtomUid}' because this atom doesn't exist.");
                            continue;
                        }
                    }

                    var controller = atom.freeControllers.FirstOrDefault(fc => fc.name == controllerName);
                    if (controller == null)
                    {
                        SuperController.LogError($"Timeline: Cannot import controller '{controllerName}' from atom '{controllerAtomUid}' because this controller doesn't exist.");
                        continue;
                    }

                    var controllerRef = targetsRegistry.GetOrCreateController(controller, atom == _atom);
                    if (controllerRef == null)
                    {
                        SuperController.LogError($"The controller {atom.uid} / {controller.name} could not be added in the registry from clip {clip.animationNameQualified}.");
                        continue;
                    }

                    var target = clip.Add(controllerRef, DeserializeBool(controllerJSON["TargetsPosition"], true), DeserializeBool(controllerJSON["TargetsRotation"], true));
                    if (target == null)
                    {
                        SuperController.LogError(
                            $"The controller {atom.uid} / {controller.name} exists more than once in clip {clip.animationNameQualified}. Only the first will be kept.");
                        continue;
                    }

                    target.controlPosition = DeserializeBool(controllerJSON["ControlPosition"], true);
                    target.controlRotation = DeserializeBool(controllerJSON["ControlRotation"], true);
                    target.weight = DeserializeFloat(controllerJSON["Weight"], 1f);
                    target.group = DeserializeString(controllerJSON["Group"], null);
                    if (controllerJSON.HasKey("Parent"))
                    {
                        var parentJSON = controllerJSON["Parent"].AsObject;
                        target.SetParent(parentJSON["Atom"], parentJSON["Rigidbody"]);
                    }

                    var dirty = false;
                    DeserializeCurve(target.position.x, controllerJSON["X"], version, ref dirty);
                    DeserializeCurve(target.position.y, controllerJSON["Y"], version, ref dirty);
                    DeserializeCurve(target.position.z, controllerJSON["Z"], version, ref dirty);
                    DeserializeCurve(target.rotation.rotX, controllerJSON["RotX"], version, ref dirty);
                    DeserializeCurve(target.rotation.rotY, controllerJSON["RotY"], version, ref dirty);
                    DeserializeCurve(target.rotation.rotZ, controllerJSON["RotZ"], version, ref dirty);
                    DeserializeCurve(target.rotation.rotW, controllerJSON["RotW"], version, ref dirty);
                    target.AddEdgeFramesIfMissing(clip.animationLength);
                    if (dirty) target.dirty = true;
                }
            }

            var floatParamsJSON = clipJSON["FloatParams"].AsArray;
            if (floatParamsJSON != null)
            {
                foreach (JSONClass paramJSON in floatParamsJSON)
                {
                    var atomUid = paramJSON["Atom"].Value;
                    var storableId = paramJSON["Storable"].Value;
                    var floatParamName = paramJSON["Name"].Value;
                    Atom atom;
                    if (string.IsNullOrEmpty(atomUid))
                    {
                        atom = _atom;
                    }
                    else
                    {
                        atom = SuperController.singleton.GetAtomByUid(atomUid);
                        if (atom == null)
                        {
                            SuperController.LogError(
                                $"Timeline: Cannot import storable float param '{storableId}' / '{floatParamName}' from atom '{atomUid}' because this atom doesn't exist.");
                            continue;
                        }
                    }

                    var floatParamRef = targetsRegistry.GetOrCreateStorableFloat(
                        atom,
                        storableId,
                        floatParamName,
                        atom == _atom,
                        paramJSON.HasKey("Min") ? (float?)paramJSON["Min"].AsFloat : null,
                        paramJSON.HasKey("Max") ? (float?)paramJSON["Max"].AsFloat : null
                    );
                    var target = clip.Add(floatParamRef);
                    if (target == null)
                    {
                        SuperController.LogError(
                            $"Timeline: Float param {atom.name} / {storableId} / {floatParamName} was added more than once in clip {clip.animationNameQualified}. Dropping second instance.");
                        continue;
                    }

                    target.group = DeserializeString(paramJSON["Group"], null);
                    var dirty = false;
                    DeserializeCurve(target.value, paramJSON["Value"], version, ref dirty);
                    target.AddEdgeFramesIfMissing(clip.animationLength);
                    if (dirty) target.dirty = true;
                }
            }

            var triggersJSON = clipJSON["Triggers"].AsArray;
            if (triggersJSON != null)
            {
                foreach (JSONClass triggerJSON in triggersJSON)
                {
                    var triggerTrackName = DeserializeString(triggerJSON["Name"], "Triggers");
                    var triggerLive = DeserializeBool(triggerJSON["Live"], false);
                    var triggerTrackRef = targetsRegistry.GetOrCreateTriggerTrack(clip.animationLayerQualifiedId, triggerTrackName);
                    //NOTE: We are cheating here, the saved setting is on each track but the animatable itself will have the setting
                    triggerTrackRef.live = triggerLive;
                    var target = clip.Add(triggerTrackRef);
                    if (target == null)
                    {
                        target = clip.targetTriggers.FirstOrDefault(t => t.name == triggerTrackName);
                        if (target == null)
                        {
                            SuperController.LogError(
                                $"The triggers track {triggerTrackName} exists more than once in clip {clip.animationNameQualified}, but couldn't be linked in the clip. Only the first track will be kept.");
                            continue;
                        }
                        else
                        {
                            SuperController.LogError(
                                $"The triggers track {triggerTrackName} exists more than once in clip {clip.animationNameQualified}. Trigger keyframes may be overwritten.");
                        }
                    }

                    target.group = DeserializeString(triggerJSON["Group"], null);
                    foreach (JSONClass entryJSON in triggerJSON["Triggers"].AsArray)
                    {
                        var trigger = target.CreateCustomTrigger();
                        trigger.RestoreFromJSON(entryJSON);
                        trigger.pendingJSON = entryJSON;
                        target.SetKeyframe(trigger.startTime, trigger);
                    }

                    target.AddEdgeFramesIfMissing(clip.animationLength);
                }
            }
        }

        private void DeserializeCurve(BezierAnimationCurve curve, JSONNode curveJSON, int version, ref bool dirty)
        {
            if (curveJSON is JSONArray)
            {
                DeserializeCurveFromArray(curve, (JSONArray)curveJSON, version, ref dirty);
            }

            if (curveJSON is JSONClass)
            {
                DeserializeCurveFromClassLegacy(curve, curveJSON);
                dirty = true;
            }
            else
            {
                DeserializeCurveFromStringLegacy(curve, curveJSON);
                dirty = true;
            }
        }

        private static void DeserializeCurveFromArray(BezierAnimationCurve curve, JSONArray curveJSON, int version, ref bool dirty)
        {
            if (curveJSON.Count == 0) return;

            var lastT = -1f;
            var lastV = 0f;
            var lastC = CurveTypeValues.SmoothLocal;
            foreach (JSONNode keyframeJSON in curveJSON)
            {
                try
                {
                    BezierKeyframe keyframe;
                    if (version >= 230 && !string.IsNullOrEmpty(keyframeJSON.Value))
                    {
                        // Compressed time and value
                        var value = keyframeJSON.Value;
                        keyframe = DecodeKeyframe(value, lastV, lastC);
                    }
                    else if(keyframeJSON is JSONClass)
                    {
                        var keyframeObject = (JSONClass)keyframeJSON;
                        // Separate time and value
                        keyframe = new BezierKeyframe
                        {
                            time = float.Parse(keyframeJSON["t"], CultureInfo.InvariantCulture).Snap(),
                            value = DeserializeFloat(keyframeJSON["v"], lastV),
                            curveType = keyframeObject.HasKey("c") ? int.Parse(keyframeJSON["c"]) : lastC,
                            controlPointIn = DeserializeFloat(keyframeJSON["i"]),
                            controlPointOut = DeserializeFloat(keyframeJSON["o"])
                        };
                        if (version < 230)
                        {
                            // Backward compatibility, tangents are not supported since bezier conversion.
                            if (keyframeObject.HasKey("ti"))
                            {
                                dirty = true;
                                if (keyframe.curveType == CurveTypeValues.LeaveAsIs)
                                    keyframe.curveType = CurveTypeValues.SmoothLocal;
                            }
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException($"Invalid keyframe type {keyframeJSON.GetType()} with version {version}\n{keyframeJSON}");
                    }

                    if (Math.Abs(keyframe.time - lastT) <= float.Epsilon) continue;
                    lastT = keyframe.time;
                    lastV = keyframe.value;
                    lastC = keyframe.curveType;

                    if (lastC != CurveTypeValues.LeaveAsIs)
                        dirty = true;

                    curve.AddKey(keyframe);
                }
                catch (IndexOutOfRangeException exc)
                {
                    throw new InvalidOperationException($"Failed to read curve: {keyframeJSON}", exc);
                }
            }
        }

        private static void DeserializeCurveFromStringLegacy(BezierAnimationCurve curve, JSONNode curveJSON)
        {
            var strFrames = curveJSON.Value.Split(';').Where(x => x != "").ToList();
            if (strFrames.Count == 0) return;

            var last = -1f;
            foreach (var strFrame in strFrames)
            {
                var parts = strFrame.Split(',');
                try
                {
                    var time = float.Parse(parts[0], CultureInfo.InvariantCulture).Snap();
                    if (time == last) continue;
                    last = time;
                    var value = DeserializeFloat(parts[1]);
                    var keyframe = new BezierKeyframe
                    {
                        time = time,
                        value = value,
                        curveType = int.Parse(parts[2])
                    };
                    // Backward compatibility, tangents are not supported since bezier conversion.
                    if (keyframe.curveType == CurveTypeValues.LeaveAsIs)
                        keyframe.curveType = CurveTypeValues.SmoothLocal;
                    curve.AddKey(keyframe);
                }
                catch (IndexOutOfRangeException exc)
                {
                    throw new InvalidOperationException($"Failed to read curve: {strFrame}", exc);
                }
            }
        }

        private static void DeserializeCurveFromClassLegacy(BezierAnimationCurve curve, JSONNode curveJSON)
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
                var keyframe = new BezierKeyframe(
                    time,
                    value,
                    CurveTypeValues.SmoothLocal
                );
                curve.AddKey(keyframe);
            }
        }

        private static float DeserializeFloat(JSONNode node, float defaultVal = 0)
        {
            if (node == null || string.IsNullOrEmpty(node.Value))
                return defaultVal;
            return float.Parse(node.Value, CultureInfo.InvariantCulture);
        }

        private static int DeserializeInt(JSONNode node, int defaultVal = 0)
        {
            if (node == null || string.IsNullOrEmpty(node.Value))
                return defaultVal;
            return int.Parse(node.Value, CultureInfo.InvariantCulture);
        }

        private static bool DeserializeBool(JSONNode node, bool defaultVal)
        {
            if (node == null || string.IsNullOrEmpty(node.Value))
                return defaultVal;
            switch (node.Value)
            {
                case "0":
                    return false;
                case "1":
                    return true;
                default:
                    return bool.Parse(node.Value);
            }
        }

        private static string DeserializeString(JSONNode node, string defaultVal)
        {
            if (node == null || string.IsNullOrEmpty(node.Value))
                return defaultVal;
            return node.Value;
        }

        #endregion

        #region Serialize JSON

        public static JSONClass SerializeEditContext(AtomAnimationEditContext animationEditContext)
        {
            var animationEditContextJSON = new JSONClass
            {
                { "AutoKeyframeAllControllers", animationEditContext.autoKeyframeAllControllers ? "1" : "0" },
                { "Snap", animationEditContext.snap.ToString(CultureInfo.InvariantCulture) },
                { "Locked", animationEditContext.locked ? "1" : "0" },
                { "ShowPaths", animationEditContext.showPaths ? "1" : "0" },
            };
            return animationEditContextJSON;
        }

        public JSONClass SerializeAnimation(AtomAnimation animation)
        {
            var animationJSON = new JSONClass
            {
                { "SerializeVersion", SerializeVersion.ToString() },
                { "SerializeMode", animation.serializeMode.ToString() },
                { "Speed", animation.globalSpeed.ToString(CultureInfo.InvariantCulture) },
                { "Weight", animation.globalWeight.ToString(CultureInfo.InvariantCulture) },
                { "Master", animation.master ? "1" : "0" },
                { "SyncWithPeers", animation.syncWithPeers ? "1" : "0" },
                { "SyncSubsceneOnly", animation.syncSubsceneOnly ? "1" : "0" },
                { "TimeMode", animation.timeMode.ToString(CultureInfo.InvariantCulture) },
                { "LiveParenting", animation.liveParenting ? "1" : "0" },
                { "ForceBlendTime", animation.forceBlendTime ? "1" : "0" },
                { "PauseSequencing", animation.pauseSequencing ? "1" : "0" },
                {
                    "GlobalTriggers", new JSONClass
                    {
                        ["OnClipsChanged"] = animation.clipListChangedTrigger.trigger.GetJSON(),
                        ["IsPlayingChanged"] = animation.isPlayingChangedTrigger.trigger.GetJSON()
                    }
                }
            };
            if (animation.fadeManager != null)
                animationJSON["FadeManager"] = animation.fadeManager.GetJSON();

            var clipsJSON = new JSONArray();
            foreach (var clip in animation.clips)
            {
                clipsJSON.Add(SerializeClip(clip, animation.serializeMode));
            }

            animationJSON.Add("Clips", clipsJSON);
            return animationJSON;
        }

        public JSONClass SerializeClip(AtomAnimationClip clip, int serializeMode)
        {
            var clipJSON = new JSONClass
            {
                { "AnimationName", clip.animationName },
                { "AnimationLength", clip.animationLength.ToString(CultureInfo.InvariantCulture) },
                { "BlendDuration", clip.blendInDuration.ToString(CultureInfo.InvariantCulture) },
                { "Loop", clip.loop ? "1" : "0" },
                { "NextAnimationRandomizeWeight", clip.nextAnimationRandomizeWeight.ToString(CultureInfo.InvariantCulture) },
                { "AutoTransitionPrevious", clip.autoTransitionPrevious ? "1" : "0" },
                { "AutoTransitionNext", clip.autoTransitionNext ? "1" : "0" },
                { "SyncTransitionTime", clip.preserveLoops ? "1" : "0" },
                { "SyncTransitionTimeNL", clip.preserveLength ? "1" : "0" },
                { "EnsureQuaternionContinuity", clip.ensureQuaternionContinuity ? "1" : "0" },
                { "AnimationLayer", clip.animationLayer },
                { "Speed", clip.speed.ToString(CultureInfo.InvariantCulture) },
                { "Weight", clip.weight.ToString(CultureInfo.InvariantCulture) },
                { "Uninterruptible", clip.uninterruptible ? "1" : "0" },
            };
            if (!clip.isOnNoneSegment)
                clipJSON["AnimationSegment"] = clip.animationSegment;
            if (clip.nextAnimationName != null)
                clipJSON["NextAnimationName"] = clip.nextAnimationName;
            if (clip.nextAnimationTime != 0)
                clipJSON["NextAnimationTime"] = clip.nextAnimationTime.ToString(CultureInfo.InvariantCulture);
            if (clip.nextAnimationTimeRandomize != 0)
                clipJSON["NextAnimationTimeRandomize"] = clip.nextAnimationTimeRandomize.ToString(CultureInfo.InvariantCulture);
            if (clip.autoPlay)
                clipJSON["AutoPlay"] = "1";
            if (clip.timeOffset != 0)
                clipJSON["TimeOffset"] = clip.timeOffset.ToString(CultureInfo.InvariantCulture);
            if (clip.pose != null)
                clipJSON["Pose"] = clip.pose.ToJSON();
            if (clip.applyPoseOnTransition)
                clipJSON["ApplyPoseOnTransition"] = "1";
            if (clip.fadeOnTransition)
                clipJSON["FadeOnTransition"] = "1";
            if (clip.animationSet != null)
                clipJSON["AnimationSet"] = clip.animationSet;
            if (clip.animationPattern != null)
                clipJSON.Add("AnimationPattern", clip.animationPattern.containingAtom.uid);
            if (clip.audioSourceControl != null)
                clipJSON.Add("AudioSourceControl", clip.audioSourceControl.containingAtom.uid);
            if (clip.animationNameGroupId > -1 && clip.nextAnimationPreventGroupExit)
                clipJSON.Add("NextAnimationPreventGroupExit", "1");

            SerializeClipTargets(clip, clipJSON, serializeMode);
            return clipJSON;
        }

        private static void SerializeClipTargets(AtomAnimationClip clip, JSONClass clipJSON, int serializeMode)
        {
            var controllersJSON = new JSONArray();
            foreach (var target in clip.targetControllers)
            {
                var controllerJSON = new JSONClass
                {
                    { "Controller", target.animatableRef.name },
                    { "TargetsPosition", target.targetsPosition ? "1" : "0" },
                    { "TargetsRotation", target.targetsRotation ? "1" : "0" },
                    { "ControlPosition", target.controlPosition ? "1" : "0" },
                    { "ControlRotation", target.controlRotation ? "1" : "0" },
                    { "X", SerializeCurve(target.position.x, serializeMode) },
                    { "Y", SerializeCurve(target.position.y, serializeMode) },
                    { "Z", SerializeCurve(target.position.z, serializeMode) },
                    { "RotX", SerializeCurve(target.rotation.rotX, serializeMode) },
                    { "RotY", SerializeCurve(target.rotation.rotY, serializeMode) },
                    { "RotZ", SerializeCurve(target.rotation.rotZ, serializeMode) },
                    { "RotW", SerializeCurve(target.rotation.rotW, serializeMode) }
                };
                if (!target.animatableRef.owned)
                {
                    if (target.animatableRef.controller == null)
                    {
                        SuperController.LogError($"Timeline: A target controller does not exist and will not be saved");
                        continue;
                    }

                    controllerJSON["Atom"] = target.animatableRef.controller.containingAtom.uid;
                }

                if (target.parentRigidbodyId != null)
                {
                    controllerJSON["Parent"] = new JSONClass
                    {
                        { "Atom", target.parentAtomId },
                        { "Rigidbody", target.parentRigidbodyId }
                    };
                }

                if (target.weight != 1f)
                {
                    controllerJSON["Weight"] = target.weight.ToString(CultureInfo.InvariantCulture);
                }

                if (target.group != null)
                {
                    controllerJSON["Group"] = target.group;
                }

                controllersJSON.Add(controllerJSON);
            }

            if (controllersJSON.Count > 0)
                clipJSON.Add("Controllers", controllersJSON);

            var floatParamsJSON = new JSONArray();
            foreach (var target in clip.targetFloatParams)
            {
                var paramJSON = new JSONClass
                {
                    { "Storable", target.animatableRef.storableId },
                    { "Name", target.animatableRef.floatParamName },
                    { "Value", SerializeCurve(target.value, serializeMode) }
                };
                if (!target.animatableRef.owned)
                {
                    if (!target.animatableRef.EnsureAvailable())
                    {
                        SuperController.LogError(
                            $"Timeline: Target {target.animatableRef.storableId} / {target.animatableRef.floatParamName} does not exist and will not be saved");
                        continue;
                    }

                    paramJSON["Atom"] = target.animatableRef.storable.containingAtom.uid;
                }

                if (target.group != null)
                {
                    paramJSON["Group"] = target.group;
                }

                var min = target.animatableRef.floatParam?.min ?? target.animatableRef.assignMinValueOnBound;
                if (min != null) paramJSON["Min"] = min.Value.ToString(CultureInfo.InvariantCulture);
                var max = target.animatableRef.floatParam?.max ?? target.animatableRef.assignMaxValueOnBound;
                if (max != null) paramJSON["Max"] = max.Value.ToString(CultureInfo.InvariantCulture);

                floatParamsJSON.Add(paramJSON);
            }

            if (floatParamsJSON.Count > 0)
                clipJSON.Add("FloatParams", floatParamsJSON);

            var triggersJSON = new JSONArray();
            foreach (var target in clip.targetTriggers)
            {
                var triggerJSON = new JSONClass
                {
                    { "Name", target.name },
                    { "Live", target.animatableRef.live ? "1" : "0" },
                };
                if (target.group != null)
                {
                    triggerJSON["Group"] = target.group;
                }

                var entriesJSON = new JSONArray();
                foreach (var x in target.triggersMap.OrderBy(kvp => kvp.Key))
                {
                    entriesJSON.Add(x.Value.GetJSON());
                }

                triggerJSON["Triggers"] = entriesJSON;
                triggersJSON.Add(triggerJSON);
            }

            if (triggersJSON.Count > 0)
                clipJSON.Add("Triggers", triggersJSON);
        }

        private static JSONNode SerializeCurve(BezierAnimationCurve curve, int serializeMode)
        {
            var curveJSON = new JSONArray();

            var lastV = 0f;
            var lastC = -1;

            for (var key = 0; key < curve.length; key++)
            {
                var keyframe = curve.GetKeyframeByKey(key);

                if (serializeMode == Modes.Optimized)
                {
                    var encoded = EncodeKeyframe(keyframe, lastV, lastC);
                    lastV = keyframe.value;
                    lastC = keyframe.curveType;
                    curveJSON.Add(encoded);
                }
                else
                {
                    var curveEntry = new JSONClass
                    {
                        ["t"] = keyframe.time.ToString(CultureInfo.InvariantCulture),
                    };
                    if (serializeMode == Modes.Full && keyframe.value != lastV)
                    {
                        curveEntry["v"] = keyframe.value.ToString(CultureInfo.InvariantCulture);
                        lastV = keyframe.value;
                    }
                    if (serializeMode == Modes.Full && keyframe.curveType != lastC)
                    {
                        curveEntry["c"] = keyframe.curveType.ToString(CultureInfo.InvariantCulture);
                        lastC = keyframe.curveType;
                    }
                    if (keyframe.curveType == CurveTypeValues.LeaveAsIs)
                    {
                        curveEntry["i"] = keyframe.controlPointIn.ToString(CultureInfo.InvariantCulture);
                        curveEntry["o"] = keyframe.controlPointOut.ToString(CultureInfo.InvariantCulture);
                    }
                    curveJSON.Add(curveEntry);
                }
            }

            return curveJSON;
        }

        #endregion

        #region Static serializers

        public static JSONClass SerializeQuaternion(Quaternion localRotation)
        {
            var jc = new JSONClass
            {
                ["x"] = { AsFloat = localRotation.x },
                ["y"] = { AsFloat = localRotation.y },
                ["z"] = { AsFloat = localRotation.z },
                ["w"] = { AsFloat = localRotation.w }
            };
            return jc;
        }

        public static JSONClass SerializeVector3(Vector3 localPosition)
        {
            var jc = new JSONClass
            {
                ["x"] = { AsFloat = localPosition.x },
                ["y"] = { AsFloat = localPosition.y },
                ["z"] = { AsFloat = localPosition.z }
            };
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

        public void RestoreMissingTriggers(AtomAnimation animation)
        {
            foreach (var t in animation.clips.SelectMany(c => c.targetTriggers))
            {
                // Allows accessing the self target
                t.RestoreMissing();
            }
        }

        private static readonly StringBuilder _encodeSb = new StringBuilder();

        [MethodImpl(256)]
        private static string EncodeKeyframe(BezierKeyframe keyframe, float lastV, int lastC)
        {
            _encodeSb.Length = 0;

            // Time and Value encoding
            var timeBytes = 4;
            var valueBytes = Math.Abs(lastV - keyframe.value) <= float.Epsilon ? 0 : 4;
            var curveTypeBytes = lastC == keyframe.curveType ? 0 : 1;

            // Encoding sizes
            _encodeSb.Append(EncodeSizes(timeBytes, valueBytes, curveTypeBytes));
            WriteBytes(keyframe.time, _encodeSb);
            if(valueBytes > 0) WriteBytes(keyframe.value, _encodeSb);
            if(curveTypeBytes > 0) WriteBytes((byte)keyframe.curveType, _encodeSb);

            return _encodeSb.ToString();
        }

        [MethodImpl(256)]
        private static void WriteBytes(float value, StringBuilder sb)
        {
            var bytes = BitConverter.GetBytes(value);
            for (var i = 0; i < bytes.Length; i++)
            {
                var b = bytes[i];
                WriteBytes(b, sb);
            }
        }

        [MethodImpl(256)]
        private static void WriteBytes(byte value, StringBuilder sb)
        {
            sb.Append(value.ToString("X2"));
        }

        [MethodImpl(256)]
        private static char EncodeSizes(int tSize, int vSize, int cSize)
        {
            var index = tSize * 25 + vSize * 5 + cSize;
            if (index < 10) return (char)('0' + index);
            if (index < 36) return (char)('a' + index - 10);
            return (char)('A' + index - 36);
        }

        [MethodImpl(256)]
        private static BezierKeyframe DecodeKeyframe(string encoded, float lastV, int lastC)
        {
            var sizeChar = encoded[0];
            int index;
            if (sizeChar >= '0' && sizeChar <= '9') index = sizeChar - '0';
            else if (sizeChar >= 'a' && sizeChar <= 'z') index = sizeChar - 'a' + 10;
            else index = sizeChar - 'A' + 36;

            var tBytes = index / 25;
            index %= 25;
            var vBytes = index / 5;
            var hasC = (index % 5) != 0;

            var t = DecodeFloat(encoded.Substring(1, tBytes * 2));
            var v = vBytes == 0 ? lastV : DecodeFloat(encoded.Substring(1 + tBytes * 2, vBytes * 2));
            var c = !hasC ? lastC : Convert.ToInt32(encoded.Substring(1 + (tBytes + vBytes) * 2, 2), 16);

            return new BezierKeyframe { time = t, value = v, curveType = c };
        }

        private static readonly byte[] _floatBuffer = new byte[4];

        [MethodImpl(256)]
        private static float DecodeFloat(string encoded)
        {
            for (var i = 0; i < 4; i++)
            {
                _floatBuffer[i] = Convert.ToByte(encoded.Substring(i * 2, 2), 16);
            }
            return BitConverter.ToSingle(_floatBuffer, 0);
        }
    }
}
