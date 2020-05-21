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
        private const int _maxDisplayedFrames = 500;
        private static readonly HashSet<string> _grabbingControllers = new HashSet<string> { "RightHandAnchor", "LeftHandAnchor", "MouseGrab", "SelectionHandles" };

        // State
        public AtomAnimation Animation { get; private set; }
        public Atom ContainingAtom => containingAtom;
        public AtomAnimationSerializer Serializer { get; private set; }
        private bool _restoring;
        public AtomClipboard Clipboard { get; } = new AtomClipboard();
        private FreeControllerAnimationTarget _grabbedController;
        private bool _cancelNextGrabbedControllerRelease;
        private bool _resumePlayOnUnfreeze;

        // Storables
        public JSONStorableStringChooser AnimationJSON { get; private set; }
        public JSONStorableStringChooser AnimationDisplayJSON { get; private set; }
        public JSONStorableAction NextAnimationJSON { get; private set; }
        public JSONStorableAction PreviousAnimationJSON { get; private set; }
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
        public JSONStorableBool LockedJSON { get; private set; }
        public JSONStorableString DisplayJSON { get; private set; }
        public JSONStorableBool AutoKeyframeAllControllersJSON { get; private set; }
        public JSONStorableFloat SpeedJSON { get; private set; }

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
                SuperController.LogError($"VamTimeline.{nameof(AtomPlugin)}.{nameof(Init)}: " + exc);
            }
        }

        #endregion

        #region Update

        public void Update()
        {
            if (Animation == null) return;

            try
            {
                Animation.Update();

                if (Animation.IsPlaying())
                {
                    var time = Animation.Time;
                    if (time != ScrubberJSON.val)
                        ScrubberJSON.valNoCallback = time;
                    if (AnimationJSON.val != Animation.Current.AnimationName)
                        AnimationJSON.valNoCallback = Animation.Current.AnimationName;

                    _ui.UpdatePlaying();

                    if (SuperController.singleton.freezeAnimation)
                    {
                        Animation.Stop();
                        Animation.Time = Animation.Time.Snap();
                        _resumePlayOnUnfreeze = true;
                    }
                }
                else
                {
                    if (_resumePlayOnUnfreeze && !SuperController.singleton.freezeAnimation)
                    {
                        _resumePlayOnUnfreeze = false;
                        Animation.Play();
                        IsPlayingJSON.valNoCallback = true;
                        AnimationFrameUpdated();
                    }
                    else if (LockedJSON != null && !LockedJSON.val)
                    {
                        UpdateNotPlaying();
                    }
                }
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AtomPlugin)}.{nameof(Update)}: " + exc);
            }
        }

        private void UpdateNotPlaying()
        {
            var sc = SuperController.singleton;
            var grabbing = sc.RightGrabbedController ?? sc.LeftGrabbedController ?? sc.RightFullGrabbedController ?? sc.LeftFullGrabbedController;
            if (grabbing != null && grabbing.containingAtom != containingAtom)
                grabbing = null;
            else if (Input.GetMouseButton(0) && grabbing == null)
                grabbing = containingAtom.freeControllers.FirstOrDefault(c => _grabbingControllers.Contains(c.linkToRB?.gameObject.name));

            if (_grabbedController == null && grabbing != null)
            {
                _grabbedController = Animation.Current.TargetControllers.FirstOrDefault(c => c.Controller == grabbing);
            }
            if (_grabbedController != null && grabbing != null)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                    _cancelNextGrabbedControllerRelease = true;
            }
            else if (_grabbedController != null && grabbing == null)
            {
                var grabbedController = _grabbedController;
                _grabbedController = null;
                if (_cancelNextGrabbedControllerRelease)
                {
                    _cancelNextGrabbedControllerRelease = false;
                    return;
                }
                // TODO: This should be done by the controller (updating the animation resets the time)
                var time = Animation.Time.Snap();
                if (AutoKeyframeAllControllersJSON.val)
                {
                    foreach (var target in Animation.Current.TargetControllers)
                        SetControllerKeyframe(time, target);
                }
                else
                {
                    SetControllerKeyframe(time, grabbedController);
                }
                Animation.RebuildAnimation();
                AnimationModified();
                if(Animation.Current.Transition) Animation.Sample();
            }
        }

        private void SetControllerKeyframe(float time, FreeControllerAnimationTarget target)
        {
            Animation.SetKeyframeToCurrentTransform(target, time);
            if (target.Settings[time.ToMilliseconds()]?.CurveType == CurveTypeValues.CopyPrevious)
                Animation.Current.ChangeCurve(time, CurveTypeValues.Smooth);
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
                SuperController.LogError($"VamTimeline.{nameof(AtomPlugin)}.{nameof(OnEnable)}: " + exc);
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
                SuperController.LogError($"VamTimeline.{nameof(AtomPlugin)}.{nameof(OnDisable)}: " + exc);
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
            AnimationDisplayJSON = new JSONStorableStringChooser(StorableNames.AnimationDisplay, new List<string>(), "", "Animation", val => ChangeAnimation(val))
            {
                isStorable = false
            };
            RegisterStringChooser(AnimationDisplayJSON);

            AnimationJSON = new JSONStorableStringChooser(StorableNames.Animation, new List<string>(), "", "Animation", val => ChangeAnimation(val))
            {
                isStorable = false
            };
            RegisterStringChooser(AnimationJSON);

            NextAnimationJSON = new JSONStorableAction(StorableNames.NextAnimation, () =>
            {
                var i = AnimationJSON.choices.IndexOf(AnimationJSON.val);
                if (i < 0 || i > AnimationJSON.choices.Count - 2) return;
                AnimationJSON.val = AnimationJSON.choices[i + 1];
            });
            RegisterAction(NextAnimationJSON);

            PreviousAnimationJSON = new JSONStorableAction(StorableNames.PreviousAnimation, () =>
            {
                var i = AnimationJSON.choices.IndexOf(AnimationJSON.val);
                if (i < 1 || i > AnimationJSON.choices.Count - 1) return;
                AnimationJSON.val = AnimationJSON.choices[i - 1];
            });
            RegisterAction(PreviousAnimationJSON);

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
                if (Animation?.Current == null)
                {
                    SuperController.LogError($"VamTimeline: Cannot play animation, Timeline is still loading");
                    return;
                }
                if (SuperController.singleton.freezeAnimation)
                {
                    _resumePlayOnUnfreeze = true;
                    return;
                }
                Animation.Play();
                IsPlayingJSON.valNoCallback = true;
                AnimationFrameUpdated();
            });
            RegisterAction(PlayJSON);

            PlayIfNotPlayingJSON = new JSONStorableAction(StorableNames.PlayIfNotPlaying, () =>
            {
                if (Animation?.Current == null)
                {
                    SuperController.LogError($"VamTimeline: Cannot play animation, Timeline is still loading");
                    return;
                }
                if (SuperController.singleton.freezeAnimation)
                {
                    _resumePlayOnUnfreeze = true;
                    return;
                }
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
                    _resumePlayOnUnfreeze = false;
                    Animation.Stop();
                    Animation.Time = Animation.Time.Snap();
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
                Animation.Time = Animation.Time.Snap();
                IsPlayingJSON.valNoCallback = false;
            });
            RegisterAction(StopIfPlayingJSON);

            FilterAnimationTargetJSON = new JSONStorableStringChooser(StorableNames.FilterAnimationTarget, new List<string> { StorableNames.AllTargets }, StorableNames.AllTargets, StorableNames.FilterAnimationTarget, val => { Animation.Current.SelectTargetByName(val == StorableNames.AllTargets ? "" : val); AnimationFrameUpdated(); })
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
                var rounded = val.Snap();
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

            LockedJSON = new JSONStorableBool(StorableNames.Locked, false, (bool val) => AnimationModified());
            RegisterBool(LockedJSON);

            DisplayJSON = new JSONStorableString(StorableNames.Display, "")
            {
                isStorable = false
            };
            RegisterString(DisplayJSON);

            AutoKeyframeAllControllersJSON = new JSONStorableBool("Auto Keyframe All Controllers", false)
            {
                isStorable = false
            };

            SpeedJSON = new JSONStorableFloat(StorableNames.Speed, 1f, v => UpdateAnimationSpeed(v), 0f, 5f, false)
            {
                isStorable = false
            };
            RegisterFloat(SpeedJSON);
        }

        private IEnumerator DeferredInit()
        {
            yield return new WaitForEndOfFrame();
            if (Animation != null)
            {
                StartAutoPlay();
                yield break;
            }
            containingAtom.RestoreFromLast(this);
            if (Animation != null)
            {
                yield break;
            }
            Animation = new AtomAnimation(containingAtom);
            Animation.Initialize();
            AnimationModified();
            AnimationFrameUpdated();
        }

        private void StartAutoPlay()
        {
            var autoPlayClip = Animation.Clips.FirstOrDefault(c => c.AutoPlay);
            if (autoPlayClip != null)
            {
                ChangeAnimation(autoPlayClip.AnimationName);
                PlayIfNotPlayingJSON.actionCallback();
            }
        }

        #endregion

        #region Load / Save

        public override JSONClass GetJSON(bool includePhysical = true, bool includeAppearance = true, bool forceStore = false)
        {
            try
            {
                Animation.Stop();
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AtomPlugin)}.{nameof(GetJSON)} (Stop): " + exc);
            }

            var json = base.GetJSON(includePhysical, includeAppearance, forceStore);

            try
            {
                json["Animation"] = GetAnimationJSON();
                needsStore = true;
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AtomPlugin)}.{nameof(GetJSON)} (Serialize): " + exc);
            }

            return json;
        }

        public JSONClass GetAnimationJSON(string animationName = null)
        {
            return Serializer.SerializeAnimation(Animation, animationName);
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
                SuperController.LogError($"VamTimeline.{nameof(AtomPlugin)}.{nameof(RestoreFromJSON)}: " + exc);
            }
        }

        public void Load(JSONNode animationJSON)
        {
            if (_restoring) return;
            _restoring = true;
            try
            {
                Animation = Serializer.DeserializeAnimation(Animation, animationJSON.AsObject);
                if (Animation == null) throw new NullReferenceException("Animation deserialized to null");

                Animation.Initialize();
                Animation.RebuildAnimation();
                AnimationModified();
                AnimationFrameUpdated();
                UpdateTime(0f, false);
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AtomPlugin)}.{nameof(Load)}: " + exc);
            }
            finally
            {
                _restoring = false;
            }
        }

        #endregion

        #region Callbacks

        public void ChangeAnimation(string animationName)
        {
            if (string.IsNullOrEmpty(animationName)) return;

            try
            {
                FilterAnimationTargetJSON.val = StorableNames.AllTargets;
                AnimationJSON.valNoCallback = Animation.Current.AnimationName;
                if (Animation.IsPlaying())
                {
                    AnimationDisplayJSON.valNoCallback = StorableNames.PlayingAnimationName;
                    if (Animation.Current.AnimationName != animationName)
                    {
                        Animation.ChangeAnimation(animationName);
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
                SuperController.LogError($"VamTimeline.{nameof(AtomPlugin)}.{nameof(ChangeAnimation)}: " + exc);
            }
        }

        private void UpdateTime(float time, bool snap)
        {
            time = time.Snap(snap ? SnapJSON.val : 0f);

            if (Animation.Current.Loop && time >= Animation.Current.AnimationLength - float.Epsilon)
                time = Animation.Current.AnimationLength - SnapJSON.val;

            Animation.Time = time;
            if (Animation.Current.AnimationPattern != null)
                Animation.Current.AnimationPattern.SetFloatParamValue("currentTime", time);

            AnimationFrameUpdated();
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
                Clipboard.Clear();
                Clipboard.Time = Animation.Time.Snap();
                Clipboard.Entries.Add(Animation.Current.Copy(Clipboard.Time));
                var time = Animation.Time.Snap();
                if (time.IsSameFrame(0f) || time.IsSameFrame(Animation.Current.AnimationLength)) return;
                Animation.Current.DeleteFrame(time);
                Animation.RebuildAnimation();
                AnimationModified();
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AtomPlugin)}.{nameof(Cut)}: " + exc);
            }
        }

        private void Copy()
        {
            try
            {
                if (Animation.IsPlaying()) return;

                Clipboard.Clear();
                Clipboard.Time = Animation.Time.Snap();
                Clipboard.Entries.Add(Animation.Current.Copy(Clipboard.Time));
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AtomPlugin)}.{nameof(Copy)}: " + exc);
            }
        }

        private void Paste()
        {
            try
            {
                if (Animation.IsPlaying()) return;

                if (Clipboard.Entries.Count == 0)
                {
                    SuperController.LogMessage("VamTimeline: Clipboard is empty");
                    return;
                }
                var time = Animation.Time;
                var timeOffset = Clipboard.Time;
                foreach (var entry in Clipboard.Entries)
                {
                    Animation.Current.Paste(Animation.Time + entry.Time - timeOffset, entry);
                }
                Animation.RebuildAnimation();
                // Sample animation now
                UpdateTime(time, false);
                AnimationModified();
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AtomPlugin)}.{nameof(Paste)}: " + exc);
            }
        }

        private void UpdateAnimationSpeed(float v)
        {
            if (v < 0) SpeedJSON.valNoCallback = v = 0f;
            Animation.Speed = v;
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

            var time = Animation.Time;
            var frames = new List<float>();
            var targets = new List<string>();
            foreach (var target in Animation.Current.GetAllOrSelectedTargets())
            {
                var keyTimes = target.GetAllKeyframesTime();
                foreach (var keyTime in keyTimes)
                {
                    frames.Add(keyTime);
                    if (frames.Count >= _maxDisplayedFrames)
                    {
                        DisplayJSON.val = "Too many frames to display";
                        return;
                    }
                    if (keyTime.IsSameFrame(time))
                        targets.Add(target.Name);
                }
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
                    if (f.IsSameFrame(time))
                        display.Append($"[{f}]");
                    else
                        display.Append($" {f} ");
                }
            }
            display.AppendLine();
            if (targets.Count == 0)
            {
                display.AppendLine($"No controller has been registered{(Animation.Current.AllTargets.Any() ? " at this frame." : ". Go to Animation Settings and add one.")}");
            }
            else
            {
                display.AppendLine("Affects:");
                foreach (var c in targets)
                    display.AppendLine(c);
            }
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
                SpeedJSON.valNoCallback = Animation.Speed;
                AnimationJSON.choices = Animation.GetAnimationNames().ToList();
                AnimationDisplayJSON.choices = AnimationJSON.choices;
                AnimationJSON.valNoCallback = Animation.Current.AnimationName;
                AnimationDisplayJSON.valNoCallback = Animation.IsPlaying() ? StorableNames.PlayingAnimationName : Animation.Current.AnimationName;
                FilterAnimationTargetJSON.choices = new List<string> { StorableNames.AllTargets }.Concat(Animation.Current.GetTargetsNames()).ToList();

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
                SuperController.LogError($"VamTimeline.{nameof(AtomPlugin)}.{nameof(AnimationModified)}: " + exc);
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
                SpeedJSON.valNoCallback = Animation.Speed;
                AnimationJSON.valNoCallback = Animation.Current.AnimationName;
                AnimationDisplayJSON.valNoCallback = Animation.IsPlaying() ? StorableNames.PlayingAnimationName : Animation.Current.AnimationName;

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
                SuperController.LogError($"VamTimeline.{nameof(AtomPlugin)}.{nameof(AnimationFrameUpdated)}: " + exc);
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

