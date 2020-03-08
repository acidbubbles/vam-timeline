using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class AtomAnimationAdvancedUI : AtomAnimationBaseUI
    {
        private static readonly Regex _sanitizeRE = new Regex("[^a-zA-Z0-9 _-]", RegexOptions.Compiled);

        public const string ScreenName = "Advanced";
        private JSONStorableStringChooser _exportAnimationsJSON;
        private JSONStorableStringChooser _importRecordedOptionsJSON;

        public override string Name => ScreenName;

        public AtomAnimationAdvancedUI(IAtomPlugin plugin)
            : base(plugin)
        {

        }
        public override void Init()
        {
            base.Init();

            // Left side

            InitAnimationSelectorUI(false);

            InitPlaybackUI(false);

            InitFrameNavUI(false);

            var enableAllTargetsUI = Plugin.CreateButton("Enable Miss. Targets On All Anims", true);
            enableAllTargetsUI.button.onClick.AddListener(() => EnableAllTargets());
            _components.Add(enableAllTargetsUI);

            CreateSpacer(true);

            var keyframeCurrentPoseUI = Plugin.CreateButton("Keyframe Pose (All On)", true);
            keyframeCurrentPoseUI.button.onClick.AddListener(() => KeyframeCurrentPose(true));
            _components.Add(keyframeCurrentPoseUI);

            var keyframeCurrentPoseTrackedUI = Plugin.CreateButton("Keyframe Pose (Animated)", true);
            keyframeCurrentPoseTrackedUI.button.onClick.AddListener(() => KeyframeCurrentPose(false));
            _components.Add(keyframeCurrentPoseTrackedUI);

            CreateSpacer(true);

            var bakeUI = Plugin.CreateButton("Bake Animation (Arm & Record)", true);
            bakeUI.button.onClick.AddListener(() => Bake());
            _components.Add(bakeUI);

            _importRecordedOptionsJSON = new JSONStorableStringChooser("Import Recorded Animation Options", new List<string> { "2fps", "10 fps", "100 fps" }, "10 fps", "Import Recorded Animation Options")
            {
                isStorable = false
            };
            var importRecordedOptionsUI = Plugin.CreateScrollablePopup(_importRecordedOptionsJSON, true);
            _linkedStorables.Add(_importRecordedOptionsJSON);

            var importRecordedUI = Plugin.CreateButton("Import Recorded Animation (Mocap)", true);
            importRecordedUI.button.onClick.AddListener(() => ImportRecorded());
            _components.Add(importRecordedUI);

            CreateSpacer(true);

            var moveAnimUpUI = Plugin.CreateButton("Reorder Animation (Move Up)", true);
            moveAnimUpUI.button.onClick.AddListener(() => ReorderAnimationMoveUp());
            _components.Add(moveAnimUpUI);

            var deleteAnimationUI = Plugin.CreateButton("Delete Animation", true);
            deleteAnimationUI.button.onClick.AddListener(() => DeleteAnimation());
            _components.Add(deleteAnimationUI);

            CreateSpacer(true);

            _exportAnimationsJSON = new JSONStorableStringChooser("Export Animation", new List<string> { "(All)" }.Concat(Plugin.Animation.GetAnimationNames()).ToList(), "(All)", "Export Animation")
            {
                isStorable = false
            };
            var exportAnimationsUI = Plugin.CreateScrollablePopup(_exportAnimationsJSON, true);
            _linkedStorables.Add(_exportAnimationsJSON);

            var exportUI = Plugin.CreateButton("Export to .json", true);
            exportUI.button.onClick.AddListener(() => Export());
            _components.Add(exportUI);

            var importUI = Plugin.CreateButton("Import from .json", true);
            importUI.button.onClick.AddListener(() => Import());
            _components.Add(importUI);

            // TODO: Keyframe all animatable morphs
        }

        private class FloatParamRef
        {
            public JSONStorable Storable { get; set; }
            public JSONStorableFloat FloatParam { get; set; }
        }

        private void EnableAllTargets()
        {
            try
            {
                var allControllers = Plugin.Animation.Clips.SelectMany(c => c.TargetControllers).Select(t => t.Controller).Distinct().ToList();
                var h = new HashSet<JSONStorableFloat>();
                var allFloatParams = Plugin.Animation.Clips.SelectMany(c => c.TargetFloatParams).Where(t => h.Add(t.FloatParam)).Select(t => new FloatParamRef { Storable = t.Storable, FloatParam = t.FloatParam }).ToList();

                foreach (var clip in Plugin.Animation.Clips)
                {
                    foreach (var controller in allControllers)
                    {
                        if (!clip.TargetControllers.Any(t => t.Controller == controller))
                        {
                            clip.Add(controller);
                        }
                    }
                    clip.TargetControllers.Sort(new FreeControllerAnimationTarget.Comparer());

                    foreach (var floatParamRef in allFloatParams)
                    {
                        if (!clip.TargetFloatParams.Any(t => t.FloatParam == floatParamRef.FloatParam))
                        {
                            clip.Add(floatParamRef.Storable, floatParamRef.FloatParam);
                        }
                    }
                    clip.TargetFloatParams.Sort(new FloatParamAnimationTarget.Comparer());
                }
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AtomAnimationAdvancedUI)}.{nameof(EnableAllTargets)}: {exc}");
            }
        }

        private void DeleteAnimation()
        {
            try
            {
                var anim = Plugin.Animation.Current;
                if (anim == null) return;
                if (Plugin.Animation.Clips.Count == 1)
                {
                    SuperController.LogError("VamTimeline: Cannot delete the only animation.");
                    return;
                }
                Plugin.Animation.Clips.Remove(anim);
                foreach (var clip in Plugin.Animation.Clips)
                {
                    if (clip.NextAnimationName == anim.AnimationName)
                    {
                        clip.NextAnimationName = null;
                        clip.NextAnimationTime = 0;
                    }
                }
                Plugin.Animation.ChangeAnimation(Plugin.Animation.Clips[0].AnimationName);
                Plugin.AnimationModified();
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AtomAnimationAdvancedUI)}.{nameof(DeleteAnimation)}: {exc}");
            }
        }

        private void ReorderAnimationMoveUp()
        {
            try
            {
                var anim = Plugin.Animation.Current;
                if (anim == null) return;
                var idx = Plugin.Animation.Clips.IndexOf(anim);
                if (idx <= 0) return;
                Plugin.Animation.Clips.RemoveAt(idx);
                Plugin.Animation.Clips.Insert(0, anim);
                Plugin.AnimationModified();
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AtomAnimationAdvancedUI)}.{nameof(ReorderAnimationMoveUp)}: {exc}");
            }
        }

        private void Export()
        {
            try
            {
                var fileBrowserUI = SuperController.singleton.fileBrowserUI;
                fileBrowserUI.defaultPath = SuperController.singleton.savesDirResolved;
                SuperController.singleton.activeUI = SuperController.ActiveUI.None;
                fileBrowserUI.SetTitle("Select Animation File");
                fileBrowserUI.SetTextEntry(true);
                fileBrowserUI.Show(ExportFileSelected);
                if (fileBrowserUI.fileEntryField != null)
                {
                    var dt = ((int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds).ToString();
                    fileBrowserUI.fileEntryField.text = _exportAnimationsJSON.val == "(All)" ? $"anims-{dt}" : $"anim-{_sanitizeRE.Replace(_exportAnimationsJSON.val, "")}-{dt}";
                    fileBrowserUI.ActivateFileNameField();
                }
                else
                {
                    SuperController.LogError("VamTimeline: No fileBrowserUI.fileEntryField");
                }
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline: Failed to save file dialog: {exc}");
            }
        }

        private void ExportFileSelected(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path)) return;

                if (!path.EndsWith(".json"))
                    path += ".json";

                var jc = Plugin.GetAnimationJSON(_exportAnimationsJSON.val == "(All)" ? null : _exportAnimationsJSON.val);
                jc["AtomType"] = Plugin.ContainingAtom.type;
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
                var fileBrowserUI = SuperController.singleton.fileBrowserUI;
                fileBrowserUI.defaultPath = SuperController.singleton.savesDirResolved;
                SuperController.singleton.activeUI = SuperController.ActiveUI.None;
                fileBrowserUI.SetTextEntry(false);
                fileBrowserUI.keepOpen = false;
                fileBrowserUI.SetTitle("Select Animation File");
                fileBrowserUI.Show(ImportFileSelected);
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline: Failed to open file dialog: {exc}");
            }
        }

        private void ImportFileSelected(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            var jc = SuperController.singleton.LoadJSON(path);
            if (jc["AtomType"]?.Value != Plugin.ContainingAtom.type)
            {
                SuperController.LogError($"VamTimeline: Loaded animation for {jc["AtomType"]} but current atom type is {Plugin.ContainingAtom.type}");
                return;
            }
            try
            {
                Plugin.Load(jc);
                Plugin.Animation.Stop();
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AtomAnimationAdvancedUI)}.{nameof(ImportFileSelected)}: Failed to import animation: {exc}");
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

                    var target = Plugin.Animation.Current.TargetControllers.FirstOrDefault(tc => tc.Controller == fc);
                    if (target == null)
                    {
                        if (!all) continue;
                        target = Plugin.Animation.Add(fc);
                    }
                    Plugin.Animation.SetKeyframeToCurrentTransform(target, time);
                }
                Plugin.Animation.RebuildAnimation();
                Plugin.AnimationModified();
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AtomAnimationAdvancedUI)}.{nameof(KeyframeCurrentPose)}: {exc}");
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
                SuperController.LogError($"VamTimeline.{nameof(AtomAnimationAdvancedUI)}.{nameof(Bake)}: {exc}");
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
                SuperController.LogError($"VamTimeline.{nameof(AtomAnimationAdvancedUI)}.{nameof(StopWhenPlaybackIsComplete)}: {exc}");
            }
        }

        private void ImportRecorded()
        {
            try
            {
                var minFrameDuration = 0.1f;
                switch (_importRecordedOptionsJSON.val)
                {
                    case "2 fps":
                        minFrameDuration = 0.5f;
                        break;
                    case "10 fps":
                        minFrameDuration = 0.1f;
                        break;
                    case "100 fps":
                        minFrameDuration = 0.01f;
                        break;
                }

                var containingAtom = Plugin.ContainingAtom;
                var current = Plugin.Animation.Current;
                current.Loop = SuperController.singleton.motionAnimationMaster.loop;
                foreach (var mot in containingAtom.motionAnimationControls)
                {
                    if (mot == null || mot.clip == null) continue;
                    if (mot.clip.clipLength <= 0.001) continue;
                    var ctrl = mot.controller;
                    current.Remove(ctrl);
                    var target = Plugin.Animation.Add(ctrl);
                    if (mot.clip.clipLength > current.AnimationLength)
                        current.CropOrExtendLengthEnd(mot.clip.clipLength.Snap());
                    var lastRecordedFrame = float.MinValue;
                    foreach (var step in mot.clip.steps)
                    {
                        if (!step.positionOn || !step.rotationOn)
                            continue;
                        var frame = step.timeStep.Snap();
                        if (frame - lastRecordedFrame < minFrameDuration) continue;
                        if (current.Loop && frame.IsSameFrame(mot.clip.clipLength)) continue;
                        target.SetKeyframe(frame, step.position - containingAtom.transform.position, Quaternion.Inverse(containingAtom.transform.rotation) * step.rotation);
                        lastRecordedFrame = frame;
                    }
                }
                Plugin.Animation.RebuildAnimation();
                Plugin.AnimationModified();
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(ImportRecorded)}.{nameof(Bake)}: {exc}");
            }
        }
    }
}

