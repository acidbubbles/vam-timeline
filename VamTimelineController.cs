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
    private JSONStorableAction _playJSON;
    private JSONStorableString _displayJSON;
    private JSONStorableStringChooser _atomJSON;
    private JSONStorableStringChooser _controllerJSON;
    private FreeControllerV3 _selectedController;

    #region Lifecycle

    public override void Init()
    {
        try
        {
            _state = new State();

            _playJSON = new JSONStorableAction("Play", () => _state.Play());
            RegisterAction(_playJSON);
            CreateButton("Play").button.onClick.AddListener(() => _playJSON.actionCallback());

            _displayJSON = new JSONStorableString("TimelineDisplay", "");
            CreateTextField(_displayJSON);

            _atomJSON = new JSONStorableStringChooser("Target atom", SuperController.singleton.GetAtomUIDs(), "", "Target atom", uid => OnTargetAtomChanged(uid));
            _controllerJSON = new JSONStorableStringChooser("Target controller", new List<string>(), "", "Target controller", uid => OnTargetControllerChanged(uid));
            InitializeTargetSelection();

            _state.OnUpdated.AddListener(() => RenderState());
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
    }

    public class ControllerState
    {
        public FreeControllerV3 Controller;
        public readonly Animation Animation;
        public AnimationClip Clip = new AnimationClip();
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
            display.AppendLine($"{controller.Controller.containingAtom.name}:{controller.Controller.name}");
        _displayJSON.val = display.ToString();
    }

    #endregion
}