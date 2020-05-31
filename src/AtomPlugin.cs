using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        private static readonly HashSet<string> _grabbingControllers = new HashSet<string> { "RightHandAnchor", "LeftHandAnchor", "MouseGrab", "SelectionHandles" };

        // State
        public AtomAnimation Animation { get; private set; }
        public Atom ContainingAtom => containingAtom;
        public MVRPluginManager Manager => manager;
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
        public JSONStorableAction NextFrameJSON { get; private set; }
        public JSONStorableAction PreviousFrameJSON { get; private set; }
        public JSONStorableFloat SnapJSON { get; private set; }
        public JSONStorableAction CutJSON { get; private set; }
        public JSONStorableAction CopyJSON { get; private set; }
        public JSONStorableAction PasteJSON { get; private set; }
        public JSONStorableBool LockedJSON { get; private set; }
        public JSONStorableBool AutoKeyframeAllControllersJSON { get; private set; }
        public JSONStorableFloat SpeedJSON { get; private set; }

        // UI
        private ScreensManager _ui;
        private AnimationControlPanel _controlPanel;

        #region Init

        public override void Init()
        {
            try
            {
                Serializer = new AtomAnimationSerializer(containingAtom);
                _ui = new ScreensManager(this);
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
                if (Animation.Current.Transition) _sampleAfterRebuild = true;
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
                // TODO: Won't re-attach controller panel after disable
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
                Animation.Time = Animation.Time.Snap(SnapJSON.val);
                _ui.Dispose();
                DestroyControllerPanel();
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AtomPlugin)}.{nameof(OnDisable)}: " + exc);
            }
        }

        public void OnDestroy()
        {
            try
            {
                Animation?.Dispose();
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AtomPlugin)}.{nameof(OnDestroy)}: " + exc);
            }
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
                    Animation.Time = Animation.Time.Snap(SnapJSON.val);
                    IsPlayingJSON.valNoCallback = false;
                }
                else
                {
                    Animation.Time = 0f;
                }
            });
            RegisterAction(StopJSON);

            StopIfPlayingJSON = new JSONStorableAction(StorableNames.StopIfPlaying, () =>
            {
                if (!Animation.IsPlaying()) return;
                Animation.Stop();
                Animation.Time = Animation.Time.Snap(SnapJSON.val);
                IsPlayingJSON.valNoCallback = false;
            });
            RegisterAction(StopIfPlayingJSON);

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

            LockedJSON = new JSONStorableBool(StorableNames.Locked, false, (bool val) =>
            {
                _ui.UpdateLocked(val);
            });
            RegisterBool(LockedJSON);

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
            BindAnimation();
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
                Animation.Time = Animation.Time.Snap(SnapJSON.val);
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
                if (Animation != null) Animation.Dispose();

                Animation = Serializer.DeserializeAnimation(Animation, animationJSON.AsObject);
                if (Animation == null) throw new NullReferenceException("Animation deserialized to null");
                Animation.Initialize();
                BindAnimation();
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

        #region Animation Events

        private void BindAnimation()
        {
            Animation.TimeChanged.AddListener(OnTimeChanged);
            Animation.AnimationRebuildRequested.AddListener(OnAnimationRebuildRequested);
            Animation.AnimationSettingsChanged.AddListener(OnAnimationParametersChanged);
            Animation.CurrentAnimationChanged.AddListener(OnCurrentAnimationChanged);
            Animation.ClipsListChanged.AddListener(OnAnimationParametersChanged);

            OnAnimationParametersChanged();
            Animation.Sample();

            _ui.Bind(Animation);

            BroadcastToControllers(nameof(IAnimationController.VamTimelineAnimationReady));
        }

        private void OnTimeChanged(float time)
        {
            if (containingAtom == null) return; // Plugin destroyed
            try
            {
                // Update UI
                ScrubberJSON.valNoCallback = time;
                TimeJSON.valNoCallback = time;
                SpeedJSON.valNoCallback = Animation.Speed;
                AnimationJSON.valNoCallback = Animation.Current.AnimationName;
                AnimationDisplayJSON.valNoCallback = Animation.IsPlaying() ? StorableNames.PlayingAnimationName : Animation.Current.AnimationName;

                BroadcastToControllers(nameof(IAnimationController.VamTimelineAnimationFrameUpdated));
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AtomPlugin)}.{nameof(OnTimeChanged)}: " + exc);
            }
        }

        private bool _animationRebuildRequestPending;
        private bool _animationRebuildInProgress;
        private bool _sampleAfterRebuild;

        private void OnAnimationRebuildRequested()
        {
            if (_animationRebuildInProgress) throw new InvalidOperationException($"A rebuild is already in progress. This is usually caused by by RebuildAnimation triggering dirty (internal error).");
            if (_animationRebuildRequestPending) return;
            _animationRebuildRequestPending = true;
            StartCoroutine(ProcessAnimationRebuildRequest());
        }
        private IEnumerator ProcessAnimationRebuildRequest()
        {
            yield return new WaitForEndOfFrame();
            _animationRebuildRequestPending = false;
            try
            {
                _animationRebuildInProgress = true;
                Animation.RebuildAnimation();
                if (_sampleAfterRebuild)
                {
                    _sampleAfterRebuild = false;
                    Animation.Sample();
                }
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AtomPlugin)}.{nameof(ProcessAnimationRebuildRequest)}: " + exc);
            }
            finally
            {
                _animationRebuildInProgress = false;
            }
        }

        private void OnCurrentAnimationChanged(AtomAnimation.CurrentAnimationChangedEventArgs args)
        {
            OnAnimationParametersChanged();
        }

        private void OnAnimationParametersChanged()
        {
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

                BroadcastToControllers(nameof(IAnimationController.VamTimelineAnimationModified));

                OnTimeChanged(Animation.Time);
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AtomPlugin)}.{nameof(OnAnimationParametersChanged)}: " + exc);
            }
        }

        private void BroadcastToControllers(string methodName)
        {
            var externalControllers = SuperController.singleton.GetAtoms().Where(a => a.type == "SimpleSign");
            foreach (var controller in externalControllers)
            {
                var pluginId = controller.GetStorableIDs().FirstOrDefault(id => id.EndsWith("VamTimeline.ControllerPlugin"));
                if (pluginId != null)
                {
                    var plugin = controller.GetStorableByID(pluginId);
                    plugin.BroadcastMessage(methodName, containingAtom.uid);
                }
            }
        }

        #endregion

        #region Callbacks

        public void ChangeAnimation(string animationName)
        {
            if (string.IsNullOrEmpty(animationName)) return;

            try
            {
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
                time = 0f;

            Animation.Time = time;
            if (Animation.Current.AnimationPattern != null)
                Animation.Current.AnimationPattern.SetFloatParamValue("currentTime", time);
        }

        private void NextFrame()
        {
            var originalTime = Animation.Time;
            var time = Animation.Current.GetNextFrame(Animation.Time);
            UpdateTime(time, false);
        }

        private void PreviousFrame()
        {
            var time = Animation.Current.GetPreviousFrame(Animation.Time);
            UpdateTime(time, false);
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
                Animation.Sample();
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

        #region Controller integration

        public void VamTimelineRequestControlPanelInjection(GameObject container)
        {
            _controlPanel = container.GetComponent<AnimationControlPanel>();
            if (_controlPanel == null)
            {
                _controlPanel = container.AddComponent<AnimationControlPanel>();
                _controlPanel.Bind(this);
            }
            _controlPanel.Bind(Animation);
        }

        private void DestroyControllerPanel()
        {
            if (_controlPanel == null) return;
            _controlPanel.gameObject.transform.SetParent(null, false);
            Destroy(_controlPanel.gameObject);
            _controlPanel = null;
        }

        #endregion
    }
}

