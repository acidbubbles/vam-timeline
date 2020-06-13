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

        public AtomAnimation animation { get; private set; }
        public new Atom containingAtom => base.containingAtom;
        public new MVRPluginManager manager => base.manager;
        public AtomAnimationSerializer serializer { get; private set; }
        public AtomClipboard clipboard { get; } = new AtomClipboard();

        public JSONStorableStringChooser animationJSON { get; private set; }
        public JSONStorableAction nextAnimationJSON { get; private set; }
        public JSONStorableAction previousAnimationJSON { get; private set; }
        public JSONStorableFloat scrubberJSON { get; private set; }
        public JSONStorableFloat timeJSON { get; private set; }
        public JSONStorableAction playJSON { get; private set; }
        public JSONStorableBool isPlayingJSON { get; private set; }
        public JSONStorableAction playIfNotPlayingJSON { get; private set; }
        public JSONStorableAction stopJSON { get; private set; }
        public JSONStorableAction stopIfPlayingJSON { get; private set; }
        public JSONStorableAction nextFrameJSON { get; private set; }
        public JSONStorableAction previousFrameJSON { get; private set; }
        public JSONStorableFloat snapJSON { get; private set; }
        public JSONStorableAction cutJSON { get; private set; }
        public JSONStorableAction copyJSON { get; private set; }
        public JSONStorableAction pasteJSON { get; private set; }
        public JSONStorableBool lockedJSON { get; private set; }
        public JSONStorableBool autoKeyframeAllControllersJSON { get; private set; }
        public JSONStorableFloat speedJSON { get; private set; }

        private FreeControllerAnimationTarget _grabbedController;
        private bool _cancelNextGrabbedControllerRelease;
        private bool _resumePlayOnUnfreeze;
        private bool _animationRebuildRequestPending;
        private bool _animationRebuildInProgress;
        private bool _sampleAfterRebuild;
        private bool _restoring;
        private ScreensManager _ui;
        private AnimationControlPanel _controllerInjectedControlerPanel;

        #region Init

        public override void Init()
        {
            base.Init();

            try
            {
                serializer = new AtomAnimationSerializer(base.containingAtom);
                _ui = new ScreensManager(this);
                InitStorables();
                StartCoroutine(DeferredInit());
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AtomPlugin)}.{nameof(Init)}: " + exc);
            }
        }

        public override void InitUI()
        {
            base.InitUI();

            try
            {
                _ui?.Init();
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AtomPlugin)}.{nameof(InitUI)}: " + exc);
            }
        }

        #endregion

        #region Update

        public void Update()
        {
            if (animation == null) return;

            try
            {
                animation.Update();

                if (animation.IsPlaying())
                {
                    var time = animation.time;
                    if (time != scrubberJSON.val)
                        scrubberJSON.valNoCallback = time;
                    if (animationJSON.val != animation.current.animationName)
                        animationJSON.valNoCallback = animation.current.animationName;

                    if (SuperController.singleton.freezeAnimation)
                    {
                        animation.Stop();
                        _resumePlayOnUnfreeze = true;
                    }
                }
                else
                {
                    if (_resumePlayOnUnfreeze && !SuperController.singleton.freezeAnimation)
                    {
                        _resumePlayOnUnfreeze = false;
                        animation.Play();
                        SendToControllers(nameof(IAnimationController.OnTimelineTimeChanged));
                        isPlayingJSON.valNoCallback = true;
                    }
                    else if (lockedJSON != null && !lockedJSON.val)
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
            if (grabbing != null && grabbing.containingAtom != base.containingAtom)
                grabbing = null;
            else if (Input.GetMouseButton(0) && grabbing == null)
                grabbing = base.containingAtom.freeControllers.FirstOrDefault(c => _grabbingControllers.Contains(c.linkToRB?.gameObject.name));

            if (_grabbedController == null && grabbing != null)
            {
                _grabbedController = animation.current.targetControllers.FirstOrDefault(c => c.controller == grabbing);
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
                if (animation.current.transition)
                    SampleAfterRebuild();
                var time = animation.time.Snap();
                if (autoKeyframeAllControllersJSON.val)
                {
                    foreach (var target in animation.current.targetControllers)
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
            animation.SetKeyframeToCurrentTransform(target, time);
            if (target.settings[time.ToMilliseconds()]?.curveType == CurveTypeValues.CopyPrevious)
                animation.current.ChangeCurve(time, CurveTypeValues.Smooth);
        }

        #endregion

        #region Lifecycle

        public void OnEnable()
        {
            try
            {
                _ui?.Enable();
                if (_controllerInjectedControlerPanel == null && animation != null && base.containingAtom != null)
                    SendToControllers(nameof(IAnimationController.OnTimelineAnimationReady));
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
                if (animation?.IsPlaying() ?? false)
                    animation.Stop();
                _ui?.Disable();
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
                animation?.Dispose();
                _ui?.Dispose();
                DestroyControllerPanel();
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
            animationJSON = new JSONStorableStringChooser(StorableNames.Animation, new List<string>(), "", "Animation", val => ChangeAnimation(val))
            {
                isStorable = false
            };
            RegisterStringChooser(animationJSON);

            nextAnimationJSON = new JSONStorableAction(StorableNames.NextAnimation, () =>
            {
                var i = animationJSON.choices.IndexOf(animationJSON.val);
                if (i < 0 || i > animationJSON.choices.Count - 2) return;
                animationJSON.val = animationJSON.choices[i + 1];
            });
            RegisterAction(nextAnimationJSON);

            previousAnimationJSON = new JSONStorableAction(StorableNames.PreviousAnimation, () =>
            {
                var i = animationJSON.choices.IndexOf(animationJSON.val);
                if (i < 1 || i > animationJSON.choices.Count - 1) return;
                animationJSON.val = animationJSON.choices[i - 1];
            });
            RegisterAction(previousAnimationJSON);

            scrubberJSON = new JSONStorableFloat(StorableNames.Scrubber, 0f, v => UpdateTime(v, true), 0f, AtomAnimationClip.DefaultAnimationLength, true)
            {
                isStorable = false
            };
            RegisterFloat(scrubberJSON);

            timeJSON = new JSONStorableFloat(StorableNames.Time, 0f, v => UpdateTime(v, false), 0f, AtomAnimationClip.DefaultAnimationLength, true)
            {
                isStorable = false
            };
            RegisterFloat(timeJSON);

            playJSON = new JSONStorableAction(StorableNames.Play, () =>
            {
                if (animation?.current == null)
                {
                    SuperController.LogError($"VamTimeline: Cannot play animation, Timeline is still loading");
                    return;
                }
                if (SuperController.singleton.freezeAnimation)
                {
                    _resumePlayOnUnfreeze = true;
                    return;
                }
                animation.Play();
                isPlayingJSON.valNoCallback = true;
                SendToControllers(nameof(IAnimationController.OnTimelineTimeChanged));
            });
            RegisterAction(playJSON);

            playIfNotPlayingJSON = new JSONStorableAction(StorableNames.PlayIfNotPlaying, () =>
            {
                if (animation?.current == null)
                {
                    SuperController.LogError($"VamTimeline: Cannot play animation, Timeline is still loading");
                    return;
                }
                if (SuperController.singleton.freezeAnimation)
                {
                    _resumePlayOnUnfreeze = true;
                    return;
                }
                if (animation.IsPlaying()) return;
                animation.Play();
                isPlayingJSON.valNoCallback = true;
                SendToControllers(nameof(IAnimationController.OnTimelineTimeChanged));
            });
            RegisterAction(playIfNotPlayingJSON);

            isPlayingJSON = new JSONStorableBool(StorableNames.IsPlaying, false, (bool val) =>
            {
                if (val)
                    playIfNotPlayingJSON.actionCallback();
                else
                    stopJSON.actionCallback();
            })
            {
                isStorable = false
            };
            RegisterBool(isPlayingJSON);

            stopJSON = new JSONStorableAction(StorableNames.Stop, () =>
            {
                if (animation.IsPlaying())
                {
                    _resumePlayOnUnfreeze = false;
                    animation.Stop();
                    animation.time = animation.time.Snap(snapJSON.val);
                    isPlayingJSON.valNoCallback = false;
                    SendToControllers(nameof(IAnimationController.OnTimelineTimeChanged));
                }
                else
                {
                    animation.time = 0f;
                }
            });
            RegisterAction(stopJSON);

            stopIfPlayingJSON = new JSONStorableAction(StorableNames.StopIfPlaying, () =>
            {
                if (!animation.IsPlaying()) return;
                animation.Stop();
                animation.time = animation.time.Snap(snapJSON.val);
                isPlayingJSON.valNoCallback = false;
                SendToControllers(nameof(IAnimationController.OnTimelineTimeChanged));
            });
            RegisterAction(stopIfPlayingJSON);

            nextFrameJSON = new JSONStorableAction(StorableNames.NextFrame, () => NextFrame());
            RegisterAction(nextFrameJSON);

            previousFrameJSON = new JSONStorableAction(StorableNames.PreviousFrame, () => PreviousFrame());
            RegisterAction(previousFrameJSON);

            snapJSON = new JSONStorableFloat(StorableNames.Snap, 0.01f, (float val) =>
            {
                var rounded = val.Snap();
                if (val != rounded)
                    snapJSON.valNoCallback = rounded;
                if (animation != null && animation.time % rounded != 0)
                    UpdateTime(animation.time, true);
            }, 0.001f, 1f, true)
            {
                isStorable = true
            };
            RegisterFloat(snapJSON);

            cutJSON = new JSONStorableAction("Cut", () => Cut());
            copyJSON = new JSONStorableAction("Copy", () => Copy());
            pasteJSON = new JSONStorableAction("Paste", () => Paste());

            lockedJSON = new JSONStorableBool(StorableNames.Locked, false, (bool val) =>
            {
                _ui.UpdateLocked(val);
                if (_controllerInjectedControlerPanel != null)
                    _controllerInjectedControlerPanel.locked = val;
            });
            RegisterBool(lockedJSON);

            autoKeyframeAllControllersJSON = new JSONStorableBool("Auto Keyframe All Controllers", false)
            {
                isStorable = false
            };

            speedJSON = new JSONStorableFloat(StorableNames.Speed, 1f, v => UpdateAnimationSpeed(v), 0f, 5f, false)
            {
                isStorable = false
            };
            RegisterFloat(speedJSON);
        }

        private IEnumerator DeferredInit()
        {
            yield return new WaitForEndOfFrame();
            if (animation != null)
            {
                StartAutoPlay();
                yield break;
            }
            base.containingAtom.RestoreFromLast(this);
            if (animation != null)
            {
                yield break;
            }
            animation = new AtomAnimation(base.containingAtom);
            animation.Initialize();
            BindAnimation();
        }

        private void StartAutoPlay()
        {
            var autoPlayClip = animation.clips.FirstOrDefault(c => c.autoPlay);
            if (autoPlayClip != null)
            {
                ChangeAnimation(autoPlayClip.animationName);
                playIfNotPlayingJSON.actionCallback();
            }
        }

        #endregion

        #region Load / Save

        public override JSONClass GetJSON(bool includePhysical = true, bool includeAppearance = true, bool forceStore = false)
        {
            try
            {
                animation.Stop();
                animation.time = animation.time.Snap(snapJSON.val);
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
            return serializer.SerializeAnimation(animation, animationName);
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
                if (animation != null)
                {
                    animation.Dispose();
                    animation = null;
                }

                animation = serializer.DeserializeAnimation(animation, animationJSON.AsObject);
                if (animation == null) throw new NullReferenceException("Animation deserialized to null");
                animation.Initialize();
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
            animation.onTimeChanged.AddListener(OnTimeChanged);
            animation.onAnimationRebuildRequested.AddListener(OnAnimationRebuildRequested);
            animation.onAnimationSettingsChanged.AddListener(OnAnimationParametersChanged);
            animation.onCurrentAnimationChanged.AddListener(OnCurrentAnimationChanged);
            animation.onClipsListChanged.AddListener(OnAnimationParametersChanged);

            OnAnimationParametersChanged();
            SampleAfterRebuild();

            _ui.Bind(animation);

            SendToControllers(nameof(IAnimationController.OnTimelineAnimationReady));
        }

        private void OnTimeChanged(float time)
        {
            if (base.containingAtom == null) return; // Plugin destroyed
            try
            {
                // Update UI
                scrubberJSON.valNoCallback = time;
                timeJSON.valNoCallback = time;

                SendToControllers(nameof(IAnimationController.OnTimelineTimeChanged));
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AtomPlugin)}.{nameof(OnTimeChanged)}: " + exc);
            }
        }

        public void SampleAfterRebuild()
        {
            _sampleAfterRebuild = true;
        }

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
                animation.RebuildAnimation();
                if (_sampleAfterRebuild)
                {
                    _sampleAfterRebuild = false;
                    animation.Sample();
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
            animationJSON.valNoCallback = animation.current.animationName;
            OnAnimationParametersChanged();
        }

        private void OnAnimationParametersChanged()
        {
            try
            {
                // Update UI
                scrubberJSON.max = animation.current.animationLength;
                scrubberJSON.valNoCallback = animation.time;
                timeJSON.max = animation.current.animationLength;
                timeJSON.valNoCallback = animation.time;
                speedJSON.valNoCallback = animation.speed;
                animationJSON.choices = animation.GetAnimationNames();
                animationJSON.valNoCallback = animation.current.animationName;

                SendToControllers(nameof(IAnimationController.OnTimelineAnimationParametersChanged));

                OnTimeChanged(animation.time);
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AtomPlugin)}.{nameof(OnAnimationParametersChanged)}: " + exc);
            }
        }

        private void SendToControllers(string methodName)
        {
            var externalControllers = SuperController.singleton.GetAtoms().Where(a => a.type == "SimpleSign");
            foreach (var controller in externalControllers)
            {
                var pluginId = controller.GetStorableIDs().FirstOrDefault(id => id.EndsWith("VamTimeline.ControllerPlugin"));
                if (pluginId != null)
                {
                    var plugin = controller.GetStorableByID(pluginId);
                    plugin.SendMessage(methodName, base.containingAtom.uid);
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
                animationJSON.valNoCallback = animation.current.animationName;
                if (animation.current.animationName != animationName)
                {
                    animation.ChangeAnimation(animationName);
                }
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AtomPlugin)}.{nameof(ChangeAnimation)}: " + exc);
            }
        }

        private void UpdateTime(float time, bool snap)
        {
            time = time.Snap(snap ? snapJSON.val : 0f);

            if (animation.current.loop && time >= animation.current.animationLength - float.Epsilon)
                time = 0f;

            animation.time = time;
            if (animation.current.animationPattern != null)
                animation.current.animationPattern.SetFloatParamValue("currentTime", time);
        }

        private void NextFrame()
        {
            var originalTime = animation.time;
            var time = animation.current.GetNextFrame(animation.time);
            UpdateTime(time, false);
        }

        private void PreviousFrame()
        {
            var time = animation.current.GetPreviousFrame(animation.time);
            UpdateTime(time, false);
        }

        private void Cut()
        {
            try
            {
                if (animation.IsPlaying()) return;
                clipboard.Clear();
                clipboard.time = animation.time.Snap();
                clipboard.entries.Add(animation.current.Copy(clipboard.time));
                var time = animation.time.Snap();
                if (time.IsSameFrame(0f) || time.IsSameFrame(animation.current.animationLength)) return;
                animation.current.DeleteFrame(time);
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
                if (animation.IsPlaying()) return;

                clipboard.Clear();
                clipboard.time = animation.time.Snap();
                clipboard.entries.Add(animation.current.Copy(clipboard.time));
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
                if (animation.IsPlaying()) return;

                if (clipboard.entries.Count == 0)
                {
                    SuperController.LogMessage("VamTimeline: Clipboard is empty");
                    return;
                }
                var time = animation.time;
                var timeOffset = clipboard.time;
                foreach (var entry in clipboard.entries)
                {
                    animation.current.Paste(animation.time + entry.time - timeOffset, entry);
                }
                SampleAfterRebuild();
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AtomPlugin)}.{nameof(Paste)}: " + exc);
            }
        }

        private void UpdateAnimationSpeed(float v)
        {
            if (v < 0) speedJSON.valNoCallback = v = 0f;
            animation.speed = v;
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
            _controllerInjectedControlerPanel = container.GetComponent<AnimationControlPanel>();
            if (_controllerInjectedControlerPanel == null)
            {
                _controllerInjectedControlerPanel = container.AddComponent<AnimationControlPanel>();
                _controllerInjectedControlerPanel.Bind(this);
            }
            _controllerInjectedControlerPanel.Bind(animation);
        }

        private void DestroyControllerPanel()
        {
            if (_controllerInjectedControlerPanel == null) return;
            _controllerInjectedControlerPanel.gameObject.transform.SetParent(null, false);
            Destroy(_controllerInjectedControlerPanel.gameObject);
            _controllerInjectedControlerPanel = null;
        }

        #endregion
    }
}

