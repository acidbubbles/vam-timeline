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
        private JSONStorableStringChooser _frameFilterJSON;
        private JSONStorableFloat _speedJSON;
        private JSONStorableFloat _lengthJSON;
        private JSONStorableFloat _blendDurationJSON;

        #region Lifecycle

        public override void Init()
        {
            try
            {
                _serializer = new Serializer();

                _saveJSON = new JSONStorableString("Save", "");
                RegisterString(_saveJSON);
                RestoreState();

                _animationJSON = new JSONStorableStringChooser("Animation", new List<string>(), "Anim1", "Animation", val => ChangeAnimation(val));
                RegisterStringChooser(_animationJSON);
                var animationPopup = CreateScrollablePopup(_animationJSON, true);
                animationPopup.popupPanelHeight = 800f;
                animationPopup.popup.onOpenPopupHandlers += () => _animationJSON.choices = _animation.Clips.Select(c => c.AnimationName).ToList();

                _scrubberJSON = new JSONStorableFloat("Time", 0f, v => _animation.Time = v, 0f, _animation.AnimationLength - float.Epsilon, true);
                RegisterFloat(_scrubberJSON);
                CreateSlider(_scrubberJSON);

                _lengthJSON = new JSONStorableFloat("Animation Length", _animation.AnimationLength, v => { _animation.AnimationLength = v; _scrubberJSON.max = v - float.Epsilon; }, 0.5f, 120f);
                CreateSlider(_lengthJSON, true);

                _speedJSON = new JSONStorableFloat("Speed", _animation.Speed, v => _animation.Speed = v, 0.001f, 5f, false);
                // TODO: Do not register JSON we don't want accessible outside. Everything is saved in local state.
                RegisterFloat(_speedJSON);
                CreateSlider(_speedJSON, true);

                _frameFilterJSON = new JSONStorableStringChooser("Frame Filter", new List<string>(), "", "Frame Filter", val => { _animation.SelectControllerByName(val); RenderState(); });
                var frameFilterPopup = CreateScrollablePopup(_frameFilterJSON);
                frameFilterPopup.popupPanelHeight = 800f;
                frameFilterPopup.popup.onOpenPopupHandlers += () => _frameFilterJSON.choices = new List<string> { "" }.Concat(_animation.GetControllersName()).ToList();

                _nextFrameJSON = new JSONStorableAction("Next Frame", () => { _animation.Time = _animation.GetNextFrame(); RenderState(); });
                RegisterAction(_nextFrameJSON);
                CreateButton("Next Frame").button.onClick.AddListener(() => _nextFrameJSON.actionCallback());

                _previousFrameJSON = new JSONStorableAction("Previous Frame", () => { _animation.Time = _animation.GetPreviousFrame(); RenderState(); });
                RegisterAction(_previousFrameJSON);
                CreateButton("Previous Frame").button.onClick.AddListener(() => _previousFrameJSON.actionCallback());

                _playJSON = new JSONStorableAction("Play", () => _animation.Play());
                RegisterAction(_playJSON);
                CreateButton("Play").button.onClick.AddListener(() => _playJSON.actionCallback());

                // TODO: Should be a checkbox
                _pauseToggleJSON = new JSONStorableAction("Pause Toggle", () => _animation.PauseToggle());
                RegisterAction(_pauseToggleJSON);
                CreateButton("Pause Toggle").button.onClick.AddListener(() => _pauseToggleJSON.actionCallback());

                _stopJSON = new JSONStorableAction("Stop", () => { _animation.Stop(); RenderState(); });
                RegisterAction(_stopJSON);
                CreateButton("Stop").button.onClick.AddListener(() => _stopJSON.actionCallback());

                _displayModeJSON = new JSONStorableStringChooser("Display Mode", RenderingModes.Values, RenderingModes.Default, "Display Mode", (string val) => RenderState());
                CreatePopup(_displayModeJSON);

                _displayJSON = new JSONStorableString("Display", "");
                CreateTextField(_displayJSON);

                _lockedJSON = new JSONStorableBool("Locked", false, (bool val) => RenderState());
                RegisterBool(_lockedJSON);
                var lockedToggle = CreateToggle(_lockedJSON, true);
                lockedToggle.label = "Locked (performance mode)";

                _controllerJSON = new JSONStorableStringChooser("Target controller", containingAtom.freeControllers.Select(fc => fc.name).ToList(), containingAtom.freeControllers.Select(fc => fc.name).FirstOrDefault(), "Target controller");
                var controllerPopup = CreateScrollablePopup(_controllerJSON, true);
                controllerPopup.popupPanelHeight = 800f;

                CreateButton("Add", true).button.onClick.AddListener(() => AddSelectedController());
                CreateButton("Remove", true).button.onClick.AddListener(() => RemoveSelectedController());

                CreateButton("Save").button.onClick.AddListener(() => SaveState());
                CreateButton("Restore").button.onClick.AddListener(() => { RestoreState(); RenderState(); });

                CreateButton("Delete Frame", true).button.onClick.AddListener(() => _animation.DeleteFrame());

                JSONStorableStringChooser changeCurveJSON = null;
                changeCurveJSON = new JSONStorableStringChooser("Change Curve", CurveTypeValues.CurveTypes, "", "Change Curve", val => { _animation.ChangeCurve(val); if (!string.IsNullOrEmpty(val)) changeCurveJSON.val = ""; });
                var changeCurvePopup = CreatePopup(changeCurveJSON, true);
                changeCurvePopup.popupPanelHeight = 800f;

                CreateButton("New Animation", true).button.onClick.AddListener(() => AddAnimation());

                _blendDurationJSON = new JSONStorableFloat("Blend Duration", _animation.BlendDuration, v => _animation.BlendDuration = v, 0.001f, 5f, false);
                RegisterFloat(_blendDurationJSON);
                CreateSlider(_blendDurationJSON, true);

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
                if (_lockedJSON == null) return;
                if (_lockedJSON.val) return;
                if (_animation.Current == null) return;

                if (_animation.IsPlaying())
                {
                    RenderState();
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
                        _grabbedController.SetKeyToCurrentPositionAndUpdate(time);
                        _animation.RebuildAnimation();
                        _animation.Time = time;
                        _grabbedController = null;
                        RenderState();
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
                _animation?.Stop();
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
                if (!string.IsNullOrEmpty(_saveJSON.val))
                {
                    _animation = _serializer.DeserializeAnimation(containingAtom, _saveJSON.val);
                    return;
                }

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
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline RestoreState: " + exc);
            }

            try
            {
                if (_animation == null)
                {
                    _animation = new AtomAnimation(containingAtom);
                    _animation.Initialize();
                }

                if (_animationJSON != null)
                    _animationJSON.choices = _animation.Clips.Select(c => c.AnimationName).ToList();
                if (_lengthJSON != null)
                    _lengthJSON.val = _animation.AnimationLength;
                if (_speedJSON != null)
                    _speedJSON.val = _animation.Speed;
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
            RenderState();
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
            RenderState();
        }

        private void ChangeAnimation(string animationName)
        {
            _animation.ChangeAnimation(animationName);
            if (_animationJSON.val != animationName) _animationJSON.val = animationName;
            _speedJSON.valNoCallback = _animation.Speed;
            _lengthJSON.valNoCallback = _animation.AnimationLength;
            RenderState();
        }

        private void AddAnimation()
        {
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
            _animationJSON.choices = _animation.Clips.Select(c => c.AnimationName).ToList();
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
                _scrubberJSON.val = time;

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
            display.AppendLine($"Time: {time}s");
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
            foreach (var keyframe in curve.keys.Take(curve.keys.Length - 1))
            {
                display.AppendLine($"  {(keyframe.time == time ? "+" : "-")} {keyframe.time:0.00}s: {keyframe.value:0.00}");
                display.AppendLine($"    Tngt in: {keyframe.inTangent:0.00} out: {keyframe.outTangent:0.00}");
                display.AppendLine($"    Wght in: {keyframe.inWeight:0.00} out: {keyframe.outWeight:0.00} {keyframe.weightedMode}");
            }
        }

        #endregion
    }
}
