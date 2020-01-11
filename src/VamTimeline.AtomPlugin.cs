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

        // Storables

        private const int MaxUndo = 20;
        private const string AllTargets = "(All)";
        private bool _saveEnabled;

        // State
        public AtomAnimation Animation { get; private set; }
        public Atom ContainingAtom => containingAtom;
        private AtomAnimationSerializer _serializer;
        private bool _restoring;
        private readonly List<string> _undoList = new List<string>();
        private AtomClipboardEntry _clipboard;
        private FreeControllerAnimationTarget _grabbedController;

        // Save
        private JSONStorableString _saveJSON;

        // Storables
        public JSONStorableStringChooser AnimationJSON { get; private set; }
        public JSONStorableFloat ScrubberJSON { get; private set; }
        public JSONStorableAction PlayJSON { get; private set; }
        public JSONStorableBool IsPlayingJSON { get; private set; }
        public JSONStorableAction PlayIfNotPlayingJSON { get; private set; }
        public JSONStorableAction StopJSON { get; private set; }
        public JSONStorableAction StopIfPlayingJSON { get; private set; }
        public JSONStorableStringChooser FilterAnimationTargetJSON { get; private set; }
        public JSONStorableAction NextFrameJSON { get; private set; }
        public JSONStorableAction PreviousFrameJSON { get; private set; }
        public JSONStorableAction SmoothAllFramesJSON { get; private set; }
        public JSONStorableAction CutJSON { get; private set; }
        public JSONStorableAction CopyJSON { get; private set; }
        public JSONStorableAction PasteJSON { get; private set; }
        public JSONStorableAction UndoJSON { get; private set; }
        public JSONStorableBool LockedJSON { get; private set; }
        public JSONStorableString DisplayJSON { get; private set; }

        // UI
        private AtomAnimationUIManager _ui;

        #region Init

        public override void Init()
        {
            try
            {
                _serializer = new AtomAnimationSerializer(containingAtom);
                _ui = new AtomAnimationUIManager(this);
                InitSharedStorables();
                _ui.Init();
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
            if (Animation == null) return;

            try
            {
                if (Animation.IsPlaying())
                {
                    var time = Animation.Time;
                    if (time != ScrubberJSON.val)
                    {
                        ScrubberJSON.valNoCallback = time;
                    }
                    UpdatePlaying();
                    // RenderState() // In practice, we don't see anything useful
                }
                else if (LockedJSON != null && !LockedJSON.val)
                {
                    UpdateNotPlaying();
                }
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.AtomPlugin.Update: " + exc);
            }
        }

        private void UpdatePlaying()
        {
            Animation.Update();
            _ui.UpdatePlaying();
            if (!Animation.Current.Loop && Animation.Time >= Animation.Current.AnimationLength)
            {
                Animation.Stop();
                AnimationFrameUpdated();
            }
        }

        private void UpdateNotPlaying()
        {
            var grabbing = SuperController.singleton.RightGrabbedController ?? SuperController.singleton.LeftGrabbedController;
            if (grabbing != null && grabbing.containingAtom != containingAtom)
                grabbing = null;
            else if (Input.GetMouseButton(0) && grabbing == null)
                grabbing = containingAtom.freeControllers.FirstOrDefault(c => GrabbingControllers.Contains(c.linkToRB?.gameObject.name));

            if (_grabbedController == null && grabbing != null)
            {
                _grabbedController = Animation.Current.TargetControllers.FirstOrDefault(c => c.Controller == grabbing);
            }
            else if (_grabbedController != null && grabbing == null)
            {
                // TODO: This should be done by the controller (updating the animation resets the time)
                Animation.SetKeyframeToCurrentTransform(_grabbedController, Animation.Time);
                _grabbedController = null;
                AnimationModified();
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
                Animation?.Stop();
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

        public void InitSharedStorables()
        {
            _saveJSON = new JSONStorableString(StorableNames.Save, "", (string v) => RestoreState(v));
            RegisterString(_saveJSON);

            AnimationJSON = new JSONStorableStringChooser(StorableNames.Animation, new List<string>(), "Anim1", "Animation", val => ChangeAnimation(val))
            {
                isStorable = false
            };
            RegisterStringChooser(AnimationJSON);

            ScrubberJSON = new JSONStorableFloat(StorableNames.Time, 0f, v => UpdateTime(v), 0f, AtomAnimationClip.DefaultAnimationLength, true)
            {
                isStorable = false
            };
            RegisterFloat(ScrubberJSON);

            PlayJSON = new JSONStorableAction(StorableNames.Play, () =>
            {
                Animation.Play();
                IsPlayingJSON.valNoCallback = true;
                AnimationFrameUpdated();
            });
            RegisterAction(PlayJSON);

            PlayIfNotPlayingJSON = new JSONStorableAction(StorableNames.PlayIfNotPlaying, () =>
            {
                if (Animation.IsPlaying()) return;
                Animation.Play();
                IsPlayingJSON.valNoCallback = true;
            });
            RegisterAction(PlayIfNotPlayingJSON);

            IsPlayingJSON = new JSONStorableBool(StorableNames.IsPlaying, false, (bool val) =>
            {
                if (val)
                    PlayIfNotPlayingJSON.actionCallback();
                else
                    StopJSON.actionCallback();
            })
            {
                isStorable = false
            };
            RegisterBool(IsPlayingJSON);

            StopJSON = new JSONStorableAction(StorableNames.Stop, () =>
            {
                if (Animation.IsPlaying())
                {
                    Animation.Stop();
                    IsPlayingJSON.valNoCallback = false;
                }
                else
                {
                    Animation.Time = 0f;
                }
                AnimationFrameUpdated();
            });
            RegisterAction(StopJSON);

            StopIfPlayingJSON = new JSONStorableAction(StorableNames.StopIfPlaying, () =>
            {
                if (!Animation.IsPlaying()) return;
                Animation.Stop();
                IsPlayingJSON.valNoCallback = false;
            });
            RegisterAction(StopIfPlayingJSON);

            FilterAnimationTargetJSON = new JSONStorableStringChooser(StorableNames.FilterAnimationTarget, new List<string> { AllTargets }, AllTargets, StorableNames.FilterAnimationTarget, val => { Animation.Current.SelectTargetByName(val == AllTargets ? "" : val); AnimationFrameUpdated(); })
            {
                isStorable = false
            };
            RegisterStringChooser(FilterAnimationTargetJSON);

            NextFrameJSON = new JSONStorableAction(StorableNames.NextFrame, () => NextFrame());
            RegisterAction(NextFrameJSON);

            PreviousFrameJSON = new JSONStorableAction(StorableNames.PreviousFrame, () => PreviousFrame());
            RegisterAction(PreviousFrameJSON);

            SmoothAllFramesJSON = new JSONStorableAction(StorableNames.SmoothAllFrames, () => SmoothAllFrames());

            CutJSON = new JSONStorableAction("Cut", () => Cut());
            CopyJSON = new JSONStorableAction("Copy", () => Copy());
            PasteJSON = new JSONStorableAction("Paste", () => Paste());
            UndoJSON = new JSONStorableAction("Undo", () => Undo());

            LockedJSON = new JSONStorableBool(StorableNames.Locked, false, (bool val) => AnimationModified());
            RegisterBool(LockedJSON);

            DisplayJSON = new JSONStorableString(StorableNames.Display, "")
            {
                isStorable = false
            };
            RegisterString(DisplayJSON);
        }

        private IEnumerator CreateAnimationIfNoneIsLoaded()
        {
            if (Animation != null)
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
                if (Animation != null)
                    Animation = null;

                if (!string.IsNullOrEmpty(json))
                {
                    Animation = _serializer.DeserializeAnimation(json);
                }

                if (Animation == null)
                {
                    var backupStorableID = containingAtom.GetStorableIDs().FirstOrDefault(s => s.EndsWith("VamTimeline.BackupPlugin"));
                    if (backupStorableID != null)
                    {
                        var backupStorable = containingAtom.GetStorableByID(backupStorableID);
                        var backupJSON = backupStorable.GetStringJSONParam(StorableNames.AtomAnimationBackup);
                        if (backupJSON != null && !string.IsNullOrEmpty(backupJSON.val))
                        {
                            SuperController.LogMessage("VamTimeline: No save found but a backup was detected. Loading backup.");
                            Animation = _serializer.DeserializeAnimation(backupJSON.val);
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
                if (Animation == null)
                    Animation = new AtomAnimation(containingAtom);

                Animation.Initialize();
                AnimationModified();
                AnimationFrameUpdated();
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
                if (Animation.IsEmpty()) return;

                var serialized = _serializer.SerializeAnimation(Animation);

                if (serialized == _undoList.LastOrDefault())
                    return;

                if (!string.IsNullOrEmpty(_saveJSON.val))
                {
                    _undoList.Add(_saveJSON.val);
                    if (_undoList.Count > MaxUndo) _undoList.RemoveAt(0);
                }

                _saveJSON.valNoCallback = serialized;

                var backupStorableID = containingAtom.GetStorableIDs().FirstOrDefault(s => s.EndsWith("VamTimeline.BackupPlugin"));
                if (backupStorableID != null)
                {
                    var backupStorable = containingAtom.GetStorableByID(backupStorableID);
                    var backupJSON = backupStorable.GetStringJSONParam(StorableNames.PushAtomAnimationBackup);
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
            _saveEnabled = false;
            try
            {
                FilterAnimationTargetJSON.val = AllTargets;
                if (!Animation.IsPlaying() || Animation.Current.AnimationName != animationName)
                    Animation.ChangeAnimation(animationName);
                if (Animation.IsPlaying())
                    AnimationJSON.valNoCallback = Animation.PlayedAnimation;
                else
                    AnimationModified();
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.AtomPlugin.ChangeAnimation: " + exc);
            }
            finally
            {
                _saveEnabled = true;
            }
        }

        private void UpdateTime(float time)
        {
            time = (float)(Math.Round(time * 1000f) / 1000f);
            if (Animation.Current.Loop && time >= Animation.Current.AnimationLength)
                time = Animation.Current.AnimationLength - AtomAnimation.PaddingBeforeLoopFrame;

            Animation.Time = time;
            if (Animation.Current.AnimationPattern != null)
                Animation.Current.AnimationPattern.SetFloatParamValue("currentTime", time);
            AnimationFrameUpdated();
        }

        private void NextFrame()
        {
            var originalTime = Animation.Time;
            var time = Animation.Current.GetNextFrame(Animation.Time);
            UpdateTime(time);
            AnimationFrameUpdated();
        }

        private void PreviousFrame()
        {
            var time = Animation.Current.GetPreviousFrame(Animation.Time);
            UpdateTime(time);
            AnimationFrameUpdated();
        }

        private void Cut()
        {
            Copy();
            if (Animation.Time == 0f) return;
            Animation.DeleteFrame();
            AnimationModified();
        }

        private void Copy()
        {
            try
            {
                _clipboard = Animation.Copy();
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
                    SuperController.LogMessage("VamTimeline: Clipboard is empty");
                    return;
                }
                var time = Animation.Time;
                Animation.Paste(_clipboard);
                // Sample animation now
                UpdateTime(time);
                AnimationModified();
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.AtomPlugin.Paste: " + exc);
            }
        }

        private void Undo()
        {
            if (_undoList.Count == 0) return;
            var animationName = AnimationJSON.val;
            var pop = _undoList[_undoList.Count - 1];
            _undoList.RemoveAt(_undoList.Count - 1);
            if (_undoList.Count == 0) return;
            if (string.IsNullOrEmpty(pop)) return;
            var time = Animation.Time;
            _saveEnabled = false;
            try
            {
                RestoreState(pop);
                _saveJSON.valNoCallback = pop;
                if (Animation.Clips.Any(c => c.AnimationName == animationName))
                    AnimationJSON.val = animationName;
                else
                    AnimationJSON.valNoCallback = Animation.Clips.First().AnimationName;
                AnimationModified();
                UpdateTime(time);
            }
            finally
            {
                _saveEnabled = true;
            }
        }

        private void SmoothAllFrames()
        {
            Animation.SmoothAllFrames();
            AnimationModified();
        }

        #endregion

        #region State Rendering

        public void RenderState()
        {
            if (LockedJSON.val)
            {
                DisplayJSON.val = "Locked";
                return;
            }

            if (Animation.IsPlaying())
            {
                DisplayJSON.val = "Playing...";
                return;
            }

            var time = ScrubberJSON.val;
            var frames = new List<float>();
            var targets = new List<string>();
            foreach (var target in Animation.Current.GetAllOrSelectedTargets())
            {
                var keyTimes = target.GetAllKeyframesTime();
                foreach (var keyTime in keyTimes)
                {
                    frames.Add(keyTime);
                    if (keyTime == time)
                        targets.Add(target.Name);
                }
            }

            if (targets.Count == 0)
            {
                DisplayJSON.val = $"No controller has been registered{(Animation.Current.AllTargets.Any() ? " at this frame." : ". Go to Animation Settings and add one.")}";
                return;
            }


            var display = new StringBuilder();
            if (frames.Count == 1)
            {
                display.AppendLine("No frame have been recorded yet.");
            }
            else
            {
                frames.Sort();
                display.Append("Frames:");
                foreach (var f in frames.Distinct())
                {
                    if (f == time)
                        display.Append($"[{f:0.00}]");
                    else
                        display.Append($" {f:0.00} ");
                }
            }
            display.AppendLine();
            display.AppendLine("Affects:");
            foreach (var c in targets)
                display.AppendLine(c);
            DisplayJSON.val = display.ToString();
        }

        #endregion

        #region Updates

        public void AnimationModified()
        {
            try
            {
                // Update UI
                ScrubberJSON.max = Animation.Current.AnimationLength;
                ScrubberJSON.valNoCallback = Animation.Time;
                AnimationJSON.choices = Animation.GetAnimationNames().ToList();
                AnimationJSON.valNoCallback = Animation.Current.AnimationName;
                FilterAnimationTargetJSON.choices = new List<string> { AllTargets }.Concat(Animation.Current.GetTargetsNames()).ToList();

                // Save
                if (_saveEnabled)
                    SaveState();

                // Render
                RenderState();

                // UI
                _ui.AnimationModified();

                // Dispatch to VamTimelineController
                var externalControllers = SuperController.singleton.GetAtoms().Where(a => a.type == "SimpleSign");
                foreach (var controller in externalControllers)
                    controller.BroadcastMessage("VamTimelineAnimationModified", containingAtom.uid);
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.AtomPlugin.AnimationModified: " + exc);
            }
        }

        private void AnimationFrameUpdated()
        {
            try
            {
                var time = Animation.Time;

                // Update UI
                ScrubberJSON.valNoCallback = time;

                _ui.AnimationFrameUpdated();

                // Render
                RenderState();

                // Dispatch to VamTimelineController
                var externalControllers = SuperController.singleton.GetAtoms().Where(a => a.type == "SimpleSign");
                foreach (var controller in externalControllers)
                    controller.BroadcastMessage("VamTimelineAnimationFrameUpdated", containingAtom.uid);
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.AtomPlugin.AnimationFrameUpdated: " + exc);
            }
        }

        #endregion

        #region Utils

        public UIDynamicTextField CreateTextInput(JSONStorableString jss, bool rightSide = false)
        {
            var textfield = CreateTextField(jss, rightSide);
            textfield.height = 20f;
            textfield.backgroundColor = Color.white;
            var input = textfield.gameObject.AddComponent<InputField>();
            var rect = input.GetComponent<RectTransform>().sizeDelta = new Vector2(1f, 0.4f);
            input.textComponent = textfield.UItext;
            jss.inputField = input;
            return textfield;
        }

        #endregion
    }
}

