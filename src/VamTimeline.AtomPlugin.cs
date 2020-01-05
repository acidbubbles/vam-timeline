using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class AtomPlugin : MVRScript, IAtomPlugin
    {
        private static readonly HashSet<string> GrabbingControllers = new HashSet<string> { "RightHandAnchor", "LeftHandAnchor", "MouseGrab", "SelectionHandles" };

        // State
        private FreeControllerAnimationTarget _grabbedController;

        // Storables
        private JSONStorableStringChooser _changeCurveJSON;

        private JSONStorableStringChooser _addControllerListJSON;
        private JSONStorableStringChooser _linkedAnimationPatternJSON;

        // UI
        private UIDynamicButton _toggleControllerUI;

        private const int MaxUndo = 20;
        private const string AllTargets = "(All)";
        private bool _saveEnabled;

        // State
        private AtomAnimationSerializer _serializer;
        protected AtomAnimation _animation;
        private bool _restoring;
        private readonly List<string> _undoList = new List<string>();
        private AtomClipboardEntry _clipboard;

        // Save
        private JSONStorableString _saveJSON;

        // Storables
        private JSONStorableStringChooser _animationJSON;
        private JSONStorableFloat _scrubberJSON;
        private JSONStorableAction _playJSON;
        private JSONStorableAction _playIfNotPlayingJSON;
        private JSONStorableAction _stopJSON;
        private JSONStorableStringChooser _filterAnimationTargetJSON;
        private JSONStorableAction _nextFrameJSON;
        private JSONStorableAction _previousFrameJSON;

        private JSONStorableBool _lockedJSON;
        private JSONStorableFloat _lengthJSON;
        private JSONStorableFloat _speedJSON;
        private JSONStorableFloat _blendDurationJSON;
        private JSONStorableStringChooser _displayModeJSON;
        private JSONStorableString _displayJSON;

        // UI
        private UIDynamicButton _undoUI;

        #region Init

        public override void Init()
        {
            try
            {
                _serializer = new AtomAnimationSerializer(containingAtom);
                InitStorables();
                InitFloatParamsStorables();
                InitCustomUI();
                InitFloatParamsCustomUI();
                // Try loading from backup
                StartCoroutine(CreateAnimationIfNoneIsLoaded());
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.AtomPlugin.Init: " + exc);
            }
        }

        #endregion

        #region Update

        public void Update()
        {
            try
            {
                if (_lockedJSON == null || _lockedJSON.val || _animation == null) return;

                if (_animation.IsPlaying())
                {
                    var time = _animation.Time;
                    if (time != _scrubberJSON.val)
                        _scrubberJSON.valNoCallback = time;
                    UpdatePlaying();
                    // RenderState() // In practice, we don't see anything useful
                }
                else
                {
                    UpdateNotPlaying();
                }
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.AtomPlugin.Update: " + exc);
            }
        }

        protected void UpdatePlaying()
        {
            _animation.Update();

            if (!_lockedJSON.val)
                ContextUpdatedCustom();
        }

        protected void UpdateNotPlaying()
        {
            var grabbing = SuperController.singleton.RightGrabbedController ?? SuperController.singleton.LeftGrabbedController;
            if (grabbing != null && grabbing.containingAtom != containingAtom)
                grabbing = null;
            else if (Input.GetMouseButton(0) && grabbing == null)
                grabbing = containingAtom.freeControllers.FirstOrDefault(c => GrabbingControllers.Contains(c.linkToRB?.gameObject.name));

            if (_grabbedController == null && grabbing != null)
            {
                _grabbedController = _animation.Current.TargetControllers.FirstOrDefault(c => c.Controller == grabbing);
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
                AnimationUpdated();
            }
        }

        #endregion

        #region Lifecycle

        public void OnEnable()
        {
            try
            {
                // TODO
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


        #region Initialization

        public void InitCommonStorables()
        {
            _saveJSON = new JSONStorableString(StorableNames.Save, "", (string v) => RestoreState(v));
            RegisterString(_saveJSON);

            _animationJSON = new JSONStorableStringChooser(StorableNames.Animation, new List<string>(), "Anim1", "Animation", val => ChangeAnimation(val))
            {
                isStorable = false
            };
            RegisterStringChooser(_animationJSON);

            _scrubberJSON = new JSONStorableFloat(StorableNames.Time, 0f, v => UpdateTime(v), 0f, 5f - float.Epsilon, true)
            {
                isStorable = false
            };
            RegisterFloat(_scrubberJSON);

            _playJSON = new JSONStorableAction(StorableNames.Play, () => { _animation.Play(); ContextUpdated(); });
            RegisterAction(_playJSON);

            _playIfNotPlayingJSON = new JSONStorableAction(StorableNames.PlayIfNotPlaying, () => { if (!_animation.IsPlaying()) { _animation.Play(); ContextUpdated(); } });
            RegisterAction(_playIfNotPlayingJSON);

            _stopJSON = new JSONStorableAction(StorableNames.Stop, () => { _animation.Stop(); ContextUpdated(); });
            RegisterAction(_stopJSON);

            _filterAnimationTargetJSON = new JSONStorableStringChooser(StorableNames.FilterAnimationTarget, new List<string> { AllTargets }, AllTargets, StorableNames.FilterAnimationTarget, val => { _animation.Current.SelectTargetByName(val == AllTargets ? "" : val); ContextUpdated(); })
            {
                isStorable = false
            };
            RegisterStringChooser(_filterAnimationTargetJSON);

            _nextFrameJSON = new JSONStorableAction(StorableNames.NextFrame, () => { UpdateTime(_animation.Current.GetNextFrame(_animation.Time)); ContextUpdated(); });
            RegisterAction(_nextFrameJSON);

            _previousFrameJSON = new JSONStorableAction(StorableNames.PreviousFrame, () => { UpdateTime(_animation.Current.GetPreviousFrame(_animation.Time)); ContextUpdated(); });
            RegisterAction(_previousFrameJSON);

            _lockedJSON = new JSONStorableBool(StorableNames.Locked, false, (bool val) => ContextUpdated());
            RegisterBool(_lockedJSON);

            _lengthJSON = new JSONStorableFloat(StorableNames.AnimationLength, 5f, v => UpdateAnimationLength(v), 0.5f, 120f, false, true);

            _speedJSON = new JSONStorableFloat(StorableNames.AnimationSpeed, 1f, v => UpdateAnimationSpeed(v), 0.001f, 5f, false);

            _blendDurationJSON = new JSONStorableFloat(StorableNames.BlendDuration, 1f, v => UpdateBlendDuration(v), 0.001f, 5f, false);

            _displayModeJSON = new JSONStorableStringChooser(StorableNames.DisplayMode, RenderingModes.Values, RenderingModes.Default, "Display Mode", (string val) => { ContextUpdated(); });
            _displayJSON = new JSONStorableString(StorableNames.Display, "")
            {
                isStorable = false
            };
            RegisterString(_displayJSON);
        }

        protected void InitPlaybackUI(bool rightSide)
        {
            var animationUI = CreateScrollablePopup(_animationJSON, rightSide);
            animationUI.popupPanelHeight = 800f;
            animationUI.popup.onOpenPopupHandlers += () => _animationJSON.choices = _animation.GetAnimationNames().ToList();

            CreateSlider(_scrubberJSON);

            var playUI = CreateButton("\u25B6 Play", rightSide);
            playUI.button.onClick.AddListener(() => _playJSON.actionCallback());

            var stopUI = CreateButton("\u25A0 Stop", rightSide);
            stopUI.button.onClick.AddListener(() => _stopJSON.actionCallback());
        }

        protected void InitFrameNavUI(bool rightSide)
        {
            var selectedControllerUI = CreateScrollablePopup(_filterAnimationTargetJSON, rightSide);
            selectedControllerUI.popupPanelHeight = 800f;

            var nextFrameUI = CreateButton("\u2192 Next Frame", rightSide);
            nextFrameUI.button.onClick.AddListener(() => _nextFrameJSON.actionCallback());

            var previousFrameUI = CreateButton("\u2190 Previous Frame", rightSide);
            previousFrameUI.button.onClick.AddListener(() => _previousFrameJSON.actionCallback());

        }

        protected void InitClipboardUI(bool rightSide)
        {
            var cutUI = CreateButton("Cut / Delete Frame", rightSide);
            cutUI.button.onClick.AddListener(() => Cut());

            var copyUI = CreateButton("Copy Frame", rightSide);
            copyUI.button.onClick.AddListener(() => Copy());

            var pasteUI = CreateButton("Paste Frame", rightSide);
            pasteUI.button.onClick.AddListener(() => Paste());

            _undoUI = CreateButton("Undo", rightSide);
            _undoUI.button.interactable = false;
            _undoUI.button.onClick.AddListener(() => Undo());
        }

        protected void InitAnimationSettingsUI(bool rightSide)
        {
            var lockedUI = CreateToggle(_lockedJSON, rightSide);
            lockedUI.label = "Locked (Performance Mode)";

            var addAnimationUI = CreateButton("Add New Animation", rightSide);
            addAnimationUI.button.onClick.AddListener(() => AddAnimation());

            CreateSlider(_lengthJSON, rightSide);

            CreateSlider(_speedJSON, rightSide);

            CreateSlider(_blendDurationJSON, rightSide);
        }

        protected void InitDisplayUI(bool rightSide)
        {
            CreatePopup(_displayModeJSON, rightSide);

            CreateTextField(_displayJSON, rightSide);
        }

        protected IEnumerator CreateAnimationIfNoneIsLoaded()
        {
            if (_animation != null)
            {
                _saveEnabled = true;
                yield break;
            }
            yield return new WaitForEndOfFrame();
            try
            {
                RestoreState(_saveJSON.val);
            }
            finally
            {
                _saveEnabled = true;
            }
        }

        #endregion

        #region Load / Save

        public void RestoreState(string json)
        {
            if (_restoring) return;
            _restoring = true;

            try
            {
                if (_animation != null)
                    _animation = null;

                if (!string.IsNullOrEmpty(json))
                {
                    _animation = _serializer.DeserializeAnimation(json);
                }

                if (_animation == null)
                {
                    var backupStorableID = containingAtom.GetStorableIDs().FirstOrDefault(s => s.EndsWith("VamTimeline.BackupPlugin"));
                    if (backupStorableID != null)
                    {
                        var backupStorable = containingAtom.GetStorableByID(backupStorableID);
                        var backupJSON = backupStorable.GetStringJSONParam(StorableNames.AtomAnimationBackup);
                        if (backupJSON != null && !string.IsNullOrEmpty(backupJSON.val))
                        {
                            SuperController.LogMessage("No save found but a backup was detected. Loading backup.");
                            _animation = _serializer.DeserializeAnimation(backupJSON.val);
                        }
                    }
                }

            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.PluginImplBase.RestoreState(1): " + exc);
            }

            try
            {
                if (_animation == null)
                    _animation = _serializer.CreateDefaultAnimation();

                _animation.Initialize();
                StateRestored();
                AnimationUpdated();
                ContextUpdated();
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.PluginImplBase.RestoreState(2): " + exc);
            }

            _restoring = false;
        }

        public void SaveState()
        {
            try
            {
                if (_restoring) return;
                if (_animation.IsEmpty()) return;

                var serialized = _serializer.SerializeAnimation(_animation);

                if (serialized == _undoList.LastOrDefault())
                    return;

                if (!string.IsNullOrEmpty(_saveJSON.val))
                {
                    _undoList.Add(_saveJSON.val);
                    if (_undoList.Count > MaxUndo) _undoList.RemoveAt(0);
                    _undoUI.button.interactable = true;
                }

                _saveJSON.valNoCallback = serialized;

                var backupStorableID = containingAtom.GetStorableIDs().FirstOrDefault(s => s.EndsWith("VamTimeline.BackupPlugin"));
                if (backupStorableID != null)
                {
                    var backupStorable = containingAtom.GetStorableByID(backupStorableID);
                    var backupJSON = backupStorable.GetStringJSONParam(StorableNames.AtomAnimationBackup);
                    if (backupJSON != null)
                        backupJSON.val = serialized;
                }
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.PluginImplBase.SaveState: " + exc);
            }
        }

        #endregion

        #region Callbacks

        private void ChangeAnimation(string animationName)
        {
            try
            {
                _filterAnimationTargetJSON.val = AllTargets;
                _animation.ChangeAnimation(animationName);
                _animationJSON.valNoCallback = animationName;
                _speedJSON.valNoCallback = _animation.Speed;
                _lengthJSON.valNoCallback = _animation.AnimationLength;
                _scrubberJSON.max = _animation.AnimationLength - float.Epsilon;
                AnimationUpdated();
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.AtomPlugin.ChangeAnimation: " + exc);
            }
        }

        protected void UpdateTime(float time)
        {
            _animation.Time = time;
            if (_animation.Current.AnimationPattern != null)
                _animation.Current.AnimationPattern.SetFloatParamValue("currentTime", time);
            ContextUpdated();
        }

        private void UpdateAnimationLength(float v)
        {
            if (v <= 0) return;
            _animation.AnimationLength = v;
            AnimationUpdated();
        }

        private void UpdateAnimationSpeed(float v)
        {
            if (v < 0) return;
            _animation.Speed = v;
            AnimationUpdated();
        }

        private void UpdateBlendDuration(float v)
        {
            if (v < 0) return;
            _animation.BlendDuration = v;
            AnimationUpdated();
        }

        private void Cut()
        {
            Copy();
            if (_animation.Time == 0f) return;
            _animation.DeleteFrame();
        }

        private void Copy()
        {
            try
            {
                _clipboard = _animation.Copy();
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
                _animation.Paste(_clipboard);
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
            var animationName = _animationJSON.val;
            var pop = _undoList[_undoList.Count - 1];
            _undoList.RemoveAt(_undoList.Count - 1);
            if (_undoList.Count == 0) _undoUI.button.interactable = false;
            if (string.IsNullOrEmpty(pop)) return;
            var time = _animation.Time;
            _saveEnabled = false;
            try
            {
                RestoreState(pop);
                _saveJSON.valNoCallback = pop;
                if (_animation.Clips.Any(c => c.AnimationName == animationName))
                    _animationJSON.val = animationName;
                else
                    _animationJSON.valNoCallback = _animation.Clips.First().AnimationName;
                AnimationUpdated();
                UpdateTime(time);
            }
            finally
            {
                _saveEnabled = true;
            }
        }

        private void AddAnimation()
        {
            _saveEnabled = false;
            try
            {
                var animationName = _animation.AddAnimation();
                AnimationUpdated();
                ChangeAnimation(animationName);
            }
            finally
            {
                _saveEnabled = true;
            }
            SaveState();
        }

        private void ChangeCurve(string curveType)
        {
            if (string.IsNullOrEmpty(curveType)) return;
            _changeCurveJSON.valNoCallback = "";
            if (_animation.Time == 0)
            {
                SuperController.LogMessage("Cannot specify curve type on frame 0");
                return;
            }
            _animation.ChangeCurve(curveType);
        }

        private void SmoothAllFrames()
        {
            _animation.SmoothAllFrames();
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
            if (_animation.Current.TargetControllers.Any(c => c.Controller.name == name))
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
                if (_animation.Current.TargetControllers.Any(c => c.Controller == controller))
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

        #region State Rendering

        public class RenderingModes
        {
            public const string None = "None";
            public const string Default = "Default";
            public const string ShowAllTargets = "Show All Targets";
            public const string Debug = "Debug";

            public static readonly List<string> Values = new List<string> { None, Default, ShowAllTargets, Debug };
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
                case RenderingModes.ShowAllTargets:
                    RenderStateShowAllTargets();
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
            var targets = new List<string>();
            foreach (var target in _animation.Current.GetAllOrSelectedTargets())
            {
                var keyTimes = target.GetAllKeyframesTime();
                foreach (var keyTime in keyTimes)
                {
                    frames.Add(keyTime);
                    if (keyTime == time)
                        targets.Add(target.Name);
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
            foreach (var c in targets)
                display.AppendLine(c);
            _displayJSON.val = display.ToString();
        }

        public void RenderStateShowAllTargets()
        {
            var time = _scrubberJSON.val;
            var display = new StringBuilder();
            foreach (var controller in _animation.Current.GetAllOrSelectedTargets())
            {
                display.AppendLine(controller.Name);
                var keyTimes = controller.GetAllKeyframesTime();
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
            // Instead make a debug screen
            var time = _scrubberJSON.val;
            var display = new StringBuilder();
            display.AppendLine($"Time: {time}s");
            foreach (var controller in _animation.Current.GetAllOrSelectedTargets())
            {
                controller.RenderDebugInfo(display, time);
            }
            _displayJSON.val = display.ToString();
        }

        #endregion

        #region Updates

        protected void AnimationUpdated()
        {
            try
            {
                // Update UI
                _scrubberJSON.valNoCallback = _animation.Time;
                _animationJSON.choices = _animation.GetAnimationNames().ToList();
                _lengthJSON.valNoCallback = _animation.AnimationLength;
                _speedJSON.valNoCallback = _animation.Speed;
                _lengthJSON.valNoCallback = _animation.AnimationLength;
                _blendDurationJSON.valNoCallback = _animation.BlendDuration;
                _scrubberJSON.max = _animation.AnimationLength - float.Epsilon;
                _filterAnimationTargetJSON.choices = new List<string> { AllTargets }.Concat(_animation.Current.GetTargetsNames()).ToList();

                _linkedAnimationPatternJSON.valNoCallback = _animation.Current.AnimationPattern?.containingAtom.uid ?? "";

                UpdateToggleAnimatedControllerButton(_addControllerListJSON.val);

                // Save
                if (_saveEnabled)
                    SaveState();

                // Render
                RenderState();

                // Dispatch to VamTimelineController
                var externalControllers = SuperController.singleton.GetAtoms().Where(a => a.type == "SimpleSign");
                foreach (var controller in externalControllers)
                    controller.BroadcastMessage("VamTimelineAnimationUpdated", containingAtom.uid);
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.AtomPlugin.AnimationUpdated: " + exc);
            }
        }

        protected void ContextUpdated()
        {
            try
            {
                var time = _animation.Time;

                // Update UI
                _scrubberJSON.valNoCallback = time;

                ContextUpdatedCustom();

                // Render
                RenderState();

                // Dispatch to VamTimelineController
                var externalControllers = SuperController.singleton.GetAtoms().Where(a => a.type == "SimpleSign");
                foreach (var controller in externalControllers)
                    controller.BroadcastMessage("VamTimelineContextChanged", containingAtom.uid);
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.AtomPlugin.ContextUpdated: " + exc);
            }
        }

        #endregion

        // Shared
        #region Initialization

        private void InitStorables()
        {
            InitCommonStorables();

            _changeCurveJSON = new JSONStorableStringChooser(StorableNames.ChangeCurve, CurveTypeValues.CurveTypes, "", "Change Curve", ChangeCurve);

            _addControllerListJSON = new JSONStorableStringChooser("Animate Controller", containingAtom.freeControllers.Select(fc => fc.name).ToList(), containingAtom.freeControllers.Select(fc => fc.name).FirstOrDefault(), "Animate controller", (string name) => UpdateToggleAnimatedControllerButton(name))
            {
                isStorable = false
            };

            _linkedAnimationPatternJSON = new JSONStorableStringChooser("Linked Animation Pattern", new[] { "" }.Concat(SuperController.singleton.GetAtoms().Where(a => a.type == "AnimationPattern").Select(a => a.uid)).ToList(), "", "Linked Animation Pattern", (string uid) => LinkAnimationPattern(uid))
            {
                isStorable = false
            };
        }

        private void InitCustomUI()
        {
            // Left side

            InitPlaybackUI(false);

            InitFrameNavUI(false);

            var changeCurveUI = CreatePopup(_changeCurveJSON, false);
            changeCurveUI.popupPanelHeight = 800f;

            var smoothAllFramesUI = CreateButton("Smooth All Frames", false);
            smoothAllFramesUI.button.onClick.AddListener(() => SmoothAllFrames());

            InitClipboardUI(false);

            // Right side

            InitAnimationSettingsUI(true);

            var addControllerUI = CreateScrollablePopup(_addControllerListJSON, true);
            addControllerUI.popupPanelHeight = 800f;

            _toggleControllerUI = CreateButton("Add/Remove Controller", true);
            _toggleControllerUI.button.onClick.AddListener(() => ToggleAnimatedController());

            var linkedAnimationPatternUI = CreateScrollablePopup(_linkedAnimationPatternJSON, true);
            linkedAnimationPatternUI.popupPanelHeight = 800f;
            linkedAnimationPatternUI.popup.onOpenPopupHandlers += () => _linkedAnimationPatternJSON.choices = new[] { "" }.Concat(SuperController.singleton.GetAtoms().Where(a => a.type == "AnimationPattern").Select(a => a.uid)).ToList();

            InitDisplayUI(true);
        }

        #endregion

        private class FloatParamJSONRef
        {
            public JSONStorable Storable;
            public JSONStorableFloat SourceFloatParam;
            public JSONStorableFloat Proxy;
            public UIDynamicSlider Slider;
        }

        private List<FloatParamJSONRef> _jsfJSONRefs;

        // Storables
        private JSONStorableStringChooser _addStorableListJSON;
        private JSONStorableStringChooser _addParamListJSON;

        // UI
        private UIDynamicButton _toggleFloatParamUI;

        #region Initialization

        private void InitFloatParamsStorables()
        {
            InitCommonStorables();

            var storables = GetInterestingStorableIDs().ToList();
            _addStorableListJSON = new JSONStorableStringChooser("Animate Storable", storables, storables.Contains("geometry") ? "geometry" : storables.FirstOrDefault(), "Animate Storable", (string name) => RefreshStorableFloatsList())
            {
                isStorable = false
            };

            _addParamListJSON = new JSONStorableStringChooser("Animate Param", new List<string>(), "", "Animate Param", (string name) => UpdateToggleAnimatedFloatParamButton(name))
            {
                isStorable = false
            };

            RefreshStorableFloatsList();
        }

        private IEnumerable<string> GetInterestingStorableIDs()
        {
            foreach (var storableId in containingAtom.GetStorableIDs())
            {
                var storable = containingAtom.GetStorableByID(storableId);
                if (storable.GetFloatParamNames().Count > 0)
                    yield return storableId;
            }
        }

        private void RefreshStorableFloatsList()
        {
            if (string.IsNullOrEmpty(_addStorableListJSON.val))
            {
                _addParamListJSON.choices = new List<string>();
                _addParamListJSON.val = "";
                return;
            }
            var values = containingAtom.GetStorableByID(_addStorableListJSON.val)?.GetFloatParamNames() ?? new List<string>();
            _addParamListJSON.choices = values;
            if (!values.Contains(_addParamListJSON.val))
                _addParamListJSON.val = values.FirstOrDefault();
        }

        private void InitFloatParamsCustomUI()
        {
            var addFloatParamListUI = CreateScrollablePopup(_addStorableListJSON, true);
            addFloatParamListUI.popupPanelHeight = 800f;
            addFloatParamListUI.popup.onOpenPopupHandlers += () => _addStorableListJSON.choices = GetInterestingStorableIDs().ToList();

            var addParamListUI = CreateScrollablePopup(_addParamListJSON, true);
            addParamListUI.popupPanelHeight = 700f;
            addParamListUI.popup.onOpenPopupHandlers += () => RefreshStorableFloatsList();

            _toggleFloatParamUI = CreateButton("Add/Remove Param", true);
            _toggleFloatParamUI.button.onClick.AddListener(() => ToggleAnimatedFloatParam());

            RefreshFloatParamsListUI();
        }

        private void RefreshFloatParamsListUI()
        {
            if (_jsfJSONRefs != null)
            {
                foreach (var jsfJSONRef in _jsfJSONRefs)
                {
                    RemoveSlider(jsfJSONRef.Slider);
                }
            }
            if (_animation == null) return;
            // TODO: This is expensive, though rarely occuring
            _jsfJSONRefs = new List<FloatParamJSONRef>();
            foreach (var target in _animation.Current.TargetFloatParams)
            {
                var jsfJSONRef = target.FloatParam;
                var jsfJSONProxy = new JSONStorableFloat($"{target.Storable.name}/{jsfJSONRef.name}", jsfJSONRef.defaultVal, (float val) => UpdateFloatParam(target, jsfJSONRef, val), jsfJSONRef.min, jsfJSONRef.max, jsfJSONRef.constrained, true);
                var slider = CreateSlider(jsfJSONProxy, true);
                _jsfJSONRefs.Add(new FloatParamJSONRef
                {
                    Storable = target.Storable,
                    SourceFloatParam = jsfJSONRef,
                    Proxy = jsfJSONProxy,
                    Slider = slider
                });
            }
        }

        #endregion

        #region Callbacks

        private void UpdateToggleAnimatedFloatParamButton(string name)
        {
            if (_toggleFloatParamUI == null) return;

            var btnText = _toggleFloatParamUI.button.GetComponentInChildren<Text>();
            if (_animation == null || string.IsNullOrEmpty(name))
            {
                btnText.text = "Add/Remove Param";
                _toggleFloatParamUI.button.interactable = false;
                return;
            }

            _toggleFloatParamUI.button.interactable = true;
            if (_animation.Current.TargetFloatParams.Any(c => c.FloatParam.name == name))
                btnText.text = "Remove Param";
            else
                btnText.text = "Add Param";
        }

        private void ToggleAnimatedFloatParam()
        {
            try
            {
                var storable = containingAtom.GetStorableByID(_addStorableListJSON.val);
                if (storable == null)
                {
                    SuperController.LogError($"Storable {_addStorableListJSON.val} in atom {containingAtom.uid} does not exist");
                    return;
                }
                var sourceFloatParam = storable.GetFloatJSONParam(_addParamListJSON.val);
                if (sourceFloatParam == null)
                {
                    SuperController.LogError($"Param {_addParamListJSON.val} in atom {containingAtom.uid} does not exist");
                    return;
                }
                if (_animation.Current.TargetFloatParams.Any(c => c.FloatParam == sourceFloatParam))
                {
                    _animation.Current.TargetFloatParams.Remove(_animation.Current.TargetFloatParams.First(c => c.FloatParam == sourceFloatParam));
                }
                else
                {
                    var target = new FloatParamAnimationTarget(storable, sourceFloatParam, _animation.AnimationLength);
                    target.SetKeyframe(0, sourceFloatParam.val);
                    _animation.Current.TargetFloatParams.Add(target);
                }
                RefreshFloatParamsListUI();
                AnimationUpdated();
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.FloatParamsPlugin.ToggleAnimatedFloatParam: " + exc);
            }
        }

        private void UpdateFloatParam(FloatParamAnimationTarget target, JSONStorableFloat sourceFloatParam, float val)
        {
            sourceFloatParam.val = val;
            // TODO: This should be done by the controller (updating the animation resets the time)
            var time = _animation.Time;
            target.SetKeyframe(time, val);
            _animation.RebuildAnimation();
            UpdateTime(time);
            AnimationUpdated();
        }

        #endregion

        #region Updates

        protected void StateRestored()
        {
            UpdateToggleAnimatedFloatParamButton(_addParamListJSON.val);
            RefreshFloatParamsListUI();
        }

        protected void ContextUpdatedCustom()
        {
            if (_jsfJSONRefs != null)
            {
                foreach (var jsfJSONRef in _jsfJSONRefs)
                    jsfJSONRef.Proxy.valNoCallback = jsfJSONRef.SourceFloatParam.val;
            }
        }

        #endregion
    }
}

