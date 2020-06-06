using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class AdvancedScreen : ScreenBase
    {
        private static readonly TimeSpan _importMocapTimeout = TimeSpan.FromSeconds(5);

        public const string ScreenName = "Advanced";
        private UIDynamicButton _importRecordedUI;
        private JSONStorableStringChooser _importRecordedOptionsJSON;
        private UIDynamicButton _reduceKeyframesUI;
        private JSONStorableFloat _reduceMinPosDistanceJSON;
        private JSONStorableFloat _reduceMinKeyframeTimeDeltaJSON;
        private JSONStorableFloat _importMaxKeyframesPerSecondJSON;

        public override string Name => ScreenName;

        public AdvancedScreen(IAtomPlugin plugin)
            : base(plugin)
        {

        }
        public override void Init()
        {
            base.Init();

            // Right side

            CreateChangeScreenButton("<b><</b> <i>Back</i>", MoreScreen.ScreenName, true);

            CreateSpacer(true);

            var keyframeCurrentPoseUI = Plugin.CreateButton("Keyframe Pose (All On)", true);
            keyframeCurrentPoseUI.button.onClick.AddListener(() => KeyframeCurrentPose(true));
            RegisterComponent(keyframeCurrentPoseUI);

            var keyframeCurrentPoseTrackedUI = Plugin.CreateButton("Keyframe Pose (Animated)", true);
            keyframeCurrentPoseTrackedUI.button.onClick.AddListener(() => KeyframeCurrentPose(false));
            RegisterComponent(keyframeCurrentPoseTrackedUI);

            CreateSpacer(true);

            var bakeUI = Plugin.CreateButton("Bake Animation (Arm & Record)", true);
            bakeUI.button.onClick.AddListener(() => Bake());
            RegisterComponent(bakeUI);

            CreateSpacer(true);

            _importRecordedOptionsJSON = new JSONStorableStringChooser(
                "Import Recorded Animation Options",
                 new List<string> { "Keyframe Reduction", "1 fps", "2 fps", "4 fps", "10 fps" },
                 "Keyframe Reduction",
                 "Import Recorded Animation Options")
            {
                isStorable = false
            };
            RegisterStorable(_importRecordedOptionsJSON);
            var importRecordedOptionsUI = Plugin.CreateScrollablePopup(_importRecordedOptionsJSON, true);
            RegisterComponent(importRecordedOptionsUI);

            _reduceMinPosDistanceJSON = new JSONStorableFloat("Minimum Value Delta", 0.04f, 0.001f, 0.1f, true);
            RegisterStorable(_reduceMinPosDistanceJSON);
            var reduceMinPosDistanceUI = Plugin.CreateSlider(_reduceMinPosDistanceJSON, true);
            RegisterComponent(reduceMinPosDistanceUI);

            _reduceMinKeyframeTimeDeltaJSON = new JSONStorableFloat("Minimum Time Dist Between Frames", 0.1f, 0.01f, 1f, true);
            RegisterStorable(_reduceMinKeyframeTimeDeltaJSON);
            var reduceMinKeyframeTimeDeltaUI = Plugin.CreateSlider(_reduceMinKeyframeTimeDeltaJSON, true);
            RegisterComponent(reduceMinKeyframeTimeDeltaUI);

            _importMaxKeyframesPerSecondJSON = new JSONStorableFloat("Max Keyframes per Second", 4f, 1f, 10f, true);
            RegisterStorable(_importMaxKeyframesPerSecondJSON);
            var importMaxKeyframesPerSecondUI = Plugin.CreateSlider(_importMaxKeyframesPerSecondJSON, true);
            RegisterComponent(importMaxKeyframesPerSecondUI);

            _importRecordedUI = Plugin.CreateButton("Import Recorded Animation (Mocap)", true);
            _importRecordedUI.button.onClick.AddListener(() => ImportRecorded());
            RegisterComponent(_importRecordedUI);

            _reduceKeyframesUI = Plugin.CreateButton("Reduce Float Params Keyframes", true);
            _reduceKeyframesUI.button.onClick.AddListener(() => ReduceKeyframes());
            RegisterComponent(_reduceKeyframesUI);

            CreateSpacer(true);

            var reverseAnimationUI = Plugin.CreateButton("Reverse Animation Keyframes", true);
            reverseAnimationUI.button.onClick.AddListener(() => ReverseAnimation());
            RegisterComponent(reverseAnimationUI);

            // TODO: Keyframe all animatable morphs
        }

        private void ReverseAnimation()
        {
            try
            {
                var anim = Current;
                if (anim == null) throw new NullReferenceException("No current animation to reverse");
                foreach (var target in anim.GetAllOrSelectedTargets())
                {
                    foreach (var curve in target.GetCurves())
                    {
                        curve.Reverse();
                    }

                    var controllerTarget = target as FreeControllerAnimationTarget;
                    if (controllerTarget != null)
                    {
                        var settings = controllerTarget.Settings.ToList();
                        var length = settings.Last().Key;
                        controllerTarget.Settings.Clear();
                        foreach (var setting in settings)
                        {
                            controllerTarget.Settings.Add(length - setting.Key, setting.Value);
                        }
                    }

                    target.Dirty = true;
                }
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AdvancedScreen)}.{nameof(ReverseAnimation)}: {exc}");
            }
        }

        private void KeyframeCurrentPose(bool all)
        {
            try
            {
                var time = Plugin.Animation.Time;
                foreach (var fc in Plugin.ContainingAtom.freeControllers)
                {
                    if (!fc.name.EndsWith("Control")) continue;
                    if (fc.currentPositionState != FreeControllerV3.PositionState.On) continue;
                    if (fc.currentRotationState != FreeControllerV3.RotationState.On) continue;

                    var target = Current.TargetControllers.FirstOrDefault(tc => tc.Controller == fc);
                    if (target == null)
                    {
                        if (!all) continue;
                        target = Plugin.Animation.Current.Add(fc);
                    }
                    Plugin.Animation.SetKeyframeToCurrentTransform(target, time);
                }
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AdvancedScreen)}.{nameof(KeyframeCurrentPose)}: {exc}");
            }
        }

        private void Bake()
        {
            try
            {
                var controllers = Plugin.Animation.Clips.SelectMany(c => c.TargetControllers).Select(c => c.Controller).Distinct().ToList();
                foreach (var mac in Plugin.ContainingAtom.motionAnimationControls)
                {
                    if (!controllers.Contains(mac.controller)) continue;
                    mac.armedForRecord = true;
                }

                Plugin.Animation.Play();
                SuperController.singleton.motionAnimationMaster.StartRecord();

                Plugin.StartCoroutine(StopWhenPlaybackIsComplete());
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AdvancedScreen)}.{nameof(Bake)}: {exc}");
            }
        }

        private IEnumerator StopWhenPlaybackIsComplete()
        {
            var waitFor = Plugin.Animation.Clips.Sum(c => c.NextAnimationTime.IsSameFrame(0) ? c.AnimationLength : c.NextAnimationTime);
            yield return new WaitForSeconds(waitFor);

            try
            {
                SuperController.singleton.motionAnimationMaster.StopRecord();
                Plugin.Animation.Stop();
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AdvancedScreen)}.{nameof(StopWhenPlaybackIsComplete)}: {exc}");
            }
        }


        private void ImportRecorded()
        {
            if (SuperController.singleton.motionAnimationMaster == null || Plugin.ContainingAtom.motionAnimationControls == null)
            {
                SuperController.LogError("Missing motion animation controls");
                return;
            }

            if (_importRecordedUI == null) return;
            if (_importRecordedUI.buttonText.text == "Importing, please wait...") return;
            _importRecordedUI.buttonText.text = "Importing, please wait...";

            Plugin.StartCoroutine(ImportRecordedCoroutine());
        }

        private IEnumerator ImportRecordedCoroutine()
        {
            var containingAtom = Plugin.ContainingAtom;
            var totalStopwatch = Stopwatch.StartNew();

            Current.Loop = SuperController.singleton.motionAnimationMaster.loop;

            float frameLength;
            switch (_importRecordedOptionsJSON.val)
            {
                case "Keyframe Reduction":
                    frameLength = 0f;
                    break;
                case "1 fps":
                    frameLength = 1f;
                    break;
                case "2 fps":
                    frameLength = 0.5f;
                    break;
                case "4 fps":
                    frameLength = 0.25f;
                    break;
                case "10 fps":
                    frameLength = 0.1f;
                    break;
                default:
                    SuperController.LogError($"Unknown import option {_importRecordedOptionsJSON.val}");
                    yield break;
            }

            yield return 0;

            foreach (var mot in containingAtom.motionAnimationControls)
            {
                FreeControllerAnimationTarget target = null;
                FreeControllerV3 ctrl;

                try
                {
                    if (mot == null || mot.clip == null) continue;
                    if (mot.clip.clipLength <= 0.001) continue;
                    ctrl = mot.controller;
                    Current.Remove(ctrl);
                    target = Plugin.Animation.Current.TargetControllers.FirstOrDefault(t => t.Controller == ctrl) ?? Plugin.Animation.Current.Add(ctrl);
                    target.StartBulkUpdates();
                    Current.AnimationLength = mot.clip.clipLength.Snap();
                    target.SetKeyframeToCurrentTransform(0);
                    target.SetKeyframeToCurrentTransform(Plugin.Animation.Current.AnimationLength);
                }
                catch (Exception exc)
                {
                    SuperController.LogError($"VamTimeline.{nameof(AdvancedScreen)}.{nameof(ImportRecordedCoroutine)}[Init]: {exc}");
                    target?.EndBulkUpdates();
                    yield break;
                }

                IEnumerator enumerator;
                try
                {
                    if (_importRecordedOptionsJSON.val == "Keyframe Reduction")
                        enumerator = ExtractFramesWithReductionTechnique(mot.clip, target, ctrl).GetEnumerator();
                    else
                        enumerator = ExtractFramesWithFpsTechnique(mot.clip, frameLength, target, ctrl).GetEnumerator();
                }
                catch
                {
                    target.EndBulkUpdates();
                    throw;
                }

                while (TryMoveNext(enumerator, target))
                    yield return enumerator.Current;

                target.EndBulkUpdates();
            }
            _importRecordedUI.buttonText.text = "Import Recorded Animation (Mocap)";
        }

        private bool TryMoveNext(IEnumerator enumerator, FreeControllerAnimationTarget target)
        {
            try
            {
                return enumerator.MoveNext();
            }
            catch
            {
                target.EndBulkUpdates();
                throw;
            }
        }

        private struct ControllerKeyframe
        {
            public float time;
            public Vector3 position;
            public Quaternion rotation;

            public static ControllerKeyframe FromStep(MotionAnimationStep s, Atom containingAtom, FreeControllerV3 ctrl)
            {
                var localPosition = s.positionOn ? s.position - containingAtom.transform.position : ctrl.transform.localPosition;
                var locationRotation = s.rotationOn ? Quaternion.Inverse(containingAtom.transform.rotation) * s.rotation : ctrl.transform.localRotation;
                return new ControllerKeyframe
                {
                    time = s.timeStep.Snap(),
                    position = localPosition,
                    rotation = locationRotation
                };
            }
        }

        private IEnumerable ExtractFramesWithReductionTechnique(MotionAnimationClip clip, FreeControllerAnimationTarget target, FreeControllerV3 ctrl)
        {
            var sw = Stopwatch.StartNew();
            var minPositionDistance = _reduceMinPosDistanceJSON.val;
            var minFrameDistance = _reduceMinKeyframeTimeDeltaJSON.val;
            var maxIterations = clip.clipLength * _importMaxKeyframesPerSecondJSON.val;

            var batchStopwatch = Stopwatch.StartNew();
            var containingAtom = Plugin.ContainingAtom;
            var simplify = new HashSet<float>();
            var steps = clip.steps
                .Where(s =>
                {
                    if (!s.positionOn && !s.rotationOn) return false;
                    var time = s.timeStep.Snap(0.01f);
                    if (simplify.Contains(time)) return false;
                    simplify.Add(time);
                    return true;
                })
                .Select(s => ControllerKeyframe.FromStep(s, containingAtom, ctrl))
                .ToList();

            target.SetKeyframe(0, steps[0].position, steps[0].rotation);
            target.SetKeyframe(clip.clipLength, steps[steps.Count].position, steps[steps.Count].rotation);

            for (var iteration = 0; iteration < maxIterations; iteration++)
            {
                var largestFrame = -1;
                var largestDiff = 0f;
                for (var i = 1; i < steps.Count - 1; i++)
                {
                    var diff = Math.Max(
                        Math.Abs(target.X.Evaluate(steps[i].time) - steps[i].position.x),
                        Math.Max(
                        Math.Abs(target.Y.Evaluate(steps[i].time) - steps[i].position.y),
                        Math.Abs(target.Z.Evaluate(steps[i].time) - steps[i].position.z)
                        )
                    );
                    if (diff > largestDiff)
                    {
                        largestDiff = diff;
                        largestFrame = i;
                    }
                }
                if (largestFrame != 0)
                {
                    if (largestDiff < minPositionDistance) break;
                    var step = steps[largestFrame];
                    steps.RemoveAt(largestFrame);
                    {
                        var i = largestFrame - 1;
                        var min = step.time - _reduceMinKeyframeTimeDeltaJSON.val;
                        while (i > 0)
                        {
                            var previousStep = steps[i];
                            if (previousStep.time > min)
                            {
                                steps.RemoveAt(i);
                                i--;
                                continue;
                            }
                            break;
                        }
                    }
                    {
                        var i = largestFrame + 1;
                        var max = step.time + _reduceMinKeyframeTimeDeltaJSON.val;
                        while (i < steps.Count)
                        {
                            var nextStep = steps[i];
                            if (nextStep.time < max)
                            {
                                steps.RemoveAt(i);
                                continue;
                            }
                            break;
                        }
                    }
                    var key = target.SetKeyframe(step.time, step.position, step.rotation);
                    target.SmoothNeighbors(key);
                }

                yield return 0;
            }

            SuperController.LogMessage($"Imported {clip.steps.Count} steps for {target.Name}, reduced to {target.GetLeadCurve().length} keyframes in {sw.Elapsed}");
        }

        private IEnumerable ExtractFramesWithFpsTechnique(MotionAnimationClip clip, float frameLength, FreeControllerAnimationTarget target, FreeControllerV3 ctrl)
        {
            var minPositionDistanceForFlat = 0.01f;
            var batchStopwatch = Stopwatch.StartNew();
            var containingAtom = Plugin.ContainingAtom;

            var lastRecordedFrame = float.MinValue;
            MotionAnimationStep previousStep = null;
            for (var stepIndex = 0; stepIndex < (clip.steps.Count - (Current.Loop ? 1 : 0)); stepIndex++)
            {
                try
                {
                    var step = clip.steps[stepIndex];
                    var frame = step.timeStep.Snap();
                    if (frame - lastRecordedFrame < frameLength) continue;
                    var k = ControllerKeyframe.FromStep(step, containingAtom, ctrl);
                    target.SetKeyframe(k.time, k.position, k.rotation);
                    if (previousStep != null && (target.Controller.name == "lFootControl" || target.Controller.name == "rFootControl") && Vector3.Distance(previousStep.position, step.position) <= minPositionDistanceForFlat)
                    {
                        KeyframeSettings settings;
                        if (target.Settings.TryGetValue(previousStep.timeStep.Snap().ToMilliseconds(), out settings))
                            target.ChangeCurve(previousStep.timeStep, CurveTypeValues.Linear);
                    }
                    lastRecordedFrame = frame;
                    previousStep = step;
                }
                catch (Exception exc)
                {
                    SuperController.LogError($"VamTimeline.{nameof(AdvancedScreen)}.{nameof(ImportRecordedCoroutine)}[Step]: {exc}");
                    yield break;
                }

                if (batchStopwatch.ElapsedMilliseconds > 5)
                {
                    batchStopwatch.Reset();
                    yield return 0;
                    batchStopwatch.Start();
                }
            }
        }

        private void ReduceKeyframes()
        {
            _reduceKeyframesUI.buttonText.text = "Optimizing, please wait...";
            _reduceKeyframesUI.button.interactable = false;

            Plugin.StartCoroutine(ReduceKeyframesCoroutine());
        }

        private IEnumerator ReduceKeyframesCoroutine()
        {
            foreach (var target in Current.GetAllOrSelectedTargets().OfType<FreeControllerAnimationTarget>())
            {
                target.StartBulkUpdates();
                try
                {
                    // ReduceKeyframes(target.X, target.Y, target.Z, target.RotX, target.RotY, target.RotZ, target.RotW);
                }
                catch (Exception exc)
                {
                    _reduceKeyframesUI.button.interactable = true;
                    _reduceKeyframesUI.buttonText.text = "Reduce Float Params Keyframes";
                    SuperController.LogError($"VamTimeline.{nameof(AdvancedScreen)}.{nameof(ReduceKeyframesCoroutine)}[FloatParam]: {exc}");
                    yield break;
                }
                finally
                {
                    target.Dirty = true;
                    target.EndBulkUpdates();
                }
                yield return 0;
            }

            foreach (var target in Current.GetAllOrSelectedTargets().OfType<FloatParamAnimationTarget>())
            {
                target.StartBulkUpdates();
                try
                {
                    ReduceKeyframes(target.Value);
                }
                catch (Exception exc)
                {
                    _reduceKeyframesUI.button.interactable = true;
                    _reduceKeyframesUI.buttonText.text = "Reduce Float Params Keyframes";
                    SuperController.LogError($"VamTimeline.{nameof(AdvancedScreen)}.{nameof(ReduceKeyframesCoroutine)}[FloatParam]: {exc}");
                    yield break;
                }
                finally
                {
                    target.Dirty = true;
                    target.EndBulkUpdates();
                }
                yield return 0;
            }

            _reduceKeyframesUI.button.interactable = true;
            _reduceKeyframesUI.buttonText.text = "Reduce Float Params Keyframes";
        }

        private void ReduceKeyframes(AnimationCurve source)
        {
            var minPositionDistance = _reduceMinPosDistanceJSON.val;
            var minFrameDistance = _reduceMinKeyframeTimeDeltaJSON.val;
            var maxIterations = source[source.length].time * _importMaxKeyframesPerSecondJSON.val;

            var batchStopwatch = Stopwatch.StartNew();
            var containingAtom = Plugin.ContainingAtom;
            var steps = new List<Keyframe>(source.keys);

            var target = new AnimationCurve();
            target.AddKey(0, source[0].value);
            target.AddKey(source[source.length - 1].time, source[source.length - 1].value);

            for (var iteration = 0; iteration < maxIterations; iteration++)
            {
                var largestFrame = -1;
                var largestDiff = 0f;
                for (var i = 1; i < steps.Count - 1; i++)
                {
                    var diff = Math.Abs(target.Evaluate(steps[i].time) - steps[i].value);
                    if (diff > largestDiff)
                    {
                        largestDiff = diff;
                        largestFrame = i;
                    }
                }
                if (largestFrame != 0)
                {
                    if (largestDiff < minPositionDistance) break;
                    var step = steps[largestFrame];
                    steps.RemoveAt(largestFrame);
                    {
                        var i = largestFrame - 1;
                        var min = step.time - _reduceMinKeyframeTimeDeltaJSON.val;
                        while (i > 0)
                        {
                            var previousStep = steps[i];
                            if (previousStep.time.Snap() > min)
                            {
                                steps.RemoveAt(i);
                                i--;
                                continue;
                            }
                            break;
                        }
                    }
                    {
                        var i = largestFrame + 1;
                        var max = step.time + _reduceMinKeyframeTimeDeltaJSON.val;
                        while (i < steps.Count)
                        {
                            var nextStep = steps[i];
                            if (nextStep.time.Snap() < max)
                            {
                                steps.RemoveAt(i);
                                continue;
                            }
                            break;
                        }
                    }
                    var key = target.AddKey(step.time, step.value);
                    if (key > -1)
                    {
                        target.SmoothTangents(key, 1f);
                        target.SmoothTangents(key - 1, 1f);
                        target.SmoothTangents(key + 1, 1f);
                    }
                }
            }

            SuperController.LogMessage($"Reduced {source.length} keyframes to {target.length} keyframes");

            source.keys = target.keys;
        }

        public override void Dispose()
        {
            _importRecordedUI = null;
            base.Dispose();
        }
    }
}

