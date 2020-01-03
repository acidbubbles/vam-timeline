using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace VamTimeline
{

    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public abstract class PluginImplBase<TAnimation> where TAnimation : class, IAnimation
    {
        private const string AllTargets = "(All)";

        protected readonly IAnimationPlugin _plugin;

        // State
        private IAnimationSerializer<TAnimation> _serializer;
        protected TAnimation _animation;
        private bool _restoring;
        private readonly List<string> _undoList = new List<string>();
        private IClipboardEntry _clipboard;

        // Save
        private JSONStorableString _saveJSON;

        // Storables
        // TODO: Make protected variables private once extraction is complete
        private JSONStorableStringChooser _animationJSON;
        protected JSONStorableFloat _scrubberJSON;
        private JSONStorableAction _playJSON;
        private JSONStorableAction _playIfNotPlayingJSON;
        private JSONStorableAction _stopJSON;
        private JSONStorableStringChooser _filterAnimationTargetJSON;
        private JSONStorableAction _nextFrameJSON;
        private JSONStorableAction _previousFrameJSON;

        protected JSONStorableBool _lockedJSON;
        private JSONStorableFloat _lengthJSON;
        private JSONStorableFloat _speedJSON;
        private JSONStorableFloat _blendDurationJSON;
        private JSONStorableStringChooser _displayModeJSON;
        private JSONStorableString _displayJSON;

        // Backup
        protected abstract string BackupStorableName { get; }

        protected PluginImplBase(IAnimationPlugin plugin)
        {
            _plugin = plugin;
        }

        #region Initialization

        public void RegisterSerializer(IAnimationSerializer<TAnimation> serializer)
        {
            _serializer = serializer;
        }

        public void InitCommonStorables()
        {
            _saveJSON = new JSONStorableString(StorableNames.Save, "", (string v) => RestoreState(v));
            _plugin.RegisterString(_saveJSON);

            _animationJSON = new JSONStorableStringChooser(StorableNames.Animation, new List<string>(), "Anim1", "Animation", val => ChangeAnimation(val))
            {
                isStorable = false
            };
            _plugin.RegisterStringChooser(_animationJSON);

            _scrubberJSON = new JSONStorableFloat(StorableNames.Time, 0f, v => UpdateTime(v), 0f, 5f - float.Epsilon, true)
            {
                isStorable = false
            };
            _plugin.RegisterFloat(_scrubberJSON);

            _playJSON = new JSONStorableAction(StorableNames.Play, () => { _animation.Play(); ContextUpdated(); });
            _plugin.RegisterAction(_playJSON);

            _playIfNotPlayingJSON = new JSONStorableAction(StorableNames.PlayIfNotPlaying, () => { if (!_animation.IsPlaying()) { _animation.Play(); ContextUpdated(); } });
            _plugin.RegisterAction(_playIfNotPlayingJSON);

            _stopJSON = new JSONStorableAction(StorableNames.Stop, () => { _animation.Stop(); ContextUpdated(); });
            _plugin.RegisterAction(_stopJSON);

            _filterAnimationTargetJSON = new JSONStorableStringChooser(StorableNames.FilterAnimationTarget, new List<string> { AllTargets }, AllTargets, StorableNames.FilterAnimationTarget, val => { _animation.SelectTargetByName(val == AllTargets ? "" : val); ContextUpdated(); })
            {
                isStorable = false
            };
            _plugin.RegisterStringChooser(_filterAnimationTargetJSON);

            _nextFrameJSON = new JSONStorableAction(StorableNames.NextFrame, () => { UpdateTime(_animation.GetNextFrame()); ContextUpdated(); });
            _plugin.RegisterAction(_nextFrameJSON);

            _previousFrameJSON = new JSONStorableAction(StorableNames.PreviousFrame, () => { UpdateTime(_animation.GetPreviousFrame()); ContextUpdated(); });
            _plugin.RegisterAction(_previousFrameJSON);

            _lockedJSON = new JSONStorableBool(StorableNames.Locked, false, (bool val) => ContextUpdated());
            _plugin.RegisterBool(_lockedJSON);

            _lengthJSON = new JSONStorableFloat(StorableNames.AnimationLength, 5f, v => UpdateAnimationLength(v), 0.5f, 120f, false, true);

            _speedJSON = new JSONStorableFloat(StorableNames.AnimationSpeed, 1f, v => UpdateAnimationSpeed(v), 0.001f, 5f, false);

            _blendDurationJSON = new JSONStorableFloat(StorableNames.BlendDuration, 1f, v => UpdateBlendDuration(v), 0.001f, 5f, false);

            _displayModeJSON = new JSONStorableStringChooser(StorableNames.DisplayMode, RenderingModes.Values, RenderingModes.Default, "Display Mode", (string val) => { ContextUpdated(); });
            _displayJSON = new JSONStorableString(StorableNames.Display, "")
            {
                isStorable = false
            };
            _plugin.RegisterString(_displayJSON);
        }

        protected void InitCommonCustomUI()
        {
            // Left side

            var animationUI = _plugin.CreateScrollablePopup(_animationJSON, false);
            animationUI.popupPanelHeight = 800f;
            animationUI.popup.onOpenPopupHandlers += () => _animationJSON.choices = _animation.GetAnimationNames().ToList();

            _plugin.CreateSlider(_scrubberJSON);

            var playUI = _plugin.CreateButton("\u25B6 Play");
            playUI.button.onClick.AddListener(() => _playJSON.actionCallback());

            var stopUI = _plugin.CreateButton("\u25A0 Stop");
            stopUI.button.onClick.AddListener(() => _stopJSON.actionCallback());

            var selectedControllerUI = _plugin.CreateScrollablePopup(_filterAnimationTargetJSON);
            selectedControllerUI.popupPanelHeight = 800f;

            var nextFrameUI = _plugin.CreateButton("\u2192 Next Frame");
            nextFrameUI.button.onClick.AddListener(() => _nextFrameJSON.actionCallback());

            var previousFrameUI = _plugin.CreateButton("\u2190 Previous Frame");
            previousFrameUI.button.onClick.AddListener(() => _previousFrameJSON.actionCallback());

            // TODO: Insert curve actions here

            var cutUI = _plugin.CreateButton("Cut / Delete Frame", false);
            cutUI.button.onClick.AddListener(() => Cut());

            var copyUI = _plugin.CreateButton("Copy Frame", false);
            copyUI.button.onClick.AddListener(() => Copy());

            var pasteUI = _plugin.CreateButton("Paste Frame", false);
            pasteUI.button.onClick.AddListener(() => Paste());

            var undoUI = _plugin.CreateButton("Undo", false);
            // TODO: Right now it doesn't work for some reason...
            undoUI.button.interactable = false;
            undoUI.button.onClick.AddListener(() => Undo());

            // Right side

            var lockedUI = _plugin.CreateToggle(_lockedJSON, true);
            lockedUI.label = "Locked (Performance Mode)";

            var addAnimationUI = _plugin.CreateButton("Add New Animation", true);
            addAnimationUI.button.onClick.AddListener(() => AddAnimation());

            _plugin.CreateSlider(_lengthJSON, true);

            _plugin.CreateSlider(_speedJSON, true);

            _plugin.CreateSlider(_blendDurationJSON, true);

            _plugin.CreatePopup(_displayModeJSON, true);

            _plugin.CreateTextField(_displayJSON, true);
        }

        protected IEnumerator CreateAnimationIfNoneIsLoaded()
        {
            if (_animation != null) yield break;
            yield return new WaitForEndOfFrame();
            RestoreState(_saveJSON.val);
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
                    // TODO: Name the backup to avoid conflict between atom and morph animations
                    var backupStorableID = _plugin.ContainingAtom.GetStorableIDs().FirstOrDefault(s => s.EndsWith("VamTimeline.BackupPlugin"));
                    if (backupStorableID != null)
                    {
                        var backupStorable = _plugin.ContainingAtom.GetStorableByID(backupStorableID);
                        var backupJSON = backupStorable.GetStringJSONParam(BackupStorableName);
                        if (!string.IsNullOrEmpty(backupJSON.val))
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

                // if (serialized == _undoList.LastOrDefault())
                //     return;

                // TODO: Bring back undoes
                // if (!string.IsNullOrEmpty(_saveJSON.val))
                // {
                //     _undoList.Add(_saveJSON.val);
                //     if (_undoList.Count > MaxUndo) _undoList.RemoveAt(0);
                // }

                _saveJSON.valNoCallback = serialized;

                var backupStorableID = _plugin.ContainingAtom.GetStorableIDs().FirstOrDefault(s => s.EndsWith("VamTimeline.BackupPlugin"));
                if (backupStorableID != null)
                {
                    var backupStorable = _plugin.ContainingAtom.GetStorableByID(backupStorableID);
                    var backupJSON = backupStorable.GetStringJSONParam(BackupStorableName);
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

        protected virtual void UpdateTime(float time)
        {
            _animation.Time = time;
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
            var pop = _undoList[_undoList.Count - 1];
            _undoList.RemoveAt(_undoList.Count - 1);
            RestoreState(pop);
            // TODO: Fix undo
            // _saveJSON.valNoCallback = pop;
        }

        private void AddAnimation()
        {
            var animationName = _animation.AddAnimation();
            AnimationUpdated();
            ChangeAnimation(animationName);
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
            foreach (var controller in _animation.GetAllOrSelectedTargets())
            {
                var keyTimes = controller.GetAllKeyframesTime();
                foreach (var keyTime in keyTimes)
                {
                    frames.Add(keyTime);
                    if (keyTime == time)
                        controllers.Add(controller.Name);
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
            foreach (var controller in _animation.GetAllOrSelectedTargets())
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
            var time = _scrubberJSON.val;
            var display = new StringBuilder();
            display.AppendLine($"Time: {time}s");
            foreach (var controller in _animation.GetAllOrSelectedTargets())
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
                _filterAnimationTargetJSON.choices = new List<string> { AllTargets }.Concat(_animation.GetTargetsNames()).ToList();

                AnimationUpdatedCustom();

                // Save
                SaveState();

                // Render
                RenderState();

                // Dispatch to VamTimelineController
                var externalControllers = SuperController.singleton.GetAtoms().Where(a => a.type == "SimpleSign");
                foreach (var controller in externalControllers)
                    controller.BroadcastMessage("VamTimelineAnimationUpdated", _plugin.ContainingAtom.uid);
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.AtomPlugin.AnimationUpdated: " + exc);
            }
        }

        protected abstract void AnimationUpdatedCustom();

        protected void ContextUpdated()
        {
            try
            {
                var time = _animation.Time;

                // Update UI
                _scrubberJSON.valNoCallback = time;

                // Render
                RenderState();

                // Dispatch to VamTimelineController
                var externalControllers = SuperController.singleton.GetAtoms().Where(a => a.type == "SimpleSign");
                foreach (var controller in externalControllers)
                    controller.BroadcastMessage("VamTimelineContextChanged", _plugin.ContainingAtom.uid);
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.AtomPlugin.ContextUpdated: " + exc);
            }
        }

        #endregion
    }
}
