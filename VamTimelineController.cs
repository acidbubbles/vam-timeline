using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// VaM Timeline Controller
/// By Acidbubbles
/// Animation timeline with keyframes
/// Source: https://github.com/acidbubbles/vam-timeline
/// </summary>
public class VamTimelineController : MVRScript
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

    #region TODOs
    /*
    [ ] Select a keyframe
    [ ] When moving a gameobject in an existing keyframe, update the keyframe
    [ ] When moving a gameobject without keyframe, add the keyframe
    [ ] Scrubbing
    [ ] Undo / Redo
    [ ] Save the animation in another plugin for "backup"
    [ ] Loop
    [ ] Delete keyframe
    [ ] Move keyframes (all keyframes together)
    [ ] Display more information about animation
    [ ] Update Add/Remove buttons enabled when the target exists or not
    [ ] Choose whether to loop, ping pong, or play once
    [ ] Trigger on animation complete
    [ ] Autoplay on load
    [ ] Animate position, rotation separately (or check what the FreeControllerV3 allow instead of modifying it)
    [ ] Animate morphs
    [ ] Attach triggers to the animation (maybe sync with an animation pattern for scrubbing?)
    [ ] Animate any property
    [ ] Control speed
    [ ] Define animation transitions using built-in unity animation blending
    [ ] Copy/paste frame (all controllers in frame)
    [ ] OnUpdate is broken
    [ ] Save / Load / Backup is broken
    [ ] Next / Previous Frame not working
    [ ] Scrubber not updated on stop
    */
    #endregion

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

    private static readonly HashSet<string> GrabbingControllers = new HashSet<string> { "MouseGrab", "SelectionHandles" };

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
                    _grabbedController.SetKeyToCurrentPositionAndUpdate(_scrubberJSON.val);
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

    #region State

    public class State
    {
        public UnityEvent OnUpdated = new UnityEvent();
        public readonly List<ControllerState> Controllers = new List<ControllerState>();

        public State()
        {
        }

        public void Add(FreeControllerV3 controller)
        {
            if (Controllers.Any(c => c.Controller == controller)) return;
            ControllerState controllerState = new ControllerState(controller);
            Controllers.Add(controllerState);
            OnUpdated.Invoke();
        }

        public void Remove(FreeControllerV3 controller)
        {
            var existing = Controllers.FirstOrDefault(c => c.Controller == controller);
            if (existing != null)
            {
                Controllers.Remove(existing);
                OnUpdated.Invoke();
            }
        }

        internal void Play()
        {
            foreach (var controller in Controllers)
            {
                controller.Animation["test"].time = 0;
                controller.Animation.Play("test");
            }
        }

        internal void Stop()
        {
            foreach (var controller in Controllers)
            {
                controller.Animation.Stop("test");
            }

            SetTime(0);
        }

        public void SetTime(float time)
        {
            foreach (var controller in Controllers)
            {
                var animState = controller.Animation["test"];
                animState.time = time;
                if (!animState.enabled)
                {
                    // TODO: Can we set this once?
                    animState.enabled = true;
                    controller.Animation.Sample();
                    animState.enabled = false;
                }
            }
            
            OnUpdated.Invoke();
        }

        public void PauseToggle()
        {
            foreach (var controller in Controllers)
            {
                var animState = controller.Animation["test"];
                animState.enabled = !animState.enabled;
            }
        }

        public bool IsPlaying()
        {
            if (Controllers.Count == 0) return false;
            return Controllers[0].Animation.IsPlaying("test");
        }

        public float GetTime()
        {
            if (Controllers.Count == 0) return 0f;
            var animState = Controllers[0].Animation["test"];
            return animState.time % animState.length;
        }

        public void NextFrame()
        {
            var time = GetTime();
            // TODO: Hardcoded loop length
            var nextTime = 5f;
            foreach (var controller in Controllers)
            {
                var animState = controller.Animation["test"];
                var controllerNextTime = controller.X.keys.FirstOrDefault(k => k.time > time).time;
                SuperController.LogMessage($"Time: {time}, controllerNextTime: {controllerNextTime}");
                if (controllerNextTime != 0 && controllerNextTime < nextTime) nextTime = controllerNextTime;
            }
            SetTime(nextTime);
        }

        public void PreviousFrame()
        {
            var time = GetTime();
            var previousTime = 0f;
            foreach (var controller in Controllers)
            {
                var animState = controller.Animation["test"];
                var controllerNextTime = controller.X.keys.LastOrDefault(k => k.time < time).time;
                if (controllerNextTime != 0 && controllerNextTime > previousTime) previousTime = controllerNextTime;
            }
            SetTime(previousTime);
        }
    }

    public class ControllerState
    {
        public FreeControllerV3 Controller;
        public readonly Animation Animation;
        public readonly AnimationClip Clip;
        public AnimationCurve X = new AnimationCurve();
        public AnimationCurve Y = new AnimationCurve();
        public AnimationCurve Z = new AnimationCurve();
        public AnimationCurve RotX = new AnimationCurve();
        public AnimationCurve RotY = new AnimationCurve();
        public AnimationCurve RotZ = new AnimationCurve();
        public AnimationCurve RotW = new AnimationCurve();

        public ControllerState(FreeControllerV3 controller)
        {
            Controller = controller;
            // TODO: These should not be set internally, but rather by the initializer
            SetKey(0f, controller.transform.position, controller.transform.rotation);
            SetKey(5f, controller.transform.position, controller.transform.rotation);

            Clip = new AnimationClip();
            // TODO: Make that an option in the UI
            Clip.wrapMode = WrapMode.Loop;
            Clip.legacy = true;
            UpdateCurves();

            Animation = controller.gameObject.GetComponent<Animation>() ?? controller.gameObject.AddComponent<Animation>();
            Animation.AddClip(Clip, "test");
        }

        private void UpdateCurves()
        {
            Clip.SetCurve("", typeof(Transform), "localPosition.x", X);
            Clip.SetCurve("", typeof(Transform), "localPosition.y", Y);
            Clip.SetCurve("", typeof(Transform), "localPosition.z", Z);
            Clip.SetCurve("", typeof(Transform), "localRotation.x", RotX);
            Clip.SetCurve("", typeof(Transform), "localRotation.y", RotY);
            Clip.SetCurve("", typeof(Transform), "localRotation.z", RotZ);
            Clip.SetCurve("", typeof(Transform), "localRotation.w", RotW);
            Clip.EnsureQuaternionContinuity();
        }

        public void SetKeyToCurrentPositionAndUpdate(float time)
        {
            SetKey(time, Controller.transform.position, Controller.transform.rotation);
            // TODO: If the time is zero, also update the last frame!
            UpdateAnimation();
        }

        private void UpdateAnimation()
        {
            UpdateCurves();
            Animation.AddClip(Clip, "test");
        }

        public void SetKey(float time, Vector3 position, Quaternion rotation)
        {
            AddKey(X, time, position.x);
            AddKey(Y, time, position.y);
            AddKey(Z, time, position.z);
            AddKey(RotX, time, rotation.x);
            AddKey(RotY, time, rotation.y);
            AddKey(RotZ, time, rotation.z);
            AddKey(RotW, time, rotation.w);
        }

        private static void AddKey(AnimationCurve curve, float time, float value)
        {
            var key = curve.AddKey(time, value);
            if (key == -1)
            {
                // TODO: If this returns -1, it means the key was not added. Maybe use MoveKey?
                curve.RemoveKey(key);
                key = curve.AddKey(time, value);
            }
            var keyframe = curve.keys[key];
            // keyframe.weightedMode = WeightedMode.Both;
            // TODO: We should not set tangents on everything, they are fine by default.
            // TODO: This should only be set for first/last frames AND should use longer weight AND should copy last/first frame instead of using zero
            keyframe.inTangent = 0;
            keyframe.outTangent = 0;
            // keyframe.weightedMode = WeightedMode.Both;
            // curve.SmoothTangents(key, 0);
            curve.MoveKey(key, keyframe);
        }
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
            display.AppendLine($"  X");
            foreach (var keyframe in controller.X.keys)
                display.AppendLine($"    [{(keyframe.time == time ? "X" : "")}] {keyframe.time:0.00}s: {keyframe.value:0.00}");
        }
        _displayJSON.val = display.ToString();
    }

    #endregion
}