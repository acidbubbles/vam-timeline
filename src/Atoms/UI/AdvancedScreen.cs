using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using MVR.FileManagementSecure;
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
    public class AdvancedScreen : ScreenBase
    {
        private const string _saveExt = "json";
        private const string _saveFolder = "Saves\\animations";
        private static readonly Regex _sanitizeRE = new Regex("[^a-zA-Z0-9 _-]", RegexOptions.Compiled);
        private static readonly TimeSpan _importMocapTimeout = TimeSpan.FromSeconds(5);

        public const string ScreenName = "Advanced";
        private JSONStorableStringChooser _exportAnimationsJSON;
        private UIDynamicButton _importRecordedUI;
        private JSONStorableStringChooser _importRecordedOptionsJSON;

        public override string Name => ScreenName;

        public AdvancedScreen(IAtomPlugin plugin)
            : base(plugin)
        {

        }
        public override void Init()
        {
            base.Init();

            // Left side

            CreateSpacer(false);

            _exportAnimationsJSON = new JSONStorableStringChooser("Export Animation", new List<string> { "(All)" }.Concat(Plugin.Animation.GetAnimationNames()).ToList(), "(All)", "Export Animation")
            {
                isStorable = false
            };
            RegisterStorable(_exportAnimationsJSON);
            var exportAnimationsUI = Plugin.CreateScrollablePopup(_exportAnimationsJSON, false);
            RegisterComponent(exportAnimationsUI);

            var exportUI = Plugin.CreateButton("Export animation", false);
            exportUI.button.onClick.AddListener(() => Export());
            RegisterComponent(exportUI);

            var importUI = Plugin.CreateButton("Import animation", false);
            importUI.button.onClick.AddListener(() => Import());
            RegisterComponent(importUI);

            // Right side

            CreateChangeScreenButton("<b><</b> <i>Back</i>", MoreScreen.ScreenName, true);

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

            _importRecordedOptionsJSON = new JSONStorableStringChooser(
                "Import Recorded Animation Options",
                 new List<string> { "Keyframe Reduction", "1 fps", "2 fps", "10 fps" },
                 "Keyframe Reduction",
                 "Import Recorded Animation Options")
            {
                isStorable = false
            };
            RegisterStorable(_importRecordedOptionsJSON);
            var importRecordedOptionsUI = Plugin.CreateScrollablePopup(_importRecordedOptionsJSON, true);
            RegisterComponent(importRecordedOptionsUI);

            _importRecordedUI = Plugin.CreateButton("Import Recorded Animation (Mocap)", true);
            _importRecordedUI.button.onClick.AddListener(() => ImportRecorded());
            RegisterComponent(_importRecordedUI);

            CreateSpacer(true);

            var moveAnimUpUI = Plugin.CreateButton("Reorder Animation (Move Up)", true);
            moveAnimUpUI.button.onClick.AddListener(() => ReorderAnimationMoveUp());
            RegisterComponent(moveAnimUpUI);

            var moveAnimDownUI = Plugin.CreateButton("Reorder Animation (Move Down)", true);
            moveAnimDownUI.button.onClick.AddListener(() => ReorderAnimationMoveDown());
            RegisterComponent(moveAnimDownUI);

            var deleteAnimationUI = Plugin.CreateButton("Delete Animation", true);
            deleteAnimationUI.button.onClick.AddListener(() => DeleteAnimation());
            RegisterComponent(deleteAnimationUI);

            var reverseAnimationUI = Plugin.CreateButton("Reverse Animation", true);
            reverseAnimationUI.button.onClick.AddListener(() => ReverseAnimation());
            RegisterComponent(reverseAnimationUI);

            CreateSpacer(true);

            // TODO: Keyframe all animatable morphs
        }

        private void DeleteAnimation()
        {
            try
            {
                var anim = Current;
                if (anim == null) return;
                if (Plugin.Animation.Clips.Count == 1)
                {
                    SuperController.LogError("VamTimeline: Cannot delete the only animation.");
                    return;
                }
                Plugin.Animation.RemoveClip(anim);
                foreach (var clip in Plugin.Animation.Clips)
                {
                    if (clip.NextAnimationName == anim.AnimationName)
                    {
                        clip.NextAnimationName = null;
                        clip.NextAnimationTime = 0;
                    }
                }
                Plugin.ChangeAnimation(Plugin.Animation.Clips[0].AnimationName);
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AdvancedScreen)}.{nameof(DeleteAnimation)}: {exc}");
            }
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

        private void ReorderAnimationMoveUp()
        {
            try
            {
                var anim = Current;
                if (anim == null) return;
                var idx = Plugin.Animation.Clips.IndexOf(anim);
                if (idx <= 0) return;
                Plugin.Animation.Clips.RemoveAt(idx);
                Plugin.Animation.Clips.Insert(idx - 1, anim);
                Plugin.Animation.ClipsListChanged.Invoke();
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AdvancedScreen)}.{nameof(ReorderAnimationMoveUp)}: {exc}");
            }
        }

        private void ReorderAnimationMoveDown()
        {
            try
            {
                var anim = Current;
                if (anim == null) return;
                var idx = Plugin.Animation.Clips.IndexOf(anim);
                if (idx >= Plugin.Animation.Clips.Count - 1) return;
                Plugin.Animation.Clips.RemoveAt(idx);
                Plugin.Animation.Clips.Insert(idx + 1, anim);
                Plugin.Animation.ClipsListChanged.Invoke();
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AdvancedScreen)}.{nameof(ReorderAnimationMoveDown)}: {exc}");
            }
        }

        private void Export()
        {
            try
            {
                FileManagerSecure.CreateDirectory(_saveFolder);
                var fileBrowserUI = SuperController.singleton.fileBrowserUI;
                fileBrowserUI.SetTitle("Save animation");
                fileBrowserUI.fileRemovePrefix = null;
                fileBrowserUI.hideExtension = false;
                fileBrowserUI.keepOpen = false;
                fileBrowserUI.fileFormat = _saveExt;
                fileBrowserUI.defaultPath = _saveFolder;
                fileBrowserUI.showDirs = true;
                fileBrowserUI.shortCuts = null;
                fileBrowserUI.browseVarFilesAsDirectories = false;
                fileBrowserUI.SetTextEntry(true);
                fileBrowserUI.Show(ExportFileSelected);
                fileBrowserUI.ActivateFileNameField();
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline: Failed to save file dialog: {exc}");
            }
        }

        private void ExportFileSelected(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (!path.ToLower().EndsWith($".{_saveExt}")) path += $".{_saveExt}";

            try
            {
                var jc = Plugin.GetAnimationJSON(_exportAnimationsJSON.val == "(All)" ? null : _exportAnimationsJSON.val);
                jc["AtomType"] = Plugin.ContainingAtom.type;
                var atomState = new JSONClass();
                var allTargets = new HashSet<FreeControllerV3>(
                    Plugin.Animation.Clips
                        .Where(c => _exportAnimationsJSON.val == "(All)" || c.AnimationName == _exportAnimationsJSON.val)
                        .SelectMany(c => c.TargetControllers)
                        .Select(t => t.Controller)
                        .Distinct());
                foreach (var fc in Plugin.ContainingAtom.freeControllers)
                {
                    if (fc.name == "control") continue;
                    if (!fc.name.EndsWith("Control")) continue;
                    atomState[fc.name] = new JSONClass
                    {
                        {"currentPositionState", ((int)fc.currentPositionState).ToString()},
                        {"localPosition", AtomAnimationSerializer.SerializeVector3(fc.transform.localPosition)},
                        {"currentRotationState", ((int)fc.currentRotationState).ToString()},
                        {"localRotation", AtomAnimationSerializer.SerializeQuaternion(fc.transform.localRotation)}
                    };
                }
                jc["ControllersState"] = atomState;
                SuperController.singleton.SaveJSON(jc, path);
                SuperController.singleton.DoSaveScreenshot(path);
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline: Failed to export animation: {exc}");
            }
        }

        private void Import()
        {
            try
            {
                FileManagerSecure.CreateDirectory(_saveFolder);
                var shortcuts = FileManagerSecure.GetShortCutsForDirectory(_saveFolder);
                SuperController.singleton.GetMediaPathDialog(ImportFileSelected, _saveExt, _saveFolder, false, true, false, null, false, shortcuts);
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline: Failed to open file dialog: {exc}");
            }
        }

        private void ImportFileSelected(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                var json = SuperController.singleton.LoadJSON(path);
                if (json["AtomType"]?.Value != Plugin.ContainingAtom.type)
                {
                    SuperController.LogError($"VamTimeline: Loaded animation for {json["AtomType"]} but current atom type is {Plugin.ContainingAtom.type}");
                    return;
                }

                var jc = json.AsObject;
                if (jc.HasKey("ControllersState"))
                {
                    var controllersState = jc["ControllersState"].AsObject;
                    foreach (var k in controllersState.Keys)
                    {
                        var fc = Plugin.ContainingAtom.freeControllers.FirstOrDefault(x => x.name == k);
                        if (fc == null)
                        {
                            SuperController.LogError($"VamTimeline: Loaded animation had state for controller {k} but no such controller were found on this atom.");
                            continue;
                        }
                        var state = controllersState[k];
                        fc.currentPositionState = (FreeControllerV3.PositionState)state["currentPositionState"].AsInt;
                        fc.transform.localPosition = AtomAnimationSerializer.DeserializeVector3(state["localPosition"].AsObject);
                        fc.currentRotationState = (FreeControllerV3.RotationState)state["currentRotationState"].AsInt;
                        fc.transform.localRotation = AtomAnimationSerializer.DeserializeQuaternion(state["localRotation"].AsObject);
                    }
                }

                Plugin.Load(jc);
                Plugin.ChangeAnimation(jc["Clips"][0]["AnimationName"].Value);
                Plugin.Animation.Stop();
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AdvancedScreen)}.{nameof(ImportFileSelected)}: Failed to import animation: {exc}");
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
                    if (mot.clip.clipLength > Current.AnimationLength)
                        Current.CropOrExtendLengthEnd(mot.clip.clipLength.Snap());
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
                        enumerator = ExtractFramesWithReductionTechnique(mot.clip, Current, target, totalStopwatch, ctrl).GetEnumerator();
                    else
                        enumerator = ExtractFramesWithFpsTechnique(mot.clip, frameLength, Current, target, totalStopwatch, ctrl).GetEnumerator();
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

        private IEnumerable ExtractFramesWithReductionTechnique(MotionAnimationClip clip, AtomAnimationClip current, FreeControllerAnimationTarget target, Stopwatch totalStopwatch, FreeControllerV3 ctrl)
        {
            var minPositionDistance = 0.06f;
            var minPositionDistanceForFlat = 0.02f;
            var minFrameDistance = 0.1f;
            var maxIterations = (int)Math.Floor(Math.Sqrt(clip.clipLength * 10));

            var batchStopwatch = Stopwatch.StartNew();
            var containingAtom = Plugin.ContainingAtom;
            var simplify = new HashSet<float>();
            var steps = clip.steps.Where(s =>
            {
                var timeStep = s.timeStep.Snap(0.01f);
                if (simplify.Contains(timeStep)) return false;
                simplify.Add(timeStep);
                return true;
            }).ToList();
            var segmentKeyframes = new List<int> { 0, steps.Count - 1 };
            var skipKeyframes = new HashSet<int>();
            var curveX = new AnimationCurve();
            curveX.AddKey(0, steps[0].position.x);
            curveX.AddKey(0, steps[steps.Count - 1].position.x);
            var curveY = new AnimationCurve();
            curveY.AddKey(0, steps[0].position.y);
            curveY.AddKey(0, steps[steps.Count - 1].position.y);
            var curveZ = new AnimationCurve();
            curveZ.AddKey(0, steps[0].position.z);
            curveZ.AddKey(0, steps[steps.Count - 1].position.z);

            for (var iteration = 0; iteration < maxIterations; iteration++)
            {
                var splits = 0;
                for (var segmentIndex = 0; segmentIndex < segmentKeyframes.Count - 1; segmentIndex++)
                {
                    int firstIndex = segmentKeyframes[segmentIndex];
                    if (skipKeyframes.Contains(firstIndex)) continue;
                    var first = steps[firstIndex];
                    int lastIndex = segmentKeyframes[segmentIndex + 1] - 1;
                    var last = steps[lastIndex];
                    if (last.timeStep - first.timeStep < minFrameDistance)
                    {
                        skipKeyframes.Add(firstIndex);
                        continue;
                    }

                    var largestDelta = 0f;
                    var largestDeltaIndex = -1;
                    for (var stepIndex = firstIndex + 1; stepIndex < lastIndex - 1; stepIndex++)
                    {
                        try
                        {
                            var step = clip.steps[stepIndex];
                            Vector3 curvePosition = new Vector3(
                                curveX.Evaluate(step.timeStep),
                                curveY.Evaluate(step.timeStep),
                                curveZ.Evaluate(step.timeStep)
                            );
                            var actualDelta = Vector3.Distance(step.position, curvePosition);
                            if (actualDelta > largestDelta)
                            {
                                largestDelta = actualDelta;
                                largestDeltaIndex = stepIndex;
                            }
                        }
                        catch (Exception exc)
                        {
                            SuperController.LogError($"VamTimeline.{nameof(AdvancedScreen)}.{nameof(ExtractFramesWithReductionTechnique)}[Step]: {exc}");
                            yield break;
                        }

                        if (batchStopwatch.ElapsedMilliseconds > 5)
                        {
                            batchStopwatch.Reset();
                            yield return 0;
                            batchStopwatch.Start();
                        }
                    }
                    try
                    {
                        if (largestDelta > minPositionDistance)
                        {
                            segmentKeyframes.Insert(++segmentIndex, largestDeltaIndex);
                            var largestDeltaStep = steps[largestDeltaIndex];

                            var curveXKey = curveX.AddKey(largestDeltaStep.timeStep, largestDeltaStep.position.x);
                            if (curveXKey > 0) curveX.SmoothTangents(curveXKey - 1, 1f);
                            curveX.SmoothTangents(curveXKey, 1f);
                            if (curveXKey < curveX.length - 2) curveX.SmoothTangents(curveXKey + 1, 1f);

                            var curveYKey = curveY.AddKey(largestDeltaStep.timeStep, largestDeltaStep.position.y);
                            if (curveYKey > 0) curveY.SmoothTangents(curveYKey - 1, 1f);
                            curveY.SmoothTangents(curveYKey, 1f);
                            if (curveYKey < curveY.length - 2) curveY.SmoothTangents(curveYKey + 1, 1f);

                            var curveZKey = curveZ.AddKey(largestDeltaStep.timeStep, largestDeltaStep.position.z);
                            if (curveZKey > 0) curveZ.SmoothTangents(curveZKey - 1, 1f);
                            curveZ.SmoothTangents(curveZKey, 1f);
                            if (curveZKey < curveZ.length - 2) curveZ.SmoothTangents(curveZKey + 1, 1f);

                            splits++;
                        }
                        else
                        {
                            skipKeyframes.Add(firstIndex);
                        }

                        if (splits == 0)
                        {
                            break;
                        }
                    }
                    catch (Exception exc)
                    {
                        SuperController.LogError($"VamTimeline.{nameof(AdvancedScreen)}.{nameof(ExtractFramesWithReductionTechnique)}[Apply]: {exc}");
                        yield break;
                    }
                }
            }

            {
                int previousKey = 0;
                MotionAnimationStep previousStep = null;
                foreach (var key in segmentKeyframes.Where(k => k != -1))
                {
                    var step = steps[key];
                    string previousCurveType = null;
                    if (previousStep != null)
                    {
                        if (Vector3.Distance(previousStep.position, step.position) <= minPositionDistanceForFlat)
                        {
                            KeyframeSettings settings;
                            if (target.Settings.TryGetValue(previousStep.timeStep.ToMilliseconds(), out settings))
                            {
                                var foot = target.Controller.name == "lFootControl" || target.Controller.name == "rFootControl";
                                previousCurveType = foot ? CurveTypeValues.Linear : CurveTypeValues.Flat;
                                target.ChangeCurve(previousStep.timeStep, previousCurveType);
                            }
                        }
                        else if (key - previousKey > 3 && step.timeStep - previousStep.timeStep > 1f)
                        {
                            // Long distances can cause long curves, here we try to reduce that
                            // After at least three keys that spans over 1s, split
                            var middleStep = steps[previousKey + (key - previousKey) / 2];
                            SetKeyframeFromStep(target, ctrl, containingAtom, middleStep, middleStep.timeStep.Snap());
                        }
                    }
                    SetKeyframeFromStep(target, ctrl, containingAtom, step, step.timeStep.Snap());
                    if (previousCurveType != null) target.ChangeCurve(step.timeStep, previousCurveType);
                    previousKey = key;
                    previousStep = step;

                    if (batchStopwatch.ElapsedMilliseconds > 5)
                    {
                        batchStopwatch.Reset();
                        yield return 0;
                        batchStopwatch.Start();
                    }
                }
            }

            yield break;
        }


        private IEnumerable ExtractFramesWithFpsTechnique(MotionAnimationClip clip, float frameLength, AtomAnimationClip current, FreeControllerAnimationTarget target, Stopwatch totalStopwatch, FreeControllerV3 ctrl)
        {
            var minPositionDistanceForFlat = 0.01f;
            var batchStopwatch = Stopwatch.StartNew();
            var containingAtom = Plugin.ContainingAtom;

            var lastRecordedFrame = float.MinValue;
            MotionAnimationStep previousStep = null;
            for (var stepIndex = 0; stepIndex < (clip.steps.Count - (current.Loop ? 1 : 0)); stepIndex++)
            {
                try
                {
                    var step = clip.steps[stepIndex];
                    var frame = step.timeStep.Snap();
                    if (frame - lastRecordedFrame < frameLength) continue;
                    SetKeyframeFromStep(target, ctrl, containingAtom, step, frame);
                    if (previousStep != null && (target.Controller.name == "lFootControl" || target.Controller.name == "rFootControl") && Vector3.Distance(previousStep.position, step.position) <= minPositionDistanceForFlat)
                    {
                        KeyframeSettings settings;
                        if (target.Settings.TryGetValue(previousStep.timeStep.Snap().ToMilliseconds(), out settings))
                            target.ChangeCurve(previousStep.timeStep, CurveTypeValues.Linear);
                    }
                    lastRecordedFrame = frame;
                    previousStep = step;
                    if (totalStopwatch.Elapsed > _importMocapTimeout) throw new TimeoutException($"Importing took more that {_importMocapTimeout.TotalSeconds} seconds. Reached {step.timeStep}s of {clip.clipLength}s");
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

        private static void SetKeyframeFromStep(FreeControllerAnimationTarget target, FreeControllerV3 ctrl, Atom containingAtom, MotionAnimationStep step, float frame)
        {
            if (!step.positionOn && !step.rotationOn) return;
            var localPosition = step.positionOn ? step.position - containingAtom.transform.position : ctrl.transform.localPosition;
            var locationRotation = step.rotationOn ? Quaternion.Inverse(containingAtom.transform.rotation) * step.rotation : ctrl.transform.localRotation;
            target.SetKeyframe(
                frame,
                localPosition,
                locationRotation
            );
        }

        public override void Dispose()
        {
            _importRecordedUI = null;
            base.Dispose();
        }
    }
}

