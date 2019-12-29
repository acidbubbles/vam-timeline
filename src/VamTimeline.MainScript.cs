using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace AcidBubbles.VamTimeline
{
    /// <summary>
    /// VaM Timeline Controller
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class MainScript : MVRScript
    {
        private const int MaxUndo = 20;
        private const string AllControllers = "(All Controllers)";

        private AtomAnimation _animation;
        private JSONStorableStringChooser _animationJSON;
        private JSONStorableFloat _scrubberJSON;
        private JSONStorableBool _lockedJSON;
        private JSONStorableAction _playJSON;
        private JSONStorableAction _stopJSON;
        private JSONStorableStringChooser _displayModeJSON;
        private JSONStorableString _displayJSON;
        private JSONStorableStringChooser _controllerJSON;
        private JSONStorableString _saveJSON;
        private JSONStorableAction _pauseToggleJSON;
        private FreeControllerV3Animation _grabbedController;
        private JSONStorableAction _nextFrameJSON;
        private JSONStorableAction _previousFrameJSON;
        private Serializer _serializer;
        private JSONStorableStringChooser _selectedControllerJSON;
        private JSONStorableFloat _speedJSON;
        private JSONStorableFloat _lengthJSON;
        private JSONStorableFloat _blendDurationJSON;
        private readonly List<string> _undoList = new List<string>();
        private List<ClipboardEntry> _clipboard;
        private bool _restoring = true;


        #region Lifecycle

        public override void Init()
        {
            try
            {
                _serializer = new Serializer();

                _saveJSON = new JSONStorableString("Save", "", (string v) => RestoreState(v));
                RegisterString(_saveJSON);

                // Left side

                _animationJSON = new JSONStorableStringChooser("Animation", new List<string>(), "Anim1", "Animation", val => ChangeAnimation(val));
                _animationJSON.isStorable = false;
                RegisterStringChooser(_animationJSON);
                var animationPopup = CreateScrollablePopup(_animationJSON, false);
                animationPopup.popupPanelHeight = 800f;
                animationPopup.popup.onOpenPopupHandlers += () => _animationJSON.choices = _animation.Clips.Select(c => c.AnimationName).ToList();

                _scrubberJSON = new JSONStorableFloat("Time", 0f, v => _animation.Time = v, 0f, 5f - float.Epsilon, true);
                _scrubberJSON.isStorable = false;
                RegisterFloat(_scrubberJSON);
                CreateSlider(_scrubberJSON);

                _playJSON = new JSONStorableAction("Play", () => { _animation.Play(); ContextUpdated(); });
                RegisterAction(_playJSON);
                CreateButton("\u25B6 Play").button.onClick.AddListener(() => _playJSON.actionCallback());

                // TODO: Should be a checkbox
                _pauseToggleJSON = new JSONStorableAction("Pause Toggle", () => _animation.PauseToggle());
                RegisterAction(_pauseToggleJSON);
                CreateButton("\u258C\u258C Pause Toggle").button.onClick.AddListener(() => _pauseToggleJSON.actionCallback());

                _stopJSON = new JSONStorableAction("Stop", () => { _animation.Stop(); RenderState(); ContextUpdated(); });
                RegisterAction(_stopJSON);
                CreateButton("\u25A0 Stop").button.onClick.AddListener(() => _stopJSON.actionCallback());

                _selectedControllerJSON = new JSONStorableStringChooser("Selected Controller", new List<string> { AllControllers }, AllControllers, "Selected Controller", val => { _animation.SelectControllerByName(val == AllControllers ? "" : val); RenderState(); ContextUpdated(); });
                _selectedControllerJSON.isStorable = false;
                RegisterStringChooser(_selectedControllerJSON);
                var frameFilterPopup = CreateScrollablePopup(_selectedControllerJSON);
                frameFilterPopup.popupPanelHeight = 800f;

                _nextFrameJSON = new JSONStorableAction("Next Frame", () => { _animation.Time = _animation.GetNextFrame(); RenderState(); ContextUpdated(); });
                RegisterAction(_nextFrameJSON);
                CreateButton("\u2192 Next Frame").button.onClick.AddListener(() => _nextFrameJSON.actionCallback());

                _previousFrameJSON = new JSONStorableAction("Previous Frame", () => { _animation.Time = _animation.GetPreviousFrame(); RenderState(); ContextUpdated(); });
                RegisterAction(_previousFrameJSON);
                CreateButton("\u2190 Previous Frame").button.onClick.AddListener(() => _previousFrameJSON.actionCallback());

                JSONStorableStringChooser changeCurveJSON = null;
                changeCurveJSON = new JSONStorableStringChooser("Change Curve", CurveTypeValues.CurveTypes, "", "Change Curve", val => { _animation.ChangeCurve(val); if (!string.IsNullOrEmpty(val)) changeCurveJSON.val = ""; });
                var changeCurvePopup = CreatePopup(changeCurveJSON, false);
                changeCurvePopup.popupPanelHeight = 800f;

                CreateButton("Smooth All Frames", false).button.onClick.AddListener(() => SmoothAllFrames());

                CreateButton("Cut / Delete Frame", false).button.onClick.AddListener(() => Cut());
                CreateButton("Copy Frame", false).button.onClick.AddListener(() => Copy());
                CreateButton("Paste Frame", false).button.onClick.AddListener(() => Paste());

                // Right side

                _lockedJSON = new JSONStorableBool("Locked", false, (bool val) => { RenderState(); ContextUpdated(); });
                RegisterBool(_lockedJSON);
                var lockedToggle = CreateToggle(_lockedJSON, true);
                lockedToggle.label = "Locked (Performance Mode)";

                CreateButton("Insert New Animation Before", true).button.onClick.AddListener(() => AddAnimation(-1));
                CreateButton("Add New Animation After", true).button.onClick.AddListener(() => AddAnimation(1));

                _lengthJSON = new JSONStorableFloat("Animation Length", 5f, v => { if (v <= 0) return; _animation.AnimationLength = v; }, 0.5f, 120f, false, true);
                CreateSlider(_lengthJSON, true);

                _speedJSON = new JSONStorableFloat("Animation Speed", 1f, v => { if (v < 0) return; _animation.Speed = v; }, 0.001f, 5f, false);
                CreateSlider(_speedJSON, true);

                _blendDurationJSON = new JSONStorableFloat("Blend Duration", 1f, v => _animation.BlendDuration = v, 0.001f, 5f, false);
                CreateSlider(_blendDurationJSON, true);

                _controllerJSON = new JSONStorableStringChooser("Animate Controller", containingAtom.freeControllers.Select(fc => fc.name).ToList(), containingAtom.freeControllers.Select(fc => fc.name).FirstOrDefault(), "Animate controller");
                _controllerJSON.isStorable = false;
                var controllerPopup = CreateScrollablePopup(_controllerJSON, true);
                controllerPopup.popupPanelHeight = 800f;

                CreateButton("Add/Remove Controller", true).button.onClick.AddListener(() => AddSelectedController());

                var undoButton = CreateButton("Undo", true);
                // TODO: Right now it doesn't work for some reason...
                undoButton.button.interactable = false;
                undoButton.button.onClick.AddListener(() => Undo());

                _displayModeJSON = new JSONStorableStringChooser("Display Mode", RenderingModes.Values, RenderingModes.Default, "Display Mode", (string val) => { RenderState(); ContextUpdated(); });
                CreatePopup(_displayModeJSON, true);

                _displayJSON = new JSONStorableString("Display", "");
                _displayJSON.isStorable = false;
                RegisterString(_displayJSON);
                CreateTextField(_displayJSON, true);

                // Try loading from backup
                StartCoroutine(CreateAnimationIfNoneIsLoaded());
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.Init: " + exc);
            }
        }

        private void SmoothAllFrames()
        {
            _animation.SmoothAllFrames();
        }

        private void Paste()
        {
            try
            {
                if (_clipboard == null)
                {
                    SuperController.LogMessage("Clipboard is empty");
                    return;
                }
                var time = _animation.Time;
                foreach (var entry in _clipboard)
                {
                    var animController = _animation.Current.Controllers.FirstOrDefault(c => c.Controller == entry.Controller);
                    if (animController == null)
                        animController = _animation.Add(entry.Controller);
                    animController.SetCurveSnapshot(time, entry.Snapshot);
                }
                _animation.RebuildAnimation();
                // Sample animation now
                _animation.Time = time;
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.Paste: " + exc);
            }
        }

        private void Copy()
        {
            try
            {
                var clipboard = new List<ClipboardEntry>();
                var time = _animation.Time;
                foreach (var controller in _animation.Current.GetAllOrSelectedControllers())
                {
                    clipboard.Add(new ClipboardEntry
                    {
                        Controller = controller.Controller,
                        Snapshot = controller.GetCurveSnapshot(time)
                    });
                }
                _clipboard = clipboard;
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.Copy: " + exc);
            }
        }

        private void Cut()
        {
            _animation.DeleteFrame();
        }

        private void Undo()
        {
            if (_undoList.Count == 0) return;
            var pop = _undoList[_undoList.Count - 1];
            _undoList.RemoveAt(_undoList.Count - 1);
            RestoreState(pop);
            _saveJSON.valNoCallback = pop;
        }

        private IEnumerator CreateAnimationIfNoneIsLoaded()
        {
            if (_animation != null) yield break;
            yield return new WaitForEndOfFrame();
            RestoreState("");
        }

        private static readonly HashSet<string> GrabbingControllers = new HashSet<string> { "RightHandAnchor", "LeftHandAnchor", "MouseGrab", "SelectionHandles" };

        public void Update()
        {
            try
            {
                if (_lockedJSON == null || _lockedJSON.val || _animation == null || _animation.Current == null) return;

                if (_animation.IsPlaying())
                {
                    var time = _animation.Time;
                    if (time != _scrubberJSON.val)
                        _scrubberJSON.valNoCallback = time;
                    // RenderState() // In practice, we don't see anything useful
                }
                else
                {
                    var grabbing = SuperController.singleton.RightGrabbedController ?? SuperController.singleton.LeftGrabbedController;
                    if (grabbing != null && grabbing.containingAtom != containingAtom)
                        grabbing = null;
                    else if (Input.GetMouseButton(0) && grabbing == null)
                        grabbing = containingAtom.freeControllers.FirstOrDefault(c => GrabbingControllers.Contains(c.linkToRB?.gameObject.name));

                    if (_grabbedController == null && grabbing != null)
                    {
                        _grabbedController = _animation.Current.Controllers.FirstOrDefault(c => c.Controller == grabbing);
                        _controllerJSON.val = grabbing.name;
                    }
                    else if (_grabbedController != null && grabbing == null)
                    {
                        // TODO: This should be done by the controller (updating the animatino resets the time)
                        var time = _animation.Time;
                        _grabbedController.SetKeyToCurrentControllerTransform(time);
                        _animation.RebuildAnimation();
                        _animation.Time = time;
                        _grabbedController = null;
                        RenderState();
                    }
                }
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.Update: " + exc);
            }
        }

        public void OnEnable()
        {
            try
            {
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.Enable: " + exc);
            }
        }

        public void OnDisable()
        {
            try
            {
                _animation?.Stop();
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.Disable: " + exc);
            }
        }

        public void OnDestroy()
        {
            OnDisable();
        }

        #endregion

        #region Load / Save

        public void RestoreState(string json)
        {
            _restoring = true;

            try
            {
                if (_animation != null)
                {
                    _animation.Updated.RemoveAllListeners();
                    _animation = null;
                }

                if (!string.IsNullOrEmpty(json))
                {
                    _animation = _serializer.DeserializeAnimation(containingAtom, json);
                }

                if (_animation == null)
                {
                    var backupStorableID = containingAtom.GetStorableIDs().FirstOrDefault(s => s.EndsWith("VamTimelineBackup"));
                    if (backupStorableID != null)
                    {
                        var backupStorable = containingAtom.GetStorableByID(backupStorableID);
                        var backupJSON = backupStorable.GetStringJSONParam("Backup");
                        if (!string.IsNullOrEmpty(backupJSON.val))
                        {
                            _animation = _serializer.DeserializeAnimation(containingAtom, backupJSON.val);
                        }
                    }
                }

            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.RestoreState: " + exc);
            }

            try
            {
                if (_animation == null)
                    _animation = new AtomAnimation(containingAtom);

                _animation.Initialize();
                _animation.Updated.AddListener(() => AnimationUpdated());
                AnimationUpdated();
                ContextUpdated();
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.RestoreState: " + exc);
            }

            _restoring = false;
        }

        public void SaveState()
        {
            try
            {
                if (_restoring) return;

                // Never save an empty animation.
                if (_animation.Clips.Count == 0) return;
                if (_animation.Clips.Count == 1 && _animation.Clips[0].Controllers.Count == 0) return;

                var serialized = _serializer.SerializeAnimation(_animation);

                if (serialized == _undoList.LastOrDefault())
                    return;

                if (!string.IsNullOrEmpty(_saveJSON.val))
                {
                    _undoList.Add(_saveJSON.val);
                    if (_undoList.Count > MaxUndo) _undoList.RemoveAt(0);
                }

                _saveJSON.valNoCallback = serialized;

                var backupStorableID = containingAtom.GetStorableIDs().FirstOrDefault(s => s.EndsWith("VamTimelineBackup"));
                if (backupStorableID != null)
                {
                    var backupStorable = containingAtom.GetStorableByID(backupStorableID);
                    var backupJSON = backupStorable.GetStringJSONParam("Backup");
                    backupJSON.val = serialized;
                }
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.SaveState: " + exc);
            }
        }

        #endregion

        #region Target Selection

        private void AddSelectedController()
        {
            try
            {
                var uid = _controllerJSON.val;
                var controller = containingAtom.freeControllers.Where(x => x.name == uid).FirstOrDefault();
                if (controller == null)
                {
                    SuperController.LogError($"Controller {uid} in atom {containingAtom.uid} does not exist");
                    return;
                }
                if (_animation.Current.Controllers.Any(c => c.Controller == controller))
                {

                    _animation.Remove(controller);
                }
                {
                    controller.currentPositionState = FreeControllerV3.PositionState.On;
                    controller.currentRotationState = FreeControllerV3.RotationState.On;
                    var animController = _animation.Add(controller);
                    animController.SetKeyToCurrentControllerTransform(0f);
                }
                _animation.Updated.Invoke();
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.AddSelectedController: " + exc);
            }
        }

        private void ChangeAnimation(string animationName)
        {
            try
            {
                _selectedControllerJSON.val = AllControllers;
                _animation.ChangeAnimation(animationName);
                _speedJSON.valNoCallback = _animation.Speed;
                _lengthJSON.valNoCallback = _animation.AnimationLength;
                _scrubberJSON.max = _animation.AnimationLength - float.Epsilon;
                RenderState();
                ContextUpdated();
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.ChangeAnimation: " + exc);
            }
        }

        private void AddAnimation(int index)
        {
            // TODO: Deal with the index
            // TODO: Let the user name the animation
            var lastAnimationName = _animation.Clips.Last().AnimationName;
            var lastAnimationIndex = lastAnimationName.Substring(4);
            var animationName = "Anim" + (int.Parse(lastAnimationIndex) + 1);
            var clip = new AtomAnimationClip(animationName);

            clip.Speed = _animation.Speed;
            clip.AnimationLength = _animation.AnimationLength;
            foreach (var controller in _animation.Current.Controllers.Select(c => c.Controller))
                clip.Add(controller);

            _animation.AddClip(clip);
            AnimationUpdated();
            ChangeAnimation(animationName);
        }

        #endregion

        #region State Rendering

        public class RenderingModes
        {
            public const string None = "None";
            public const string Default = "Default";
            public const string Debug = "Debug";

            public static readonly List<string> Values = new List<string> { None, Default, Debug };
        }

        public void RenderState()
        {
            if (_lockedJSON.val)
            {
                _displayJSON.val = "Locked";
                return;
            }

            var time = _animation.Time;
            if (time != _scrubberJSON.val)
                _scrubberJSON.valNoCallback = time;

            switch (_displayModeJSON.val)
            {
                case RenderingModes.None:
                    _displayJSON.val = "";
                    break;
                case RenderingModes.Default:
                    RenderStateDefault();
                    break;
                case RenderingModes.Debug:
                    RenderStateDebug();
                    break;
                default:
                    throw new NotSupportedException($"Unknown rendering mode {_displayModeJSON.val}");
            }
        }

        public void RenderStateDefault()
        {
            var time = _scrubberJSON.val;
            var display = new StringBuilder();
            foreach (var controller in _animation.GetAllOrSelectedControllers())
            {
                display.AppendLine($"{controller.Controller.containingAtom.name}:{controller.Controller.name}");
                var keyTimes = controller.Curves.SelectMany(c => c.keys.Take(c.keys.Length - 1)).Select(k => k.time).Distinct();
                foreach (var keyTime in keyTimes)
                {
                    display.Append($"{(keyTime == time ? "[" : " ")}{keyTime:0.00}{(keyTime == time ? "]" : " ")}");
                }
                display.AppendLine();
            }
            _displayJSON.val = display.ToString();
        }

        public void RenderStateDebug()
        {
            var time = _scrubberJSON.val;
            var display = new StringBuilder();
            display.AppendLine($"Time: {time}s");
            foreach (var controller in _animation.GetAllOrSelectedControllers())
            {
                display.AppendLine($"{controller.Controller.containingAtom.name}:{controller.Controller.name}");
                RenderStateController(time, display, "X", controller.X);
                RenderStateController(time, display, "Y", controller.Y);
                RenderStateController(time, display, "Z", controller.Z);
                RenderStateController(time, display, "RotX", controller.RotX);
                RenderStateController(time, display, "RotY", controller.RotY);
                RenderStateController(time, display, "RotZ", controller.RotZ);
                RenderStateController(time, display, "RotW", controller.RotW);
            }
            _displayJSON.val = display.ToString();
        }

        private static void RenderStateController(float time, StringBuilder display, string name, AnimationCurve curve)
        {
            display.AppendLine($"{name}");
            foreach (var keyframe in curve.keys)
            {
                display.AppendLine($"  {(keyframe.time == time ? "+" : "-")} {keyframe.time:0.00}s: {keyframe.value:0.00}");
                display.AppendLine($"    Tngt in: {keyframe.inTangent:0.00} out: {keyframe.outTangent:0.00}");
                display.AppendLine($"    Wght in: {keyframe.inWeight:0.00} out: {keyframe.outWeight:0.00} {keyframe.weightedMode}");
            }
        }

        #endregion

        #region Updates

        private void AnimationUpdated()
        {
            try
            {
                // Update UI
                _animationJSON.choices = _animation.Clips.Select(c => c.AnimationName).ToList();
                _lengthJSON.valNoCallback = _animation.AnimationLength;
                _speedJSON.valNoCallback = _animation.Speed;
                _lengthJSON.valNoCallback = _animation.AnimationLength;
                _blendDurationJSON.valNoCallback = _animation.BlendDuration;
                _scrubberJSON.max = _animation.AnimationLength - float.Epsilon;
                _selectedControllerJSON.choices = new List<string> { AllControllers }.Concat(_animation.GetControllersName()).ToList();

                // Save
                SaveState();

                // Dispatch to VamTimelineController
                var externalControllers = SuperController.singleton.GetAtoms().Where(a => a.type == "SimpleSign");
                foreach (var controller in externalControllers)
                    controller.BroadcastMessage("VamTimelineAnimationUpdated", containingAtom.uid);

                // Render
                RenderState();
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.AnimationUpdated: " + exc);
            }
        }

        private void ContextUpdated()
        {
            try
            {
                // Dispatch to VamTimelineController
                var externalControllers = SuperController.singleton.GetAtoms().Where(a => a.type == "SimpleSign");
                foreach (var controller in externalControllers)
                    controller.BroadcastMessage("VamTimelineContextChanged", containingAtom.uid);
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.ContextUpdated: " + exc);
            }
        }

        private class ClipboardEntry
        {
            internal FreeControllerV3 Controller;
            internal FreeControllerV3Snapshot Snapshot;
        }

        #endregion
    }
}
