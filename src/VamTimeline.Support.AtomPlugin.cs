using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace AcidBubbles.VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class AtomPlugin : MVRScript
    {
        private const int MaxUndo = 20;
        private const string AllControllers = "(All Controllers)";
        private static readonly HashSet<string> GrabbingControllers = new HashSet<string> { "RightHandAnchor", "LeftHandAnchor", "MouseGrab", "SelectionHandles" };

        // State
        private Serializer _serializer;
        private AtomAnimation _animation;
        private FreeControllerV3Animation _grabbedController;
        private readonly List<string> _undoList = new List<string>();
        private List<ClipboardEntry> _clipboard;
        private bool _restoring = true;

        // Save
        private JSONStorableString _saveJSON;

        // Storables
        private JSONStorableStringChooser _animationJSON;
        private JSONStorableFloat _scrubberJSON;
        private JSONStorableAction _playJSON;
        private JSONStorableAction _stopJSON;
        private JSONStorableStringChooser _selectedControllerJSON;
        private JSONStorableAction _nextFrameJSON;
        private JSONStorableAction _previousFrameJSON;
        private JSONStorableStringChooser _changeCurveJSON;

        private JSONStorableBool _lockedJSON;
        private JSONStorableFloat _lengthJSON;
        private JSONStorableFloat _speedJSON;
        private JSONStorableFloat _blendDurationJSON;
        private JSONStorableStringChooser _addControllerListJSON;
        private JSONStorableStringChooser _linkedAnimationPatternJSON;
        private JSONStorableStringChooser _displayModeJSON;
        private JSONStorableString _displayJSON;

        // UI
        private UIDynamicButton _toggleControllerUI;

        #region Initialization

        public override void Init()
        {
            try
            {
                _serializer = new Serializer();
                InitStorables();
                InitCustomUI();
                // Try loading from backup
                StartCoroutine(CreateAnimationIfNoneIsLoaded());
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.AtomPlugin.Init: " + exc);
            }
        }

        private void InitStorables()
        {
            _saveJSON = new JSONStorableString(AtomPluginStorableNames.Save, "", (string v) => RestoreState(v));
            RegisterString(_saveJSON);

            // Left side

            _animationJSON = new JSONStorableStringChooser(AtomPluginStorableNames.Animation, new List<string>(), "Anim1", "Animation", val => ChangeAnimation(val))
            {
                isStorable = false
            };
            RegisterStringChooser(_animationJSON);

            _scrubberJSON = new JSONStorableFloat(AtomPluginStorableNames.Time, 0f, v => UpdateTime(v), 0f, 5f - float.Epsilon, true)
            {
                isStorable = false
            };
            RegisterFloat(_scrubberJSON);

            _playJSON = new JSONStorableAction(AtomPluginStorableNames.Play, () => { _animation.Play(); ContextUpdated(); });
            RegisterAction(_playJSON);

            _stopJSON = new JSONStorableAction(AtomPluginStorableNames.Stop, () => { _animation.Stop(); ContextUpdated(); });
            RegisterAction(_stopJSON);

            _selectedControllerJSON = new JSONStorableStringChooser(AtomPluginStorableNames.SelectedController, new List<string> { AllControllers }, AllControllers, "Selected Controller", val => { _animation.SelectControllerByName(val == AllControllers ? "" : val); ContextUpdated(); })
            {
                isStorable = false
            };
            RegisterStringChooser(_selectedControllerJSON);

            _nextFrameJSON = new JSONStorableAction(AtomPluginStorableNames.NextFrame, () => { UpdateTime(_animation.GetNextFrame()); ContextUpdated(); });
            RegisterAction(_nextFrameJSON);

            _previousFrameJSON = new JSONStorableAction(AtomPluginStorableNames.PreviousFrame, () => { UpdateTime(_animation.GetPreviousFrame()); ContextUpdated(); });
            RegisterAction(_previousFrameJSON);

            _changeCurveJSON = new JSONStorableStringChooser(AtomPluginStorableNames.ChangeCurve, CurveTypeValues.CurveTypes, "", "Change Curve", ChangeCurve);

            // Right side

            _lockedJSON = new JSONStorableBool(AtomPluginStorableNames.Locked, false, (bool val) => { ContextUpdated(); });
            RegisterBool(_lockedJSON);

            _lengthJSON = new JSONStorableFloat(AtomPluginStorableNames.AnimationLength, 5f, v => { if (v <= 0) return; _animation.AnimationLength = v; }, 0.5f, 120f, false, true);

            _speedJSON = new JSONStorableFloat(AtomPluginStorableNames.AnimationSpeed, 1f, v => { if (v < 0) return; _animation.Speed = v; }, 0.001f, 5f, false);

            _blendDurationJSON = new JSONStorableFloat(AtomPluginStorableNames.BlendDuration, 1f, v => _animation.BlendDuration = v, 0.001f, 5f, false);

            _addControllerListJSON = new JSONStorableStringChooser("Animate Controller", containingAtom.freeControllers.Select(fc => fc.name).ToList(), containingAtom.freeControllers.Select(fc => fc.name).FirstOrDefault(), "Animate controller", (string name) => UpdateToggleAnimatedControllerButton(name))
            {
                isStorable = false
            };

            _linkedAnimationPatternJSON = new JSONStorableStringChooser("Linked Animation Pattern", new[] { "" }.Concat(SuperController.singleton.GetAtoms().Where(a => a.type == "AnimationPattern").Select(a => a.uid)).ToList(), "", "Linked Animation Pattern", (string uid) => LinkAnimationPattern(uid))
            {
                isStorable = false
            };

            _displayModeJSON = new JSONStorableStringChooser(AtomPluginStorableNames.DisplayMode, RenderingModes.Values, RenderingModes.Default, "Display Mode", (string val) => { ContextUpdated(); });
            _displayJSON = new JSONStorableString(AtomPluginStorableNames.Display, "")
            {
                isStorable = false
            };
            RegisterString(_displayJSON);
        }

        private void InitCustomUI()
        {
            // Left side

            var animationUI = CreateScrollablePopup(_animationJSON, false);
            animationUI.popupPanelHeight = 800f;
            animationUI.popup.onOpenPopupHandlers += () => _animationJSON.choices = _animation.Clips.Select(c => c.AnimationName).ToList();

            CreateSlider(_scrubberJSON);

            var playUI = CreateButton("\u25B6 Play");
            playUI.button.onClick.AddListener(() => _playJSON.actionCallback());

            var stopUI = CreateButton("\u25A0 Stop");
            stopUI.button.onClick.AddListener(() => _stopJSON.actionCallback());

            var selectedControllerUI = CreateScrollablePopup(_selectedControllerJSON);
            selectedControllerUI.popupPanelHeight = 800f;

            var nextFrameUI = CreateButton("\u2192 Next Frame");
            nextFrameUI.button.onClick.AddListener(() => _nextFrameJSON.actionCallback());

            var previousFrameUI = CreateButton("\u2190 Previous Frame");
            previousFrameUI.button.onClick.AddListener(() => _previousFrameJSON.actionCallback());

            var changeCurveUI = CreatePopup(_changeCurveJSON, false);
            changeCurveUI.popupPanelHeight = 800f;

            var smoothAllFramesUI = CreateButton("Smooth All Frames", false);
            smoothAllFramesUI.button.onClick.AddListener(() => SmoothAllFrames());

            var cutUI = CreateButton("Cut / Delete Frame", false);
            cutUI.button.onClick.AddListener(() => Cut());

            var copyUI = CreateButton("Copy Frame", false);
            copyUI.button.onClick.AddListener(() => Copy());

            var pasteUI = CreateButton("Paste Frame", false);
            pasteUI.button.onClick.AddListener(() => Paste());

            var undoUI = CreateButton("Undo", false);
            // TODO: Right now it doesn't work for some reason...
            undoUI.button.interactable = false;
            undoUI.button.onClick.AddListener(() => Undo());

            // Right side

            var lockedUI = CreateToggle(_lockedJSON, true);
            lockedUI.label = "Locked (Performance Mode)";

            var addAnimationUI = CreateButton("Add New Animation", true);
            addAnimationUI.button.onClick.AddListener(() => AddAnimation());

            CreateSlider(_lengthJSON, true);

            CreateSlider(_speedJSON, true);

            CreateSlider(_blendDurationJSON, true);

            var addControllerUI = CreateScrollablePopup(_addControllerListJSON, true);
            addControllerUI.popupPanelHeight = 800f;

            _toggleControllerUI = CreateButton("Add/Remove Controller", true);
            _toggleControllerUI.button.onClick.AddListener(() => ToggleAnimatedController());

            var linkedAnimationPatternUI = CreateScrollablePopup(_linkedAnimationPatternJSON, true);
            linkedAnimationPatternUI.popupPanelHeight = 800f;
            linkedAnimationPatternUI.popup.onOpenPopupHandlers += () => _linkedAnimationPatternJSON.choices = new[] { "" }.Concat(SuperController.singleton.GetAtoms().Where(a => a.type == "AnimationPattern").Select(a => a.uid)).ToList();

            CreatePopup(_displayModeJSON, true);

            CreateTextField(_displayJSON, true);
        }

        private IEnumerator CreateAnimationIfNoneIsLoaded()
        {
            if (_animation != null) yield break;
            yield return new WaitForEndOfFrame();
            RestoreState("");
        }

        #endregion

        #region Lifecycle

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
                        _addControllerListJSON.val = grabbing.name;
                    }
                    else if (_grabbedController != null && grabbing == null)
                    {
                        // TODO: This should be done by the controller (updating the animation resets the time)
                        var time = _animation.Time;
                        _grabbedController.SetKeyframeToCurrentTransform(time);
                        _animation.RebuildAnimation();
                        UpdateTime(time);
                        _grabbedController = null;
                        RenderState();
                    }
                }
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.AtomPlugin.Update: " + exc);
            }
        }

        public void OnEnable()
        {
            try
            {
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.AtomPlugin.OnEnable: " + exc);
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
                SuperController.LogError("VamTimeline.AtomPlugin.OnDisable: " + exc);
            }
        }

        public void OnDestroy()
        {
            OnDisable();
        }

        #endregion

        #region Callbacks

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
                SuperController.LogError("VamTimeline.AtomPlugin.ChangeAnimation: " + exc);
            }
        }

        private void UpdateTime(float time)
        {
            _animation.Time = time;
            if (_animation.Current.AnimationPattern != null)
                _animation.Current.AnimationPattern.SetFloatParamValue("currentTime", time);
        }

        private void ChangeCurve(string val)
        {
            if (string.IsNullOrEmpty(val)) return;
            _changeCurveJSON.valNoCallback = "";
            if (_animation.Time == 0)
            {
                SuperController.LogMessage("Cannot specify curve type on frame 0");
                return;
            }
            _animation.ChangeCurve(val);
        }

        private void SmoothAllFrames()
        {
            _animation.SmoothAllFrames();
        }

        private void Cut()
        {
            _animation.DeleteFrame();
        }

        private class ClipboardEntry
        {
            public FreeControllerV3 Controller;
            public FreeControllerV3Snapshot Snapshot;
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
                SuperController.LogError("VamTimeline.AtomPlugin.Copy: " + exc);
            }
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
                UpdateTime(time);
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.AtomPlugin.Paste: " + exc);
            }
        }

        private void Undo()
        {
            if (_undoList.Count == 0) return;
            var pop = _undoList[_undoList.Count - 1];
            _undoList.RemoveAt(_undoList.Count - 1);
            RestoreState(pop);
            _saveJSON.valNoCallback = pop;
        }

        private void AddAnimation()
        {
            // TODO: Let the user name the animation
            var lastAnimationName = _animation.Clips.Last().AnimationName;
            var lastAnimationIndex = lastAnimationName.Substring(4);
            var animationName = "Anim" + (int.Parse(lastAnimationIndex) + 1);
            var clip = new AtomAnimationClip(animationName)
            {
                Speed = _animation.Speed,
                AnimationLength = _animation.AnimationLength
            };
            foreach (var controller in _animation.Current.Controllers.Select(c => c.Controller))
            {
                var animController = clip.Add(controller);
                animController.SetKeyframeToCurrentTransform(0f);
            }

            _animation.AddClip(clip);
            AnimationUpdated();
            ChangeAnimation(animationName);
        }

        private void UpdateToggleAnimatedControllerButton(string name)
        {
            var btnText = _toggleControllerUI.button.GetComponentInChildren<Text>();
            if (string.IsNullOrEmpty(name))
            {
                btnText.text = "Add/Remove Controller";
                _toggleControllerUI.button.interactable = false;
                return;
            }

            _toggleControllerUI.button.interactable = true;
            if (_animation.Current.Controllers.Any(c => c.Controller.name == name))
                btnText.text = "Remove Controller";
            else
                btnText.text = "Add Controller";
        }

        private void ToggleAnimatedController()
        {
            try
            {
                var uid = _addControllerListJSON.val;
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
                else
                {
                    controller.currentPositionState = FreeControllerV3.PositionState.On;
                    controller.currentRotationState = FreeControllerV3.RotationState.On;
                    var animController = _animation.Add(controller);
                    animController.SetKeyframeToCurrentTransform(0f);
                }
                AnimationUpdated();
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.AtomPlugin.AddSelectedController: " + exc);
            }
        }

        private void LinkAnimationPattern(string uid)
        {
            if (string.IsNullOrEmpty(uid))
            {
                _animation.Current.AnimationPattern = null;
                return;
            }
            var animationPattern = SuperController.singleton.GetAtomByUid(uid)?.GetComponentInChildren<AnimationPattern>();
            if (animationPattern == null)
            {
                SuperController.LogError($"Could not find Animation Pattern '{uid}'");
                return;
            }
            _animation.Current.AnimationPattern = animationPattern;
            animationPattern.SetBoolParamValue("autoPlay", false);
            animationPattern.SetBoolParamValue("pause", false);
            animationPattern.SetBoolParamValue("loop", false);
            animationPattern.SetBoolParamValue("loopOnce", false);
            animationPattern.SetFloatParamValue("speed", _animation.Speed);
            animationPattern.ResetAnimation();
            AnimationUpdated();
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
                    var backupStorableID = containingAtom.GetStorableIDs().FirstOrDefault(s => s.EndsWith("VamTimeline.BackupPlugin"));
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
                SuperController.LogError("VamTimeline.AtomPlugin.RestoreState: " + exc);
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
                SuperController.LogError("VamTimeline.AtomPlugin.RestoreState: " + exc);
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

                var backupStorableID = containingAtom.GetStorableIDs().FirstOrDefault(s => s.EndsWith("Backup"));
                if (backupStorableID != null)
                {
                    var backupStorable = containingAtom.GetStorableByID(backupStorableID);
                    var backupJSON = backupStorable.GetStringJSONParam("Backup");
                    backupJSON.val = serialized;
                }
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.AtomPlugin.SaveState: " + exc);
            }
        }

        #endregion

        #region State Rendering

        public class RenderingModes
        {
            public const string None = "None";
            public const string Default = "Default";
            public const string ShowAllControllers = "ShowAllControllers";
            public const string Debug = "Debug";

            public static readonly List<string> Values = new List<string> { None, Default, ShowAllControllers, Debug };
        }

        public void RenderState()
        {
            if (_lockedJSON.val)
            {
                _displayJSON.val = "Locked";
                return;
            }

            var time = _animation.Time;

            switch (_displayModeJSON.val)
            {
                case RenderingModes.None:
                    _displayJSON.val = "";
                    break;
                case RenderingModes.Default:
                    RenderStateDefault();
                    break;
                case RenderingModes.ShowAllControllers:
                    RenderStateShowAllControllers();
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
            var frames = new List<float>();
            var controllers = new List<string>();
            foreach (var controller in _animation.GetAllOrSelectedControllers())
            {
                var keyTimes = controller.Curves.SelectMany(c => c.keys.Take(c.keys.Length - 1)).Select(k => k.time).Distinct();
                foreach (var keyTime in keyTimes)
                {
                    frames.Add(keyTime);
                    if (keyTime == time)
                        controllers.Add($"{controller.Controller.containingAtom.name}:{controller.Controller.name}");
                }
            }
            var display = new StringBuilder();
            frames.Sort();
            display.Append("Frames:");
            foreach (var f in frames.Distinct())
            {
                if (f == time)
                    display.Append($"[{f:0.00}]");
                else
                    display.Append($" {f:0.00} ");
            }
            display.AppendLine();
            display.AppendLine("Affects:");
            foreach (var c in controllers)
                display.AppendLine(c);
            _displayJSON.val = display.ToString();
        }

        public void RenderStateShowAllControllers()
        {
            var time = _scrubberJSON.val;
            var display = new StringBuilder();
            foreach (var controller in _animation.GetAllOrSelectedControllers())
            {
                display.AppendLine($"{controller.Controller.containingAtom.name}:{controller.Controller.name}");
                var keyTimes = controller.Curves.SelectMany(c => c.keys.Take(c.keys.Length - 1)).Select(k => k.time).Distinct();
                foreach (var keyTime in keyTimes)
                {
                    display.Append($"{(keyTime == time ? "[" : " ")}{keyTime:0.0000}{(keyTime == time ? "]" : " ")}");
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
                _scrubberJSON.valNoCallback = _animation.Time;
                _animationJSON.choices = _animation.Clips.Select(c => c.AnimationName).ToList();
                _lengthJSON.valNoCallback = _animation.AnimationLength;
                _speedJSON.valNoCallback = _animation.Speed;
                _lengthJSON.valNoCallback = _animation.AnimationLength;
                _blendDurationJSON.valNoCallback = _animation.BlendDuration;
                _scrubberJSON.max = _animation.AnimationLength - float.Epsilon;
                _selectedControllerJSON.choices = new List<string> { AllControllers }.Concat(_animation.GetControllersName()).ToList();
                _linkedAnimationPatternJSON.valNoCallback = _animation.Current.AnimationPattern?.containingAtom.uid ?? "";

                UpdateToggleAnimatedControllerButton(_addControllerListJSON.val);

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
                SuperController.LogError("VamTimeline.AtomPlugin.AnimationUpdated: " + exc);
            }
        }

        private void ContextUpdated()
        {
            try
            {
                var time = _animation.Time;

                // Update UI
                _scrubberJSON.valNoCallback = time;

                // Dispatch to VamTimelineController
                var externalControllers = SuperController.singleton.GetAtoms().Where(a => a.type == "SimpleSign");
                foreach (var controller in externalControllers)
                    controller.BroadcastMessage("VamTimelineContextChanged", containingAtom.uid);

                // Render
                RenderState();
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.AtomPlugin.ContextUpdated: " + exc);
            }
        }

        #endregion
    }
}
