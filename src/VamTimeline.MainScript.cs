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
        private State _state;
        private JSONStorableFloat _scrubberJSON;
        private JSONStorableBool _lockedJSON;
        private JSONStorableAction _playJSON;
        private JSONStorableAction _stopJSON;
        private JSONStorableString _displayJSON;
        private JSONStorableStringChooser _atomJSON;
        private JSONStorableStringChooser _controllerJSON;
        private FreeControllerV3 _selectedController;
        private JSONStorableString _saveJSON;
        private JSONStorableAction _pauseToggleJSON;
        private ControllerState _grabbedController;
        private JSONStorableAction _nextFrameJSON;
        private JSONStorableAction _previousFrameJSON;

        #region Lifecycle

        public override void Init()
        {
            try
            {
                _state = new State();

                // TODO: Hardcoded loop length
                _scrubberJSON = new JSONStorableFloat("Time", 0f, v => _state.SetTime(v), 0f, 5f, true);
                RegisterFloat(_scrubberJSON);
                CreateSlider(_scrubberJSON);

                _nextFrameJSON = new JSONStorableAction("Next Frame", () => _state.NextFrame());
                RegisterAction(_nextFrameJSON);
                CreateButton("Next Frame").button.onClick.AddListener(() => _nextFrameJSON.actionCallback());

                _previousFrameJSON = new JSONStorableAction("Previous Frame", () => _state.PreviousFrame());
                RegisterAction(_previousFrameJSON);
                CreateButton("Previous Frame").button.onClick.AddListener(() => _previousFrameJSON.actionCallback());

                _playJSON = new JSONStorableAction("Play", () => _state.Play());
                RegisterAction(_playJSON);
                CreateButton("Play").button.onClick.AddListener(() => _playJSON.actionCallback());

                // TODO: Should be a checkbox
                _pauseToggleJSON = new JSONStorableAction("Pause Toggle", () => _state.PauseToggle());
                RegisterAction(_pauseToggleJSON);
                CreateButton("Pause Toggle").button.onClick.AddListener(() => _pauseToggleJSON.actionCallback());

                _stopJSON = new JSONStorableAction("Stop", () => _state.Stop());
                RegisterAction(_stopJSON);
                CreateButton("Stop").button.onClick.AddListener(() => _stopJSON.actionCallback());

                _displayJSON = new JSONStorableString("TimelineDisplay", "");
                CreateTextField(_displayJSON);

                _lockedJSON = new JSONStorableBool("Locked", false);
                RegisterBool(_lockedJSON);
                var lockedToggle = CreateToggle(_lockedJSON, true);
                lockedToggle.label = "Locked (performance mode)";

                _atomJSON = new JSONStorableStringChooser("Target atom", SuperController.singleton.GetAtomUIDs(), "", "Target atom", uid => OnTargetAtomChanged(uid));
                _controllerJSON = new JSONStorableStringChooser("Target controller", new List<string>(), "", "Target controller", uid => OnTargetControllerChanged(uid));

                var atomPopup = CreateScrollablePopup(_atomJSON, true);
                atomPopup.popupPanelHeight = 800f;
                atomPopup.popup.onOpenPopupHandlers += () => _atomJSON.choices = SuperController.singleton.GetAtomUIDs();

                var controllerPopup = CreateScrollablePopup(_controllerJSON, true);
                controllerPopup.popupPanelHeight = 800f;

                SuperController.singleton.onAtomUIDsChangedHandlers += (uids) => OnAtomsChanged(uids);
                OnAtomsChanged(SuperController.singleton.GetAtomUIDs());

                CreateButton("Add", true).button.onClick.AddListener(() => AddSelectedController());
                CreateButton("Remove", true).button.onClick.AddListener(() => RemoveSelectedController());

                _state.OnUpdated.AddListener(() => RenderState());

                _saveJSON = new JSONStorableString("Save", "");
                RegisterString(_saveJSON);
                RestoreState();

                CreateButton("Save").button.onClick.AddListener(() => SaveState());
                CreateButton("Restore").button.onClick.AddListener(() => RestoreState());
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimelineController Init: " + exc);
            }
        }

        private static readonly HashSet<string> GrabbingControllers = new HashSet<string> { "RightHandAnchor", "LeftHandAnchor", "MouseGrab", "SelectionHandles" };

        public void Update()
        {
            try
            {
                if (_lockedJSON.val) return;

                if (_state.IsPlaying())
                {
                    RenderState();
                }
                else
                {
                    // NOTE: If we had access to SuperController.instance.rightGrabbedController (and left) and grabbedControllerMouse we would not have to scan the controllers.
                    var grabbing = SuperController.singleton.GetRightGrab() || SuperController.singleton.GetLeftGrab() || Input.GetMouseButton(0);
                    if (_grabbedController == null && grabbing)
                    {
                        // SuperController.singleton.ClearMessages();
                        // SuperController.LogMessage("Grabbing: " + _state.Controllers.FirstOrDefault()?.Controller.linkToRB?.gameObject.name);
                        _grabbedController = _state.Controllers.FirstOrDefault(c => GrabbingControllers.Contains(c.Controller.linkToRB?.gameObject.name));
                    }
                    else if (_grabbedController != null && !grabbing)
                    {
                        // TODO: This should be done by the controller (updating the animatino resets the time)
                        var time = _state.GetTime();
                        _grabbedController.SetKeyToCurrentPositionAndUpdate(_scrubberJSON.val);
                        _state.SetTime(time);
                        // TODO: This should not be here (the state should keep track of itself)
                        _state.OnUpdated.Invoke();
                        _grabbedController = null;
                    }
                }
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimelineController Update: " + exc);
            }
        }

        public void OnEnable()
        {
            try
            {
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimelineController Enable: " + exc);
            }
        }

        public void OnDisable()
        {
            try
            {
                _state.Stop();
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimelineController Disable: " + exc);
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
                    _state = DeserializeState(_saveJSON.val);
                    return;
                }

                var backupJSON = containingAtom.GetStorableByID("VamTimelineBackup")?.GetStringParamValue("Backup");

                if (!string.IsNullOrEmpty(backupJSON))
                {
                    _state = DeserializeState(backupJSON);
                }
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimelineController RestoreState: " + exc);
            }
        }

        private State DeserializeState(string val)
        {
            SuperController.LogMessage("Deserializing: " + val);
            return new State();
        }

        public void SaveState()
        {
            try
            {
                var serialized = SerializeState(_state);
                _saveJSON.val = serialized;

                var backupStorableID = containingAtom.GetStorableIDs().FirstOrDefault(s => s.EndsWith("_VamTimelineBackup"));
                SuperController.LogMessage(backupStorableID);
                if (backupStorableID != null)
                {
                    var backupStorable = containingAtom.GetStorableByID(backupStorableID);
                    var backupJSON = backupStorable.GetStringJSONParam("Backup");
                    SuperController.LogMessage(backupJSON.val);
                    backupJSON.val = serialized;
                    SuperController.LogMessage(backupJSON.val);
                }
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimelineController SaveState: " + exc);
            }
        }

        private string SerializeState(State state)
        {
            var sb = new StringBuilder();
            foreach (var controller in _state.Controllers)
            {
                sb.AppendLine($"{controller.Controller.containingAtom.name}/{controller.Controller.name}");
                sb.AppendLine($"  X");
                foreach (var x in controller.X.keys)
                    sb.AppendLine($"    {x.time}: {x.value}");

            }
            return sb.ToString();
        }

        #endregion

        #region Target Selection

        private void OnAtomsChanged(List<string> uids)
        {
            try
            {
                var atoms = new List<string>(uids);
                atoms.Insert(0, "");
                _atomJSON.choices = atoms;
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimelineController OnAtomsChanged: " + exc);
            }
        }

        private void OnTargetAtomChanged(string uid)
        {
            try
            {
                if (uid == "")
                {
                    _controllerJSON.choices = new List<string>(new[] { "" });
                    _controllerJSON.val = "";
                    _selectedController = null;
                    return;
                }

                var atom = SuperController.singleton.GetAtomByUid(uid);
                if (atom == null)
                {
                    SuperController.LogError($"Atom {uid} does not exist");
                    return;
                }
                var controllers = atom.freeControllers.Select(x => x.name).ToList();
                controllers.Insert(0, "");
                _controllerJSON.choices = controllers;
                _controllerJSON.val = controllers.FirstOrDefault(s => s == _controllerJSON.val) ?? "";
            }
            catch (Exception exc)
            {
                SuperController.LogError("UISlider OnTargetAtomChanged: " + exc);
            }
        }

        private void OnTargetControllerChanged(string uid)
        {
            _selectedController = null;

            var atom = SuperController.singleton.GetAtomByUid(_atomJSON.val);
            if (atom == null)
            {
                SuperController.LogError($"Atom {_atomJSON.val} does not exist");
                return;
            }
            var controller = atom.freeControllers.Where(x => x.name == uid).FirstOrDefault();
            if (controller == null)
            {
                SuperController.LogError($"Controller {uid} in atom {_atomJSON.val} does not exist");
                return;
            }

            _selectedController = controller;
        }

        private void AddSelectedController()
        {
            if (_selectedController != null)
            {
                _selectedController.currentPositionState = FreeControllerV3.PositionState.On;
                _selectedController.currentRotationState = FreeControllerV3.RotationState.On;
                _state.Add(_selectedController);
            }
        }

        private void RemoveSelectedController()
        {
            if (_selectedController != null)
                _state.Remove(_selectedController);
        }

        #endregion

        #region State Rendering

        public void RenderState()
        {
            var time = _state.GetTime();
            if (time != _scrubberJSON.val)
                _scrubberJSON.val = time;

            var display = new StringBuilder();
            foreach (var controller in _state.Controllers)
            {
                display.AppendLine($"Time: {time}s");
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