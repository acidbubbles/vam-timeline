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
    private JSONStorableBool _lockedJSON;
    private JSONStorableAction _playJSON;
    private JSONStorableAction _stopJSON;
    private JSONStorableString _displayJSON;
    private JSONStorableStringChooser _atomJSON;
    private JSONStorableStringChooser _controllerJSON;
    private FreeControllerV3 _selectedController;
    private JSONStorableString _saveJSON;

    #region TODOs
    /*
    [ ] Select a keyframe
    [ ] When moving a gameobject in an existing keyframe, update the keyframe
    [ ] When moving a gameobject without keyframe, add the keyframe
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
    */
    #endregion

    #region Lifecycle

    public override void Init()
    {
        try
        {
            _state = new State();

            _lockedJSON = new JSONStorableBool("Locked", false);
            RegisterBool(_lockedJSON);

            _playJSON = new JSONStorableAction("Play", () => _state.Play());
            RegisterAction(_playJSON);
            CreateButton("Play").button.onClick.AddListener(() => _playJSON.actionCallback());

            _stopJSON = new JSONStorableAction("Stop", () => _state.Stop());
            RegisterAction(_stopJSON);
            CreateButton("Stop").button.onClick.AddListener(() => _stopJSON.actionCallback());

            _displayJSON = new JSONStorableString("TimelineDisplay", "");
            CreateTextField(_displayJSON);

            _atomJSON = new JSONStorableStringChooser("Target atom", SuperController.singleton.GetAtomUIDs(), "", "Target atom", uid => OnTargetAtomChanged(uid));
            _controllerJSON = new JSONStorableStringChooser("Target controller", new List<string>(), "", "Target controller", uid => OnTargetControllerChanged(uid));
            InitializeTargetSelection();

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

    public void Update()
    {
        try
        {
            if (_lockedJSON.val) return;

            // NOTE: If we had access to SuperController.instance.rightGrabbedController (and left) and grabbedControllerMouse we would not have to scan the controllers.
            if (SuperController.singleton.GetRightGrab() || SuperController.singleton.GetLeftGrab() || Input.GetMouseButton(0))
            {
                // SuperController.LogMessage(_state.Controllers.FirstOrDefault()?.Controller.linkToRB?.gameObject.name);
                var grabbedController = _state.Controllers.FirstOrDefault(c => c.Controller.linkToRB?.gameObject.name == "MouseGrab");
                if (grabbedController == null) return;
                SuperController.LogMessage("Update!");

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

    private State DeserializeState(string val)
    {
        SuperController.LogMessage("Deserializing: " + val);
        return new State();
    }

    public void SaveState()
    {
        var serialized = "";
        _saveJSON.val = serialized;

        var backupJSON = containingAtom.GetStorableByID("VamTimelineBackup")?.GetStringJSONParam("Backup");

        if (backupJSON != null)
            backupJSON.val = serialized;
    }

    #endregion

    #region Target Selection

    private void InitializeTargetSelection()
    {
        var atomPopup = CreateScrollablePopup(_atomJSON, true);
        atomPopup.popupPanelHeight = 800f;

        var controllerPopup = CreateScrollablePopup(_controllerJSON, true);
        controllerPopup.popupPanelHeight = 800f;

        SuperController.singleton.onAtomUIDsChangedHandlers += (uids) => OnAtomsChanged(uids);
        OnAtomsChanged(SuperController.singleton.GetAtomUIDs());

        CreateButton("Add", true).button.onClick.AddListener(() => AddSelectedController());
        CreateButton("Remove", true).button.onClick.AddListener(() => RemoveSelectedController());
    }

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
                controller.Animation.Play("test");
        }

        internal void Stop()
        {
            foreach (var controller in Controllers)
                controller.Animation.Stop("test");
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
            AddKey(0, controller.transform.position, controller.transform.rotation);
            AddKey(1f, controller.transform.position + Vector3.right * UnityEngine.Random.Range(-0.2f, 0.2f), Quaternion.Euler(0, 0, UnityEngine.Random.Range(-15f, 15f)));
            AddKey(2f, controller.transform.position + Vector3.left * UnityEngine.Random.Range(-0.2f, 0.2f), Quaternion.Euler(0, 0, UnityEngine.Random.Range(-15f, 15f)));
            AddKey(3f, controller.transform.position, controller.transform.rotation);

            Clip = new AnimationClip();
            // TODO: Make that an option in the UI
            Clip.wrapMode = WrapMode.Loop;
            Clip.legacy = true;
            Clip.SetCurve("", typeof(Transform), "localPosition.x", X);
            Clip.SetCurve("", typeof(Transform), "localPosition.y", Y);
            Clip.SetCurve("", typeof(Transform), "localPosition.z", Z);
            Clip.SetCurve("", typeof(Transform), "localRotation.x", RotX);
            Clip.SetCurve("", typeof(Transform), "localRotation.y", RotY);
            Clip.SetCurve("", typeof(Transform), "localRotation.z", RotZ);
            Clip.SetCurve("", typeof(Transform), "localRotation.w", RotW);
            Clip.EnsureQuaternionContinuity();

            Animation = controller.gameObject.GetComponent<Animation>() ?? controller.gameObject.AddComponent<Animation>();
            Animation.AddClip(Clip, "test");
        }

        private void AddKey(float time, Vector3 position, Quaternion rotation)
        {
            X.AddKey(time, position.x);
            Y.AddKey(time, position.y);
            Z.AddKey(time, position.z);
            RotX.AddKey(time, rotation.x);
            RotY.AddKey(time, rotation.y);
            RotZ.AddKey(time, rotation.z);
            RotW.AddKey(time, rotation.w);
        }
    }

    #endregion

    #region State Rendering

    public void RenderState()
    {
        var display = new StringBuilder();
        foreach (var controller in _state.Controllers)
        {
            display.AppendLine($"{controller.Controller.containingAtom.name}:{controller.Controller.name}");
            display.AppendLine($"  X: {string.Join(", ", controller.X.keys.Select(k => k.time.ToString("0.00")).ToArray())}");
        }
        _displayJSON.val = display.ToString();
    }

    #endregion
}