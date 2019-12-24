using System;
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
        private AtomAnimation _animation;
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
        private JSONStorableStringChooser _frameFilterJSON;
        private JSONStorableFloat _speedJSON;

        #region Lifecycle

        public override void Init()
        {
            try
            {
                _serializer = new Serializer();

                _saveJSON = new JSONStorableString("Save", "");
                RegisterString(_saveJSON);
                RestoreState();

                _scrubberJSON = new JSONStorableFloat("Time", 0f, v => _animation.SetTime(v), 0f, _animation.AnimationLength - float.Epsilon, true);
                RegisterFloat(_scrubberJSON);
                CreateSlider(_scrubberJSON);

                var lengthJSON = new JSONStorableFloat("Animation Length", _animation.AnimationLength, v => { _animation.SetLength(v); _scrubberJSON.max = v - float.Epsilon; }, 0.5f, 120f);
                CreateSlider(lengthJSON, true);

                _speedJSON = new JSONStorableFloat("Speed", _animation.Speed, v => _animation.SetSpeed(v), 0.001f, 5f, false);
                RegisterFloat(_speedJSON);
                CreateSlider(_speedJSON, true);

                _frameFilterJSON = new JSONStorableStringChooser("Frame Filter", new List<string>(), "", "Frame Filter", val => { _animation.SelectControllerByName(val); RenderState(); });
                var frameFilterPopup = CreateScrollablePopup(_frameFilterJSON);
                frameFilterPopup.popupPanelHeight = 800f;
                frameFilterPopup.popup.onOpenPopupHandlers += () => _frameFilterJSON.choices = new List<string> { "" }.Concat(_animation.GetControllersName()).ToList();

                _nextFrameJSON = new JSONStorableAction("Next Frame", () => _animation.NextFrame());
                RegisterAction(_nextFrameJSON);
                CreateButton("Next Frame").button.onClick.AddListener(() => _nextFrameJSON.actionCallback());

                _previousFrameJSON = new JSONStorableAction("Previous Frame", () => _animation.PreviousFrame());
                RegisterAction(_previousFrameJSON);
                CreateButton("Previous Frame").button.onClick.AddListener(() => _previousFrameJSON.actionCallback());

                _playJSON = new JSONStorableAction("Play", () => _animation.Play());
                RegisterAction(_playJSON);
                CreateButton("Play").button.onClick.AddListener(() => _playJSON.actionCallback());

                // TODO: Should be a checkbox
                _pauseToggleJSON = new JSONStorableAction("Pause Toggle", () => _animation.PauseToggle());
                RegisterAction(_pauseToggleJSON);
                CreateButton("Pause Toggle").button.onClick.AddListener(() => _pauseToggleJSON.actionCallback());

                _stopJSON = new JSONStorableAction("Stop", () => _animation.Stop());
                RegisterAction(_stopJSON);
                CreateButton("Stop").button.onClick.AddListener(() => _stopJSON.actionCallback());

                _displayModeJSON = new JSONStorableStringChooser("Display Mode", RenderingModes.Values, RenderingModes.Default, "Display Mode");
                CreatePopup(_displayModeJSON);

                _displayJSON = new JSONStorableString("Display", "");
                CreateTextField(_displayJSON);

                _lockedJSON = new JSONStorableBool("Locked", false);
                RegisterBool(_lockedJSON);
                var lockedToggle = CreateToggle(_lockedJSON, true);
                lockedToggle.label = "Locked (performance mode)";

                _controllerJSON = new JSONStorableStringChooser("Target controller", containingAtom.freeControllers.Select(fc => fc.name).ToList(), containingAtom.freeControllers.Select(fc => fc.name).FirstOrDefault(), "Target controller");
                var controllerPopup = CreateScrollablePopup(_controllerJSON, true);
                controllerPopup.popupPanelHeight = 800f;

                CreateButton("Add", true).button.onClick.AddListener(() => AddSelectedController());
                CreateButton("Remove", true).button.onClick.AddListener(() => RemoveSelectedController());

                _animation.OnUpdated.AddListener(() => RenderState());

                CreateButton("Save").button.onClick.AddListener(() => SaveState());
                CreateButton("Restore").button.onClick.AddListener(() => { RestoreState(); RenderState(); });

                CreateButton("Delete Frame", true).button.onClick.AddListener(() => _animation.DeleteFrame());

                JSONStorableStringChooser changeCurveJSON = null;
                changeCurveJSON = new JSONStorableStringChooser("Change Curve", _animation.CurveTypes, "", "Change Curve", val => { _animation.ChangeCurve(val); if (!string.IsNullOrEmpty(val)) changeCurveJSON.val = ""; });
                var changeCurvePopup = CreatePopup(changeCurveJSON, true);
                changeCurvePopup.popupPanelHeight = 800f;

                RenderState();
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline Init: " + exc);
            }
        }

        private static readonly HashSet<string> GrabbingControllers = new HashSet<string> { "RightHandAnchor", "LeftHandAnchor", "MouseGrab", "SelectionHandles" };

        public void Update()
        {
            try
            {
                if (_lockedJSON.val) return;

                if (_animation.IsPlaying())
                {
                    RenderState();
                }
                else
                {
                    var grabbing = SuperController.singleton.RightGrabbedController ?? SuperController.singleton.LeftGrabbedController;
                    if (_grabbedController == null && grabbing != null)
                    {
                        _grabbedController = _animation.Controllers.FirstOrDefault(c => c.Controller == grabbing);
                    }
                    else if (_grabbedController == null && grabbing == null && Input.GetMouseButton(0))
                    {
                        _grabbedController = _animation.Controllers.FirstOrDefault(c => GrabbingControllers.Contains(c.Controller.linkToRB?.gameObject.name));
                    }
                    else if (_grabbedController != null && !grabbing)
                    {
                        // TODO: This should be done by the controller (updating the animatino resets the time)
                        var time = _animation.GetTime();
                        _grabbedController.SetKeyToCurrentPositionAndUpdate(time);
                        _animation.SetTime(time);
                        // TODO: This should not be here (the state should keep track of itself)
                        _animation.OnUpdated.Invoke();
                        _grabbedController = null;
                    }
                }
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline Update: " + exc);
            }
        }

        public void OnEnable()
        {
            try
            {
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline Enable: " + exc);
            }
        }

        public void OnDisable()
        {
            try
            {
                _animation.Stop();
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline Disable: " + exc);
            }
        }

        public void OnDestroy()
        {
            OnDisable();
        }

        #endregion

        #region Load / Save

        public void RestoreState()
        {
            try
            {
                if (_animation != null) _animation.OnUpdated.RemoveAllListeners();

                if (!string.IsNullOrEmpty(_saveJSON.val))
                {
                    _animation = _serializer.DeserializeAnimation(_saveJSON.val);
                    return;
                }

                var backupStorableID = containingAtom.GetStorableIDs().FirstOrDefault(s => s.EndsWith("_VamTimelineBackup"));
                if (backupStorableID != null)
                {
                    var backupStorable = containingAtom.GetStorableByID(backupStorableID);
                    var backupJSON = backupStorable.GetStringJSONParam("Backup");
                    if (!string.IsNullOrEmpty(backupJSON.val))
                    {
                        _animation = _serializer.DeserializeAnimation(backupJSON.val);
                    }
                }

            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline RestoreState: " + exc);
            }

            try
            {
                if (_animation == null) _animation = new AtomAnimation();

                _animation.OnUpdated.AddListener(() => RenderState());
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline RestoreState: " + exc);
            }
        }

        public void SaveState()
        {
            try
            {
                var serialized = _serializer.SerializeAnimation(_animation);
                _saveJSON.val = serialized;

                var backupStorableID = containingAtom.GetStorableIDs().FirstOrDefault(s => s.EndsWith("_VamTimelineBackup"));
                if (backupStorableID != null)
                {
                    var backupStorable = containingAtom.GetStorableByID(backupStorableID);
                    var backupJSON = backupStorable.GetStringJSONParam("Backup");
                    backupJSON.val = serialized;
                }
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline SaveState: " + exc);
            }
        }

        #endregion

        #region Target Selection

        private void AddSelectedController()
        {
            var uid = _controllerJSON.val;
            var controller = containingAtom.freeControllers.Where(x => x.name == uid).FirstOrDefault();
            if (controller == null)
            {
                SuperController.LogError($"Controller {uid} in atom {containingAtom.uid} does not exist");
                return;
            }
            controller.currentPositionState = FreeControllerV3.PositionState.On;
            controller.currentRotationState = FreeControllerV3.RotationState.On;
            _animation.Add(controller);
        }

        private void RemoveSelectedController()
        {
            var uid = _controllerJSON.val;
            var controller = containingAtom.freeControllers.Where(x => x.name == uid).FirstOrDefault();
            if (controller == null)
            {
                SuperController.LogError($"Controller {uid} in atom {containingAtom.uid} does not exist");
                return;
            }
            _animation.Remove(controller);
        }

        #endregion

        #region State Rendering

        public class RenderingModes
        {
            public const string Default = "Default";
            public const string Debug = "Debug";

            public static readonly List<string> Values = new List<string> { Default, Debug };
        }

        public void RenderState()
        {
            var time = _animation.GetTime();
            if (time != _scrubberJSON.val)
                _scrubberJSON.val = time;

            switch (_displayModeJSON.val)
            {
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
            display.AppendLine($"Time: {time}s");
            foreach (var controller in _animation.GetAllOrSelectedControllers())
            {
                display.AppendLine($"{controller.Controller.containingAtom.name}:{controller.Controller.name}");
                var keyTimes = controller.Curves.SelectMany(c => c.keys).Select(k => k.time).Distinct();
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
    }
}
