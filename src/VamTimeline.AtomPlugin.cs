using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SimpleJSON;
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
        private const string PlayingAnimationName = "(Playing...)";
        private bool _saveEnabled;

        // State
        public AtomAnimation Animation { get; private set; }
        public Atom ContainingAtom => containingAtom;
        public AtomAnimationSerializer Serializer { get; private set; }
        private bool _restoring;
        private readonly List<string> _undoList = new List<string>();
        private AtomClipboardEntry _clipboard;
        private FreeControllerAnimationTarget _grabbedController;

        // Storables
        public JSONStorableStringChooser AnimationJSON { get; private set; }
        public JSONStorableFloat ScrubberJSON { get; private set; }
        public JSONStorableFloat TimeJSON { get; private set; }
        public JSONStorableAction PlayJSON { get; private set; }
        public JSONStorableBool IsPlayingJSON { get; private set; }
        public JSONStorableAction PlayIfNotPlayingJSON { get; private set; }
        public JSONStorableAction StopJSON { get; private set; }
        public JSONStorableAction StopIfPlayingJSON { get; private set; }
        public JSONStorableStringChooser FilterAnimationTargetJSON { get; private set; }
        public JSONStorableAction NextFrameJSON { get; private set; }
        public JSONStorableAction PreviousFrameJSON { get; private set; }
        public JSONStorableFloat SnapJSON { get; private set; }
        public JSONStorableAction CutJSON { get; private set; }
        public JSONStorableAction CopyJSON { get; private set; }
        public JSONStorableAction PasteJSON { get; private set; }
        public JSONStorableAction UndoJSON { get; private set; }
        public JSONStorableBool LockedJSON { get; private set; }
        public JSONStorableString DisplayJSON { get; private set; }
        public JSONStorableString ImportJSON { get; private set; }

        // UI
        private AtomAnimationUIManager _ui;

        #region Init

        public override void Init()
        {
            try
            {
                Serializer = new AtomAnimationSerializer(containingAtom);
                _ui = new AtomAnimationUIManager(this);
                InitStorables();
                _ui.Init();
                StartCoroutine(DeferredInit());
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
                        ScrubberJSON.valNoCallback = time;
                    // Note: We don't update SetTime in real time, it's not necessary
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
                var time = (float)(Math.Round(Animation.Time * 1000f) / 1000f);
                Animation.SetKeyframeToCurrentTransform(_grabbedController, time);
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

        public void InitStorables()
        {
            ImportJSON = new JSONStorableString(StorableNames.Import, "", (string v) =>
            {
                if (!string.IsNullOrEmpty(v))
                    Load(JSONNode.Parse(v));
            })
            {
                isStorable = false
            };
            RegisterString(ImportJSON);

            AnimationJSON = new JSONStorableStringChooser(StorableNames.Animation, new List<string>(), "", "Animation", val => ChangeAnimation(val))
            {
                isStorable = false
            };
            RegisterStringChooser(AnimationJSON);

            ScrubberJSON = new JSONStorableFloat(StorableNames.Scrubber, 0f, v => UpdateTime(v, true), 0f, AtomAnimationClip.DefaultAnimationLength, true)
            {
                isStorable = false
            };
            RegisterFloat(ScrubberJSON);

            TimeJSON = new JSONStorableFloat(StorableNames.Time, 0f, v => UpdateTime(v, false), 0f, AtomAnimationClip.DefaultAnimationLength, true)
            {
                isStorable = false
            };
            RegisterFloat(TimeJSON);

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
                AnimationFrameUpdated();
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
                    Animation.Time = GetRounded(Animation.Time);
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
                Animation.Time = GetRounded(Animation.Time);
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

            SnapJSON = new JSONStorableFloat(StorableNames.Snap, 0.01f, (float val) =>
            {
                var rounded = GetRounded(val);
                if (val != rounded)
                    SnapJSON.valNoCallback = rounded;
                if (Animation != null && Animation.Time % rounded != 0)
                    UpdateTime(Animation.Time, true);
            }, 0.001f, 1f, true)
            {
                isStorable = true
            };
            RegisterFloat(SnapJSON);

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

        private IEnumerator DeferredInit()
        {
            yield return new WaitForEndOfFrame();
            if (Animation != null)
            {
                _saveEnabled = true;
                yield break;
            }
            containingAtom.RestoreFromLast(this);
            if (Animation != null)
            {
                _saveEnabled = true;
                yield break;
            }
            try
            {
                Animation = new AtomAnimation(containingAtom);

                Animation.Initialize();
                AnimationModified();
                AnimationFrameUpdated();
            }
            finally
            {
                _saveEnabled = true;
            }
        }

        #endregion

        #region Load / Save

        public override JSONClass GetJSON(bool includePhysical = true, bool includeAppearance = true, bool forceStore = false)
        {
            try
            {
                Animation?.Stop();
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.AtomPlugin.GetJSON (Stop): " + exc);
            }

            var json = base.GetJSON(includePhysical, includeAppearance, forceStore);

            try
            {
                json["Animation"] = Serializer.SerializeAnimation(Animation);
                needsStore = true;
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.AtomPlugin.GetJSON (Serialize): " + exc);
            }

            return json;
        }

        public override void RestoreFromJSON(JSONClass jc, bool restorePhysical = true, bool restoreAppearance = true, JSONArray presetAtoms = null, bool setMissingToDefault = true)
        {
            base.RestoreFromJSON(jc, restorePhysical, restoreAppearance, presetAtoms, setMissingToDefault);

            try
            {
                var animationJSON = jc["Animation"];
                if (animationJSON != null && animationJSON.AsObject != null)
                {
                    Load(animationJSON);
                    return;
                }

                var legacyStr = jc["Save"];
                if (!string.IsNullOrEmpty(legacyStr))
                {
                    Load(JSONNode.Parse(legacyStr) as JSONClass);
                    return;
                }
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.AtomPlugin.RestoreFromJSON: " + exc);
            }

        }

        private void Load(JSONNode animationJSON)
        {
            if (_restoring) return;
            _restoring = true;
            try
            {
                Animation = Serializer.DeserializeAnimation(animationJSON.AsObject);
                if (Animation == null) throw new NullReferenceException("Animation deserialized to null");

                Animation.Initialize();
                AnimationModified();
                AnimationFrameUpdated();
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.AtomPlugin.Load: " + exc);
            }
            finally
            {
                _restoring = false;
            }
        }

        public void Save()
        {
            try
            {
                if (_restoring) return;
                if (Animation.IsEmpty()) return;

                var serialized = Serializer.SerializeAnimation(Animation);

                if (serialized == _undoList.LastOrDefault())
                    return;

                if (_undoList.Count > MaxUndo) _undoList.RemoveAt(0);
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.AtomPlugin.SaveState: " + exc);
            }
        }

        #endregion

        #region Callbacks

        private void ChangeAnimation(string animationName)
        {
            if (string.IsNullOrEmpty(animationName)) return;

            _saveEnabled = false;
            try
            {
                FilterAnimationTargetJSON.val = AllTargets;
                if (Animation.IsPlaying())
                {
                    if (Animation.Current.AnimationName != animationName)
                    {
                        Animation.ChangeAnimation(animationName);
                        AnimationModified();
                    }
                    else
                    {
                        AnimationJSON.valNoCallback = PlayingAnimationName;
                    }
                }
                else
                {
                    Animation.ChangeAnimation(animationName);
                    UpdateTime(0f, false);
                    AnimationModified();
                }
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

        private IEnumerator _reEnableCollisions = null;
        private void UpdateTime(float time, bool snap)
        {
            time = (float)(Math.Round(time * 1000f) / 1000f);

            if (snap)
            {
                var snapDelta = time % SnapJSON.val;
                if (snapDelta != 0f)
                {
                    time -= snapDelta;
                    if (snapDelta > SnapJSON.val / 2f)
                        time += SnapJSON.val;

                }
            }

            if (Animation.Current.Loop && time >= Animation.Current.AnimationLength)
                time = Animation.Current.AnimationLength - SnapJSON.val;

            if (containingAtom.collisionEnabledJSON.val == true && Math.Abs(Animation.Time - time) > 1f)
            {
                if (_reEnableCollisions != null)
                    StopCoroutine(_reEnableCollisions);
                else
                    containingAtom.collisionEnabledJSON.val = false;
                _reEnableCollisions = ReEnableCollisions();
                StartCoroutine(_reEnableCollisions);
            }

            Animation.Time = time;
            if (Animation.Current.AnimationPattern != null)
                Animation.Current.AnimationPattern.SetFloatParamValue("currentTime", time);
            AnimationFrameUpdated();
        }

        private IEnumerator ReEnableCollisions()
        {
            yield return new WaitForSeconds(0.3f);
            containingAtom.collisionEnabledJSON.val = true;
            _reEnableCollisions = null;
        }

        private void NextFrame()
        {
            var originalTime = Animation.Time;
            var time = Animation.Current.GetNextFrame(Animation.Time);
            UpdateTime(time, false);
            AnimationFrameUpdated();
        }

        private void PreviousFrame()
        {
            var time = Animation.Current.GetPreviousFrame(Animation.Time);
            UpdateTime(time, false);
            AnimationFrameUpdated();
        }

        private void Cut()
        {
            try
            {
                if (Animation.IsPlaying()) return;
                _clipboard = Animation.Current.Copy(Animation.Time);
                var time = Animation.Time;
                if (time == 0f || time == Animation.Current.AnimationLength) return;
                Animation.Current.DeleteFrame(time);
                Animation.RebuildAnimation();
                AnimationModified();
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.AtomPlugin.Cut: " + exc);
            }
        }

        private void Copy()
        {
            try
            {
                if (Animation.IsPlaying()) return;

                _clipboard = Animation.Current.Copy(Animation.Time);
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
                if (Animation.IsPlaying()) return;

                if (_clipboard == null)
                {
                    SuperController.LogMessage("VamTimeline: Clipboard is empty");
                    return;
                }
                var time = Animation.Time;
                Animation.Current.Paste(Animation.Time, _clipboard);
                Animation.RebuildAnimation();
                // Sample animation now
                UpdateTime(time, false);
                AnimationModified();
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.AtomPlugin.Paste: " + exc);
            }
        }

        private void Undo()
        {
            try
            {
                if (Animation.IsPlaying()) return;
                if (_undoList.Count == 0) return;
                var animationName = AnimationJSON.val;
                var pop = _undoList[_undoList.Count - 1];
                _undoList.RemoveAt(_undoList.Count - 1);
                if (_undoList.Count == 0) return;
                if (string.IsNullOrEmpty(pop)) return;

                var time = Animation.Time;

                _saveEnabled = false;
                Load(pop);
                if (Animation.Clips.Any(c => c.AnimationName == animationName))
                    AnimationJSON.val = animationName;
                else
                    AnimationJSON.valNoCallback = Animation.Clips.First().AnimationName;
                AnimationModified();
                UpdateTime(time, false);
            }
            finally
            {
                _saveEnabled = true;
            }
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

            var time = Animation.Time;
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
                        display.Append($"[{f}]");
                    else
                        display.Append($" {f} ");
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
            if (Animation == null || Animation.Current == null) return;

            try
            {
                // Update UI
                ScrubberJSON.max = Animation.Current.AnimationLength;
                ScrubberJSON.valNoCallback = Animation.Time;
                TimeJSON.max = Animation.Current.AnimationLength;
                TimeJSON.valNoCallback = Animation.Time;
                AnimationJSON.choices = Animation.GetAnimationNames().ToList();
                AnimationJSON.valNoCallback = Animation.IsPlaying() ? PlayingAnimationName : Animation.Current.AnimationName;
                FilterAnimationTargetJSON.choices = new List<string> { AllTargets }.Concat(Animation.Current.GetTargetsNames()).ToList();

                // Save
                if (_saveEnabled)
                    Save();

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
            if (Animation == null || Animation.Current == null) return;

            try
            {
                var time = Animation.Time;

                // Update UI
                ScrubberJSON.valNoCallback = time;
                TimeJSON.valNoCallback = time;
                AnimationJSON.valNoCallback = Animation.IsPlaying() ? PlayingAnimationName : Animation.Current.AnimationName;

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

        private static float GetRounded(float val)
        {
            return (float)(Math.Round(val * 1000f) / 1000f);
        }

        #endregion
    }
}

