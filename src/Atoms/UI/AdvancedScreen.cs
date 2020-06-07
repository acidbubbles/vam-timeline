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
        private JSONStorableFloat _reduceMaxFramesPerSecondJSON;
        private JSONStorableFloat _reduceMinRotationJSON;

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
                 new List<string> { "Keyframe Reduction", "Fixed Frames per Second" },
                 "Keyframe Reduction",
                 "Import Recorded Animation Options")
            {
                isStorable = false
            };
            RegisterStorable(_importRecordedOptionsJSON);
            var importRecordedOptionsUI = Plugin.CreateScrollablePopup(_importRecordedOptionsJSON, true);
            RegisterComponent(importRecordedOptionsUI);

            _reduceMinPosDistanceJSON = new JSONStorableFloat("Minimum Distance Between Frames", 0.04f, 0.001f, 0.5f, true);
            RegisterStorable(_reduceMinPosDistanceJSON);
            var reduceMinPosDistanceUI = Plugin.CreateSlider(_reduceMinPosDistanceJSON, true);
            RegisterComponent(reduceMinPosDistanceUI);

            _reduceMinRotationJSON = new JSONStorableFloat("Minimum Rotation Between Frames", 10f, 0.1f, 90f, true);
            RegisterStorable(_reduceMinRotationJSON);
            var reduceMinRotationUI = Plugin.CreateSlider(_reduceMinRotationJSON, true);
            RegisterComponent(reduceMinRotationUI);

            _reduceMaxFramesPerSecondJSON = new JSONStorableFloat("Max Frames per Second", 5f, (float val) => _reduceMaxFramesPerSecondJSON.valNoCallback = Mathf.Round(val), 1f, 10f, true);
            RegisterStorable(_reduceMaxFramesPerSecondJSON);
            var maxFramesPerSecondUI = Plugin.CreateSlider(_reduceMaxFramesPerSecondJSON, true);
            RegisterComponent(maxFramesPerSecondUI);

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
            _importRecordedUI.buttonText.text = "Importing, please wait...";
            _importRecordedUI.button.interactable = false;

            Plugin.StartCoroutine(ImportRecordedCoroutine());
        }

        private IEnumerator ImportRecordedCoroutine()
        {
            var containingAtom = Plugin.ContainingAtom;
            var totalStopwatch = Stopwatch.StartNew();

            Current.Loop = SuperController.singleton.motionAnimationMaster.loop;
            Current.AnimationLength = containingAtom.motionAnimationControls[0].clip.clipLength.Snap(0.01f);

            yield return 0;

            var controlCounter = 0;
            foreach (var mot in containingAtom.motionAnimationControls)
            {
                FreeControllerAnimationTarget target = null;
                FreeControllerV3 ctrl;

                _importRecordedUI.buttonText.text = $"Importing, please wait... ({++controlCounter} / {containingAtom.motionAnimationControls.Length})";

                try
                {
                    if (mot == null || mot.clip == null) continue;
                    if (mot.clip.clipLength <= 0.1) continue;
                    ctrl = mot.controller;
                    Current.Remove(ctrl);
                    target = Current.Add(ctrl);
                    target.StartBulkUpdates();
                    target.SetKeyframeToCurrentTransform(0);
                    target.SetKeyframeToCurrentTransform(Current.AnimationLength);
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
                        enumerator = ExtractFramesWithFpsTechnique(mot.clip, target, ctrl).GetEnumerator();
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
            _importRecordedUI.button.interactable = true;
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

            public static ControllerKeyframe FromStep(float time, MotionAnimationStep s, Atom containingAtom, FreeControllerV3 ctrl)
            {
                var localPosition = s.positionOn ? s.position - containingAtom.transform.position : ctrl.transform.localPosition;
                var locationRotation = s.rotationOn ? Quaternion.Inverse(containingAtom.transform.rotation) * s.rotation : ctrl.transform.localRotation;
                return new ControllerKeyframe
                {
                    time = time,
                    position = localPosition,
                    rotation = locationRotation
                };
            }
        }

        private IEnumerable ExtractFramesWithReductionTechnique(MotionAnimationClip clip, FreeControllerAnimationTarget target, FreeControllerV3 ctrl)
        {
            var sw = Stopwatch.StartNew();
            var minFrameDistance = 1f / _reduceMaxFramesPerSecondJSON.val;
            var maxIterations = (int)(clip.clipLength * 10);

            var batchStopwatch = Stopwatch.StartNew();
            var containingAtom = Plugin.ContainingAtom;
            var steps = clip.steps
                .Where(s => s.positionOn || s.rotationOn)
                .GroupBy(s => s.timeStep.Snap(minFrameDistance).ToMilliseconds())
                .Select(g =>
                {
                    var step = g.OrderBy(s => Math.Abs(g.Key - s.timeStep)).First();
                    return ControllerKeyframe.FromStep((g.Key / 1000f).Snap(), step, containingAtom, ctrl);
                })
                .ToList();

            target.SetKeyframe(0, steps[0].position, steps[0].rotation);
            target.SetKeyframe(Current.AnimationLength, steps[steps.Count - 1].position, steps[steps.Count - 1].rotation);

            for (var iteration = 0; iteration < maxIterations; iteration++)
            {
                // Scan for largest difference with curve
                // TODO: When we add a keyframe, we only need to rescan between the keyframe before and after. We could optimize by building a list of buckets in which the max deltas are known, and avoid re-scanning if the dirty bucket does not have the largest delta.
                var keyWithLargestPositionDistance = -1;
                var largestPositionDistance = 0f;
                var keyWithLargestRotationAngle = -1;
                var largestRotationAngle = 0f;
                for (var i = 1; i < steps.Count - 1; i++)
                {
                    var positionDiff = Vector3.Distance(
                        new Vector3(
                            target.X.Evaluate(steps[i].time),
                            target.Y.Evaluate(steps[i].time),
                            target.Z.Evaluate(steps[i].time)
                        ),
                        steps[i].position
                    );
                    if (positionDiff > largestPositionDistance)
                    {
                        largestPositionDistance = positionDiff;
                        keyWithLargestPositionDistance = i;
                    }

                    var rotationAngle = Vector3.Angle(
                        new Quaternion(
                            target.RotX.Evaluate(steps[i].time),
                            target.RotY.Evaluate(steps[i].time),
                            target.RotZ.Evaluate(steps[i].time),
                            target.RotW.Evaluate(steps[i].time)
                        ).eulerAngles,
                        steps[i].rotation.eulerAngles
                        );
                    if (rotationAngle > largestRotationAngle)
                    {
                        largestRotationAngle = rotationAngle;
                        keyWithLargestRotationAngle = i;
                    }
                }

                // Cannot find large enough diffs, exit
                if (keyWithLargestRotationAngle == -1 || keyWithLargestPositionDistance == -1) break;
                var posInRange = largestPositionDistance >= _reduceMinPosDistanceJSON.val;
                var rotInRange = largestRotationAngle >= _reduceMinRotationJSON.val;
                if (!posInRange && !rotInRange) break;

                // This is an attempt to compare translations and rotations
                var normalizedPositionDistance = largestPositionDistance / 0.4f;
                var normalizedRotationAngle = largestRotationAngle / 180f;
                var selectPosOverRot = (normalizedPositionDistance > normalizedRotationAngle) && posInRange;
                var keyToApply = selectPosOverRot ? keyWithLargestPositionDistance : keyWithLargestRotationAngle;

                var step = steps[keyToApply];
                steps.RemoveAt(keyToApply);
                SuperController.LogMessage("" + step.time);
                var key = target.SetKeyframe(step.time, step.position, step.rotation);
                target.SmoothNeighbors(key);

                yield return 0;
            }
        }

        private IEnumerable ExtractFramesWithFpsTechnique(MotionAnimationClip clip, FreeControllerAnimationTarget target, FreeControllerV3 ctrl)
        {
            var minPositionDistanceForFlat = 0.01f;
            var batchStopwatch = Stopwatch.StartNew();
            var containingAtom = Plugin.ContainingAtom;
            var frameLength = 1f / _reduceMaxFramesPerSecondJSON.val;

            var lastRecordedFrame = float.MinValue;
            MotionAnimationStep previousStep = null;
            for (var stepIndex = 0; stepIndex < (clip.steps.Count - (Current.Loop ? 1 : 0)); stepIndex++)
            {
                try
                {
                    var step = clip.steps[stepIndex];
                    var time = step.timeStep.Snap(0.01f);
                    if (time - lastRecordedFrame < frameLength) continue;
                    var k = ControllerKeyframe.FromStep(time, step, containingAtom, ctrl);
                    target.SetKeyframe(time, k.position, k.rotation);
                    if (previousStep != null && (target.Controller.name == "lFootControl" || target.Controller.name == "rFootControl") && Vector3.Distance(previousStep.position, step.position) <= minPositionDistanceForFlat)
                    {
                        KeyframeSettings settings;
                        if (target.Settings.TryGetValue(previousStep.timeStep.Snap().ToMilliseconds(), out settings))
                            target.ChangeCurve(previousStep.timeStep, CurveTypeValues.Linear);
                    }
                    lastRecordedFrame = time;
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
            var sw = Stopwatch.StartNew();
            var minFrameDistance = 1f / _reduceMaxFramesPerSecondJSON.val;
            var maxIterations = (int)(source[source.length - 1].time * 10);

            var batchStopwatch = Stopwatch.StartNew();
            var containingAtom = Plugin.ContainingAtom;
            var steps = source.keys
                .GroupBy(s => s.time.Snap(minFrameDistance).ToMilliseconds())
                .Select(g =>
                {
                    var keyframe = g.OrderBy(s => Math.Abs(g.Key - s.time)).First();
                    return new Keyframe((g.Key / 1000f).Snap(), keyframe.value, 0, 0);
                })
                .ToList();

            var target = new AnimationCurve();
            target.FlatFrame(target.AddKey(0, source[0].value));
            target.FlatFrame(target.AddKey(source[source.length - 1].time, source[source.length - 1].value));

            for (var iteration = 0; iteration < maxIterations; iteration++)
            {
                // Scan for largest difference with curve
                // TODO: When we add a keyframe, we only need to rescan between the keyframe before and after. We could optimize by building a list of buckets in which the max deltas are known, and avoid re-scanning if the dirty bucket does not have the largest delta.
                var keyWithLargestDiff = -1;
                var largestDiff = 0f;
                for (var i = 1; i < steps.Count - 1; i++)
                {
                    var diff = Mathf.Abs(target.Evaluate(steps[i].time) - steps[i].value);

                    if (diff > largestDiff)
                    {
                        largestDiff = diff;
                        keyWithLargestDiff = i;
                    }
                }

                // Cannot find large enough diffs, exit
                if (keyWithLargestDiff == -1) break;
                var inRange = largestDiff >= _reduceMinPosDistanceJSON.val;
                if (!inRange) break;

                // This is an attempt to compare translations and rotations
                var keyToApply = keyWithLargestDiff;

                var step = steps[keyToApply];
                steps.RemoveAt(keyToApply);
                var key = target.SetKeyframe(step.time, step.value);
                target.FlatFrame(key);
            }

            source.keys = target.keys;
        }

        public override void Dispose()
        {
            _importRecordedUI = null;
            base.Dispose();
        }
    }
}

