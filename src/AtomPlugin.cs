using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SimpleJSON;
using UnityEngine;
using UnityEngine.UI;

namespace VamTimeline
{
    public class AtomPlugin : MVRScript, IAtomPlugin
    {
        private static readonly HashSet<string> _grabbingControllers = new HashSet<string> { "RightHandAnchor", "LeftHandAnchor", "MouseGrab", "SelectionHandles" };

        public AtomAnimation animation { get; private set; }
        public new Atom containingAtom => base.containingAtom;
        public new Transform UITransform => base.UITransform;
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
        public JSONStorableAction deleteJSON { get; private set; }
        public JSONStorableAction cutJSON { get; private set; }
        public JSONStorableAction copyJSON { get; private set; }
        public JSONStorableAction pasteJSON { get; private set; }
        public JSONStorableBool lockedJSON { get; private set; }
        public JSONStorableFloat speedJSON { get; private set; }

        private FreeControllerAnimationTarget _grabbedController;
        private bool _cancelNextGrabbedControllerRelease;
        private bool _restoring;
        private Editor _ui;
        private Editor _controllerInjectedUI;
        private class AnimStorableActionMap { public JSONStorableAction jsa; public string animationName; }
        private readonly List<AnimStorableActionMap> _playActions = new List<AnimStorableActionMap>();

        #region Init

        public override void Init()
        {
            base.Init();

            try
            {
                serializer = new AtomAnimationSerializer(base.containingAtom);
                InitStorables();
                StartCoroutine(DeferredInit());
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AtomPlugin)}.{nameof(Init)}: {exc}");
            }
        }

        public override void InitUI()
        {
            base.InitUI();

            try
            {
                if (UITransform == null) return;

                StartCoroutine(InitUIDeferred());
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AtomPlugin)}.{nameof(InitUI)}: {exc}");
            }
        }

        private IEnumerator InitUIDeferred()
        {
            yield return StartCoroutine(VamPrefabFactory.LoadUIAssets());

            var scriptUI = UITransform.GetComponentInChildren<MVRScriptUI>();

            var scrollRect = scriptUI.fullWidthUIContent.transform.parent.parent.parent.GetComponent<ScrollRect>();
            if (scrollRect == null)
                SuperController.LogError("VamTimeline: Scroll rect not at the expected hierarchy position");
            else
            {
                scrollRect.elasticity = 0;
                scrollRect.inertia = false;
                scrollRect.movementType = ScrollRect.MovementType.Clamped;
            }

            _ui = Editor.AddTo(scriptUI.fullWidthUIContent);
            _ui.Bind(this);
            if (animation != null) _ui.Bind(animation);
        }

        #endregion

        #region Update

        public void Update()
        {
            if (animation == null) return;

            try
            {
                if (animation.isPlaying)
                {
                    scrubberJSON.valNoCallback = animation.clipTime;
                    timeJSON.valNoCallback = animation.playTime;
                }
                else if (animation?.locked == false)
                {
                    UpdateNotPlaying();
                }
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AtomPlugin)}.{nameof(Update)}: {exc}");
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

            if (_grabbedController == null && grabbing != null && !grabbing.possessed)
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
                    animation.Sample();
                var time = animation.clipTime.Snap();
                if (animation.autoKeyframeAllControllers)
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
        }

        #endregion

        #region Lifecycle

        public void OnEnable()
        {
            try
            {
                if (animation != null) animation.enabled = true;
                if (_ui != null) _ui.enabled = true;
                if (_controllerInjectedUI == null && animation != null && base.containingAtom != null)
                    SendToControllers(nameof(IRemoteControllerPlugin.OnTimelineAnimationReady));
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AtomPlugin)}.{nameof(OnEnable)}: {exc}");
            }
        }

        public void OnDisable()
        {
            try
            {
                if (animation != null) animation.enabled = false;
                if (_ui != null) _ui.enabled = false;
                DestroyControllerPanel();
                SendToControllers(nameof(IRemoteControllerPlugin.OnTimelineAnimationDisabled));
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AtomPlugin)}.{nameof(OnDisable)}: {exc}");
            }
        }

        public void OnDestroy()
        {
            try
            {
                try { Destroy(animation); } catch (Exception exc) { SuperController.LogError($"VamTimeline.{nameof(OnDestroy)}: {exc}"); }
                try { Destroy(_ui); } catch (Exception exc) { SuperController.LogError($"VamTimeline.{nameof(OnDestroy)}: {exc}"); }
                try { DestroyControllerPanel(); } catch (Exception exc) { SuperController.LogError($"VamTimeline.{nameof(OnDestroy)}: {exc}"); }
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AtomPlugin)}.{nameof(OnDestroy)}: {exc}");
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

            scrubberJSON = new JSONStorableFloat(StorableNames.Scrubber, 0f, v => animation.clipTime = v.Snap(animation.snap), 0f, AtomAnimationClip.DefaultAnimationLength, true)
            {
                isStorable = false
            };
            RegisterFloat(scrubberJSON);

            timeJSON = new JSONStorableFloat(StorableNames.Time, 0f, v => animation.playTime = v.Snap(), 0f, float.MaxValue, true)
            {
                isStorable = false
            };
            RegisterFloat(timeJSON);

            playJSON = new JSONStorableAction(StorableNames.Play, () =>
            {
                animation?.PlayAll();
            });
            RegisterAction(playJSON);

            playIfNotPlayingJSON = new JSONStorableAction(StorableNames.PlayIfNotPlaying, () =>
            {
                if (animation == null || animation.isPlaying == true) return;
                animation.PlayAll();
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
                if (animation == null) return;
                if (animation.isPlaying)
                    animation.StopAll();
                else
                    animation.ResetAll();
            });
            RegisterAction(stopJSON);

            stopIfPlayingJSON = new JSONStorableAction(StorableNames.StopIfPlaying, () =>
            {
                if (animation == null || !animation.isPlaying) return;
                animation.StopAll();
            });
            RegisterAction(stopIfPlayingJSON);

            nextFrameJSON = new JSONStorableAction(StorableNames.NextFrame, () => NextFrame());
            RegisterAction(nextFrameJSON);

            previousFrameJSON = new JSONStorableAction(StorableNames.PreviousFrame, () => PreviousFrame());
            RegisterAction(previousFrameJSON);

            deleteJSON = new JSONStorableAction("Delete", () => Delete());
            cutJSON = new JSONStorableAction("Cut", () => Cut());
            copyJSON = new JSONStorableAction("Copy", () => Copy());
            pasteJSON = new JSONStorableAction("Paste", () => Paste());

            lockedJSON = new JSONStorableBool(StorableNames.Locked, false, (bool val) =>
            {
                if (animation == null) return;
                animation.locked = val;
            })
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
                yield return 0;
                animation.Sample();
                yield break;
            }
            animation = gameObject.AddComponent<AtomAnimation>();
            animation.Initialize();
            BindAnimation();

            yield return 0;

            animation.Sample();
        }

        private void StartAutoPlay()
        {
            foreach (var autoPlayClip in animation.clips.Where(c => c.autoPlay))
            {
                animation.PlayClip(autoPlayClip.animationName, true);
            }
        }

        #endregion

        #region Load / Save

        public override JSONClass GetJSON(bool includePhysical = true, bool includeAppearance = true, bool forceStore = false)
        {
            try
            {
                animation.StopAll();
                animation.playTime = animation.playTime.Snap(animation.snap);
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AtomPlugin)}.{nameof(GetJSON)} (Stop): {exc}");
            }

            var json = base.GetJSON(includePhysical, includeAppearance, forceStore);

            try
            {
                json["Animation"] = GetAnimationJSON();
                needsStore = true;
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AtomPlugin)}.{nameof(GetJSON)} (Serialize): {exc}");
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
                // Merge load calls RestoreFromJSON before disposing the previous version...
                if (animation != null) return;

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
                SuperController.LogError($"VamTimeline.{nameof(AtomPlugin)}.{nameof(RestoreFromJSON)}: {exc}");
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
                    Destroy(animation);
                    animation = null;
                }

                animation = gameObject.AddComponent<AtomAnimation>();
                serializer.DeserializeAnimation(animation, animationJSON.AsObject);
                if (animation == null) throw new NullReferenceException("Animation deserialized to null");
                animation.Initialize();
                BindAnimation();
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AtomPlugin)}.{nameof(Load)}: {exc}");
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
            foreach (var action in _playActions)
            {
                DeregisterAction(action.jsa);
            }
            _playActions.Clear();

            animation.onTimeChanged.AddListener(OnTimeChanged);
            animation.onClipsListChanged.AddListener(OnClipsListChanged);
            animation.onAnimationSettingsChanged.AddListener(OnAnimationParametersChanged);
            animation.onCurrentAnimationChanged.AddListener(OnCurrentAnimationChanged);
            animation.onEditorSettingsChanged.AddListener(OnEditorSettingsChanged);
            animation.onIsPlayingChanged.AddListener(OnIsPlayingChanged);

            OnClipsListChanged();
            OnAnimationParametersChanged();

            _ui?.Bind(animation);

            SendToControllers(nameof(IRemoteControllerPlugin.OnTimelineAnimationReady));
        }

        private void OnTimeChanged(AtomAnimation.TimeChangedEventArgs time)
        {
            if (base.containingAtom == null) return; // Plugin destroyed
            try
            {
                // Update UI
                scrubberJSON.valNoCallback = time.currentClipTime;
                timeJSON.valNoCallback = time.time;

                SendToControllers(nameof(IRemoteControllerPlugin.OnTimelineTimeChanged));
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AtomPlugin)}.{nameof(OnTimeChanged)}: {exc}");
            }
        }

        private void OnCurrentAnimationChanged(AtomAnimation.CurrentAnimationChangedEventArgs args)
        {
            animationJSON.valNoCallback = args.after.animationName;
            OnAnimationParametersChanged();
        }

        private void OnClipsListChanged()
        {
            try
            {
                animationJSON.choices = animation.clips.Select(c => c.animationName).ToList();
                animationJSON.valNoCallback = animation.current.animationName;

                foreach (var animName in animationJSON.choices)
                {
                    if (_playActions.Any(a => a.animationName == animName)) continue;
                    CreateAndRegisterPlayAction(animName);
                }
                if (animationJSON.choices.Count > _playActions.Count)
                {
                    foreach (var action in _playActions.ToArray())
                    {
                        if (!animationJSON.choices.Contains(action.animationName))
                            _playActions.Remove(action);
                    }
                }

                SendToControllers(nameof(IRemoteControllerPlugin.OnTimelineAnimationParametersChanged));
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AtomPlugin)}.{nameof(OnClipsListChanged)}: {exc}");
            }
        }

        private void CreateAndRegisterPlayAction(string animationName)
        {
            var jsa = new JSONStorableAction($"Play {animationName}", () =>
            {
                animation.PlayClip(animationName, true);
            });
            RegisterAction(jsa);
            _playActions.Add(new AnimStorableActionMap { animationName = animationName, jsa = jsa });
        }

        private void OnAnimationParametersChanged()
        {
            try
            {
                // Update UI
                scrubberJSON.max = animation.current.animationLength;
                scrubberJSON.valNoCallback = animation.clipTime;
                timeJSON.valNoCallback = animation.playTime;
                speedJSON.valNoCallback = animation.speed;

                SendToControllers(nameof(IRemoteControllerPlugin.OnTimelineAnimationParametersChanged));
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AtomPlugin)}.{nameof(OnAnimationParametersChanged)}: {exc}");
            }
        }

        private void OnEditorSettingsChanged(string name)
        {
            try
            {
                // Update UI
                lockedJSON.valNoCallback = animation.locked;
                speedJSON.valNoCallback = animation.speed;

                if (name == nameof(AtomAnimation.locked))
                    SendToControllers(nameof(IRemoteControllerPlugin.OnTimelineAnimationParametersChanged));
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AtomPlugin)}.{nameof(OnAnimationParametersChanged)}: {exc}");
            }
        }

        private void OnIsPlayingChanged(bool isPlaying)
        {
            isPlayingJSON.valNoCallback = isPlaying;
            SendToControllers(nameof(IRemoteControllerPlugin.OnTimelineTimeChanged));
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
                    plugin.SendMessage(methodName, this, SendMessageOptions.RequireReceiver);
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
                animation.SelectAnimation(animationName);
                animationJSON.valNoCallback = animation.current.animationName;
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AtomPlugin)}.{nameof(ChangeAnimation)}: {exc}");
            }
        }

        private void NextFrame()
        {
            animation.clipTime = animation.current.GetNextFrame(animation.clipTime);
        }

        private void PreviousFrame()
        {
            animation.clipTime = animation.current.GetPreviousFrame(animation.clipTime);
        }

        private void Delete()
        {
            try
            {
                if (animation.isPlaying) return;
                var time = animation.clipTime;
                if (time.IsSameFrame(0f) || time.IsSameFrame(animation.current.animationLength)) return;
                foreach (var target in animation.current.GetAllOrSelectedTargets())
                {
                    target.DeleteFrame(time);
                }
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AtomPlugin)}.{nameof(Delete)}: {exc}");
            }
        }

        private void Cut()
        {
            try
            {
                if (animation.isPlaying) return;
                clipboard.Clear();
                var time = animation.clipTime;
                clipboard.time = time;
                clipboard.entries.Add(animation.current.Copy(clipboard.time));
                if (time.IsSameFrame(0f) || time.IsSameFrame(animation.current.animationLength)) return;
                foreach (var target in animation.current.GetAllOrSelectedTargets())
                {
                    target.DeleteFrame(time);
                }
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AtomPlugin)}.{nameof(Cut)}: {exc}");
            }
        }

        private void Copy()
        {
            try
            {
                if (animation.isPlaying) return;

                clipboard.Clear();
                clipboard.time = animation.clipTime;
                clipboard.entries.Add(animation.current.Copy(clipboard.time));
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AtomPlugin)}.{nameof(Copy)}: {exc}");
            }
        }

        private void Paste()
        {
            try
            {
                if (animation.isPlaying) return;

                if (clipboard.entries.Count == 0)
                {
                    SuperController.LogMessage("VamTimeline: Clipboard is empty");
                    return;
                }
                var time = animation.clipTime;
                var timeOffset = clipboard.time;
                foreach (var entry in clipboard.entries)
                {
                    animation.current.Paste(animation.clipTime + entry.time - timeOffset, entry);
                }
                animation.Sample();
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AtomPlugin)}.{nameof(Paste)}: {exc}");
            }
        }

        private void UpdateAnimationSpeed(float v)
        {
            if (v < 0) speedJSON.valNoCallback = v = 0f;
            animation.speed = v;
        }

        #endregion

        #region Controller integration

        public void VamTimelineConnectController(Dictionary<string, object> dict)
        {
            var proxy = SyncProxy.Wrap(dict);
            // TODO: This or just use the storables dict already on storable??
            proxy.animation = animationJSON;
            proxy.isPlaying = isPlayingJSON;
            proxy.locked = lockedJSON;
            proxy.nextFrame = nextFrameJSON;
            proxy.play = playJSON;
            proxy.playIfNotPlaying = playIfNotPlayingJSON;
            proxy.previousFrame = previousFrameJSON;
            proxy.stop = stopJSON;
            proxy.time = timeJSON;
            proxy.connected = true;
        }

        public void VamTimelineRequestControlPanel(GameObject container)
        {
            StartCoroutine(InjectControlPanelDeferred(container));
        }

        private IEnumerator InjectControlPanelDeferred(GameObject container)
        {
            while (_ui == null && container != null) { yield return 0; }

            if (container == null) yield break;

            _controllerInjectedUI = container.GetComponent<Editor>();
            if (_controllerInjectedUI == null)
            {
                _controllerInjectedUI = Editor.Configure(container);
                _controllerInjectedUI.Bind(this);
            }
            if (_controllerInjectedUI.animation != animation)
                _controllerInjectedUI.Bind(animation);
        }

        private void DestroyControllerPanel()
        {
            if (_controllerInjectedUI == null) return;
            _controllerInjectedUI.gameObject.transform.SetParent(null, false);
            Destroy(_controllerInjectedUI.gameObject);
            _controllerInjectedUI = null;
        }

        #endregion
    }
}

