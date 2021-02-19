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
        public AtomAnimation animation { get; private set; }
        public AtomAnimationEditContext animationEditContext { get; private set; }
        public new Atom containingAtom => base.containingAtom;
        public new Transform UITransform => base.UITransform;
        public new MVRPluginManager manager => base.manager;
        public AtomAnimationSerializer serializer { get; private set; }

        public Editor ui { get; private set; }
        public Editor controllerInjectedUI { get; private set; }
        public PeerManager peers { get; private set; }

        public JSONStorableStringChooser animationLegacyJSON { get; private set; }
        public JSONStorableAction nextAnimationLegacyJSON { get; private set; }
        public JSONStorableAction previousAnimationLegacyJSON { get; private set; }
        public JSONStorableFloat scrubberJSON { get; private set; }
        public JSONStorableFloat timeJSON { get; private set; }
        public JSONStorableAction playJSON { get; private set; }
        public JSONStorableBool isPlayingJSON { get; private set; }
        public JSONStorableAction playIfNotPlayingJSON { get; private set; }
        public JSONStorableAction stopJSON { get; private set; }
        public JSONStorableAction stopIfPlayingJSON { get; private set; }
        public JSONStorableAction stopAndResetJSON { get; private set; }
        public JSONStorableAction nextFrameJSON { get; private set; }
        public JSONStorableAction previousFrameJSON { get; private set; }
        public JSONStorableAction deleteJSON { get; private set; }
        public JSONStorableAction cutJSON { get; private set; }
        public JSONStorableAction copyJSON { get; private set; }
        public JSONStorableAction pasteJSON { get; private set; }
        public JSONStorableFloat speedJSON { get; private set; }
        public JSONStorableBool lockedJSON { get; private set; }
        public JSONStorableBool pausedJSON { get; private set; }

        private JSONStorableFloat _scrubberAnalogControlJSON;
        private bool _scrubbing = false;

        private bool _restoring;
        private FreeControllerV3Hook _freeControllerHook;

        private class AnimStorableActionMap
        {
            public string animationName;
            public JSONStorableAction playJSON;
            public JSONStorableFloat speedJSON;
            public JSONStorableFloat weightJSON;
        }
        private readonly List<AnimStorableActionMap> _clipStorables = new List<AnimStorableActionMap>();

        #region Init

        public override void Init()
        {
            base.Init();

            try
            {
                serializer = new AtomAnimationSerializer(base.containingAtom);
                peers = new PeerManager(base.containingAtom, this);
                _freeControllerHook = gameObject.AddComponent<FreeControllerV3Hook>();
                _freeControllerHook.enabled = false;
                _freeControllerHook.containingAtom = base.containingAtom;
                InitStorables();
                SuperController.singleton.StartCoroutine(DeferredInit());
            }
            catch (Exception exc)
            {
                SuperController.LogError($"Timeline.{nameof(AtomPlugin)}.{nameof(Init)}: {exc}");
            }
        }

        public override void InitUI()
        {
            base.InitUI();

            try
            {
                if (UITransform == null) return;

                SuperController.singleton.StartCoroutine(InitUIDeferred());
            }
            catch (Exception exc)
            {
                SuperController.LogError($"Timeline.{nameof(AtomPlugin)}.{nameof(InitUI)}: {exc}");
            }
        }

        private IEnumerator InitUIDeferred()
        {
            if (ui != null || this == null) yield break;
            yield return SuperController.singleton.StartCoroutine(VamPrefabFactory.LoadUIAssets());
            if (this == null) yield break;

            var scriptUI = UITransform.GetComponentInChildren<MVRScriptUI>();

            var scrollRect = scriptUI.fullWidthUIContent.transform.parent.parent.parent.GetComponent<ScrollRect>();
            if (scrollRect == null)
                SuperController.LogError("Timeline: Scroll rect not at the expected hierarchy position");
            else
            {
                scrollRect.elasticity = 0;
                scrollRect.inertia = false;
                scrollRect.movementType = ScrollRect.MovementType.Clamped;
            }

            ui = Editor.AddTo(scriptUI.fullWidthUIContent);
            ui.popupParent = UITransform;
            ui.Bind(this);
            if (animationEditContext != null) ui.Bind(animationEditContext);
            ui.screensManager.onScreenChanged.AddListener(args =>
            {
                if (controllerInjectedUI != null) controllerInjectedUI.screensManager.ChangeScreen(args.screenName, args.screenArg);
                peers.SendScreen(args.screenName, args.screenArg);
            });
        }

        #endregion

        #region Update

        public void Update()
        {
            if (ReferenceEquals(animation, null)) return;
            if (animation.isPlaying)
            {
                scrubberJSON.valNoCallback = animationEditContext.clipTime;
                timeJSON.valNoCallback = animation.playTime;
            }

            if (_scrubberAnalogControlJSON.val != 0)
            {
                _scrubbing = true;
                animationEditContext.clipTime += _scrubberAnalogControlJSON.val * Time.unscaledDeltaTime;
            }
            else if (_scrubbing)
            {
                animationEditContext.clipTime = animationEditContext.clipTime.Snap(animationEditContext.snap);
                _scrubbing = false;
            }
        }

        #endregion

        #region Lifecycle

        public void OnEnable()
        {
            try
            {
                if (ui != null)
                    ui.enabled = true;
                if (animation != null)
                {
                    animation.enabled = true;
                }
                if (animationEditContext != null)
                {
                    animationEditContext.enabled = true;
                    if (_freeControllerHook != null)
                        _freeControllerHook.enabled = !animation.isPlaying;
                    if (base.containingAtom != null)
                    {
                        peers.Ready();
                        BroadcastToControllers(nameof(IRemoteControllerPlugin.OnTimelineAnimationReady));
                    }
                }
            }
            catch (Exception exc)
            {
                SuperController.LogError($"Timeline.{nameof(AtomPlugin)}.{nameof(OnEnable)}: {exc}");
            }
        }

        public void OnDisable()
        {
            try
            {
                if (animation != null) animation.enabled = false;
                if (animationEditContext != null) animationEditContext.enabled = false;
                if (ui != null) ui.enabled = false;
                if (_freeControllerHook != null) _freeControllerHook.enabled = false;
                peers?.Unready();
                DestroyControllerPanel();
                BroadcastToControllers(nameof(IRemoteControllerPlugin.OnTimelineAnimationDisabled));
            }
            catch (Exception exc)
            {
                SuperController.LogError($"Timeline.{nameof(AtomPlugin)}.{nameof(OnDisable)}: {exc}");
            }
        }

        public void OnDestroy()
        {
            try
            {
                try { Destroy(animation); } catch (Exception exc) { SuperController.LogError($"Timeline.{nameof(OnDestroy)} [animations]: {exc}"); }
                try { Destroy(ui); } catch (Exception exc) { SuperController.LogError($"Timeline.{nameof(OnDestroy)} [ui]: {exc}"); }
                try { DestroyControllerPanel(); } catch (Exception exc) { SuperController.LogError($"Timeline.{nameof(OnDestroy)} [panel]: {exc}"); }
                Destroy(_freeControllerHook);
                SuperController.singleton.BroadcastMessage("OnActionsProviderDestroyed", this, SendMessageOptions.DontRequireReceiver);
            }
            catch (Exception exc)
            {
                SuperController.LogError($"Timeline.{nameof(AtomPlugin)}.{nameof(OnDestroy)}: {exc}");
            }
        }

        #endregion

        #region Initialization

        public void InitStorables()
        {
            animationLegacyJSON = new JSONStorableStringChooser(StorableNames.Animation, new List<string>(), "", "Animation", ChangeAnimationLegacy)
            {
                isStorable = false,
                isRestorable = false
            };
            RegisterStringChooser(animationLegacyJSON);

            nextAnimationLegacyJSON = new JSONStorableAction(StorableNames.NextAnimation, () =>
            {
                if (animationLegacyJSON.choices.Count < 2) return;
                var clip = string.IsNullOrEmpty(animationLegacyJSON.val)
                    ? animation.clips[0]
                    : animation.clips.First(c => c.animationName == animationLegacyJSON.val);
                var inLayer = animation.index.ByLayer(clip.animationLayer);
                var i = inLayer.IndexOf(clip);
                if (i < 0 || i > inLayer.Count - 2)
                    animationLegacyJSON.val = inLayer[0].animationName;
                else
                    animationLegacyJSON.val = inLayer[i + 1].animationName;
            });
            RegisterAction(nextAnimationLegacyJSON);

            previousAnimationLegacyJSON = new JSONStorableAction(StorableNames.PreviousAnimation, () =>
            {
                if (animationLegacyJSON.choices.Count < 2) return;
                var clip = string.IsNullOrEmpty(animationLegacyJSON.val)
                    ? animation.clips[0]
                    : animation.clips.First(c => c.animationName == animationLegacyJSON.val);
                var inLayer = animation.index.ByLayer(clip.animationLayer);
                var i = inLayer.IndexOf(clip);
                if (i < 1 || i > inLayer.Count - 1)
                    animationLegacyJSON.val = inLayer[inLayer.Count - 1].animationName;
                else
                    animationLegacyJSON.val = inLayer[i - 1].animationName;
            });
            RegisterAction(previousAnimationLegacyJSON);

            scrubberJSON = new JSONStorableFloat(StorableNames.Scrubber, 0f, v => animationEditContext.clipTime = v.Snap(animationEditContext.snap), 0f, AtomAnimationClip.DefaultAnimationLength)
            {
                isStorable = false,
                isRestorable = false
            };
            RegisterFloat(scrubberJSON);

            timeJSON = new JSONStorableFloat(StorableNames.Time, 0f, v => animationEditContext.playTime = v.Snap(), 0f, float.MaxValue)
            {
                isStorable = false,
                isRestorable = false
            };
            RegisterFloat(timeJSON);

            playJSON = new JSONStorableAction(StorableNames.Play, () =>
            {
                var selected = string.IsNullOrEmpty(animationLegacyJSON.val) ? animation.GetDefaultClip() : animation.GetClips(animationLegacyJSON.val).FirstOrDefault();
                animation.PlayOneAndOtherMainsInLayers(selected);
            });
            RegisterAction(playJSON);

            playIfNotPlayingJSON = new JSONStorableAction(StorableNames.PlayIfNotPlaying, () =>
            {
                if (animation == null) return;
                var selected = string.IsNullOrEmpty(animationLegacyJSON.val) ? animation.GetDefaultClip() : animation.GetClips(animationLegacyJSON.val).FirstOrDefault();
                if (selected == null) return;
                if (!animation.isPlaying)
                    animation.PlayOneAndOtherMainsInLayers(selected);
                else if (!selected.playbackEnabled)
                    animation.PlayClip(selected, true);
            });
            RegisterAction(playIfNotPlayingJSON);

            isPlayingJSON = new JSONStorableBool(StorableNames.IsPlaying, false, val =>
            {
                if (val)
                    playIfNotPlayingJSON.actionCallback();
                else
                    stopJSON.actionCallback();
            })
            {
                isStorable = false,
                isRestorable = false
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

            stopAndResetJSON = new JSONStorableAction(StorableNames.StopAndReset, () =>
            {
                if (animation == null) return;
                animationEditContext.StopAndReset();
                peers.SendStopAndReset();
            });
            RegisterAction(stopAndResetJSON);

            nextFrameJSON = new JSONStorableAction(StorableNames.NextFrame, () => animationEditContext.NextFrame());
            RegisterAction(nextFrameJSON);

            previousFrameJSON = new JSONStorableAction(StorableNames.PreviousFrame, () => animationEditContext.PreviousFrame());
            RegisterAction(previousFrameJSON);

            deleteJSON = new JSONStorableAction("Delete", () => animationEditContext.Delete());
            cutJSON = new JSONStorableAction("Cut", () => animationEditContext.Cut());
            copyJSON = new JSONStorableAction("Copy", () => animationEditContext.Copy());
            pasteJSON = new JSONStorableAction("Paste", () => animationEditContext.Paste());

            speedJSON = new JSONStorableFloat(StorableNames.Speed, 1f, v => animation.speed = v, -1f, 5f, false)
            {
                isStorable = false,
                isRestorable = false
            };
            RegisterFloat(speedJSON);

            lockedJSON = new JSONStorableBool(StorableNames.Locked, false, v => animationEditContext.locked = v)
            {
                isStorable = false,
                isRestorable = false
            };
            RegisterBool(lockedJSON);

            _scrubberAnalogControlJSON = new JSONStorableFloat("Scrubber", 0f, -1f, 1f);

            pausedJSON = new JSONStorableBool(StorableNames.Paused, false, v => animation.paused = v)
            {
                isStorable = false,
                isRestorable = false
            };
            RegisterBool(pausedJSON);
        }

        private IEnumerator DeferredInit()
        {
            yield return new WaitForEndOfFrame();
            if (this == null) yield break;
            if (animation != null)
            {
                while (SuperController.singleton.isLoading)
                {
                    yield return 0;
                    if (this == null) yield break;
                }
                foreach (var t in animation.clips.SelectMany(c => c.targetTriggers))
                {
                    // Allows accessing the self target
                    t.Refresh();
                }
                StartAutoPlay();
                yield break;
            }
            base.containingAtom.RestoreFromLast(this);
            if (animation != null)
            {
                yield return 0;
                if (this == null) yield break;
                animationEditContext.Sample();
                yield break;
            }
            AddAnimationComponents();
            animationEditContext.Initialize();
            BindAnimation();
            animation.enabled = enabled;

            yield return 0;
            if (this == null) yield break;

            if (enabled)
                animationEditContext.Sample();
        }

        private void AddAnimationComponents()
        {
            if (animation != null) return;
            animation = gameObject.AddComponent<AtomAnimation>();
            if (animation == null) throw new InvalidOperationException("Could not add animation component");
            animationEditContext = gameObject.AddComponent<AtomAnimationEditContext>();
            if (animationEditContext == null) throw new InvalidOperationException("Could not add animationEditContext component");
            animationEditContext.animation = animation;
        }

        private void StartAutoPlay()
        {
            foreach (var autoPlayClip in animation.clips.Where(c => c.autoPlay))
            {
                animation.PlayClip(autoPlayClip, true);
            }
        }

        #endregion

        #region Load / Save

        public override JSONClass GetJSON(bool includePhysical = true, bool includeAppearance = true, bool forceStore = false)
        {
            var json = base.GetJSON(includePhysical, includeAppearance, forceStore);

            try
            {
                json["Animation"] = serializer.SerializeAnimation(animation);
                json["Options"] = AtomAnimationSerializer.SerializeEditContext(animationEditContext);
                needsStore = true;
            }
            catch (Exception exc)
            {
                SuperController.LogError($"Timeline.{nameof(AtomPlugin)}.{nameof(GetJSON)} (Serialize): {exc}");
            }

            return json;
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
                    Load(animationJSON, jc["Options"]);
                    return;
                }

                var legacyStr = jc["Save"];
                if (!string.IsNullOrEmpty(legacyStr))
                {
                    Load(JSONNode.Parse(legacyStr) as JSONClass, null);
                }
            }
            catch (Exception exc)
            {
                SuperController.LogError($"Timeline.{nameof(AtomPlugin)}.{nameof(RestoreFromJSON)}: {exc}");
            }
        }

        public void Load(JSONNode animationJSON, JSONNode animationEditContextJSON)
        {
            if (_restoring) return;
            _restoring = true;
            try
            {
                AddAnimationComponents();
                animation.Clear();
                serializer.DeserializeAnimation(animation, animationJSON.AsObject);
                if (animationEditContextJSON != null)
                    serializer.DeserializeAnimationEditContext(animationEditContext, animationEditContextJSON.AsObject);
                animationEditContext.Initialize();
                BindAnimation();
                animation.enabled = enabled;
                GC.Collect();
            }
            catch (Exception exc)
            {
                SuperController.LogError($"Timeline.{nameof(AtomPlugin)}.{nameof(Load)}: {exc}");
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
            foreach (var action in _clipStorables)
            {
                DeregisterAction(action);
            }
            _clipStorables.Clear();

            animationEditContext.onTimeChanged.AddListener(OnTimeChanged);
            animationEditContext.onCurrentAnimationChanged.AddListener(OnCurrentAnimationChanged);
            animationEditContext.onEditorSettingsChanged.AddListener(OnEditorSettingsChanged);

            animation.onClipsListChanged.AddListener(OnClipsListChanged);
            animation.onAnimationSettingsChanged.AddListener(OnAnimationParametersChanged);
            animation.onIsPlayingChanged.AddListener(OnIsPlayingChanged);
            animation.onClipIsPlayingChanged.AddListener(OnClipIsPlayingChanged);
            animation.onPausedChanged.AddListener(OnPauseChanged);

            OnClipsListChanged();
            OnAnimationParametersChanged();

            if(ui != null) ui.Bind(animationEditContext);
            peers.animationEditContext = animationEditContext;
            if (_freeControllerHook != null) _freeControllerHook.animationEditContext = animationEditContext;
            if (enabled) _freeControllerHook.enabled = true;

            animationLegacyJSON.valNoCallback = "";

            peers.Ready();
            BroadcastToControllers(nameof(IRemoteControllerPlugin.OnTimelineAnimationReady));
            SuperController.singleton.BroadcastMessage("OnActionsProviderAvailable", this, SendMessageOptions.DontRequireReceiver);
        }

        private void DeregisterAction(AnimStorableActionMap action)
        {
            DeregisterAction(action.playJSON);
            DeregisterFloat(action.speedJSON);
            DeregisterFloat(action.weightJSON);
        }

        private void OnTimeChanged(AtomAnimationEditContext.TimeChangedEventArgs time)
        {
            if (base.containingAtom == null) return; // Plugin destroyed
            try
            {
                // Update UI
                scrubberJSON.valNoCallback = time.currentClipTime;
                timeJSON.valNoCallback = time.time;

                peers.SendTime(animationEditContext.current);
                BroadcastToControllers(nameof(IRemoteControllerPlugin.OnTimelineTimeChanged));
            }
            catch (Exception exc)
            {
                SuperController.LogError($"Timeline.{nameof(AtomPlugin)}.{nameof(OnTimeChanged)}: {exc}");
            }
        }

        private void OnCurrentAnimationChanged(AtomAnimationEditContext.CurrentAnimationChangedEventArgs args)
        {
            peers.SendCurrentAnimation(animationEditContext.current);
            OnAnimationParametersChanged();
        }

        private void OnClipsListChanged()
        {
            try
            {
                var animationNames = animation.clips.Select(c => c.animationName).Distinct().ToList();

                animationLegacyJSON.choices = animationNames;

                foreach (var animName in animationNames)
                {
                    if (_clipStorables.Any(a => a.animationName == animName)) continue;
                    CreateAndRegisterClipStorables(animName);
                }

                foreach (var group in animation.clips.GroupBy(c => c.animationNameGroup).Where(g => g.Key != null && g.Count() > 1))
                {
                    CreateAndRegisterGroupStorables(group.Key);
                }

                if (_clipStorables.Count > animationNames.Count)
                {
                    foreach (var action in _clipStorables.ToArray())
                    {
                        if (!animationNames.Contains(action.animationName))
                        {
                            DeregisterAction(action);
                            _clipStorables.Remove(action);
                        }
                    }
                }

                BroadcastToControllers(nameof(IRemoteControllerPlugin.OnTimelineAnimationParametersChanged));
            }
            catch (Exception exc)
            {
                SuperController.LogError($"Timeline.{nameof(AtomPlugin)}.{nameof(OnClipsListChanged)}: {exc}");
            }
        }

        private void CreateAndRegisterGroupStorables(string groupKey)
        {
            RegisterAction(new JSONStorableAction($"Play {groupKey}{AtomAnimation._randomizeGroupSuffix}", () =>
            {
                animation.PlayRandom(groupKey);
            }));

            var setSpeedJSON = new JSONStorableFloat($"Set Speed {groupKey}{AtomAnimation._randomizeGroupSuffix}", 0f, -1f, 5f, false);
            setSpeedJSON.setCallbackFunction = val =>
            {
                foreach (var clip in animation.clips.Where(c => c.animationNameGroup == groupKey))
                    clip.speed = val;
                setSpeedJSON.valNoCallback = 0;
            };
            RegisterFloat(setSpeedJSON);

            var setWeightJSON = new JSONStorableFloat($"Set Weight {groupKey}{AtomAnimation._randomizeGroupSuffix}", 1f, 0f, 1f);
            setWeightJSON.setCallbackFunction = val =>
            {
                foreach (var clip in animation.clips.Where(c => c.animationNameGroup == groupKey))
                    clip.weight = val;
                setWeightJSON.valNoCallback = 0;
            };
            RegisterFloat(setWeightJSON);
        }

        private void CreateAndRegisterClipStorables(string animationName)
        {
            var playClipJSON = new JSONStorableAction($"Play {animationName}", () =>
            {
                animation.PlayClips(animationName, true);
            });
            RegisterAction(playClipJSON);

            var speedClipJSON = new JSONStorableFloat($"Speed {animationName}", 1f, val =>
            {
                foreach (var clip in animation.GetClips(animationName))
                    clip.speed = val;
            }, -1f, 5f, false)
            {
                valNoCallback = animation.GetClips(animationName).First().speed,
                isStorable = false,
                isRestorable = false
            };
            RegisterFloat(speedClipJSON);

            var weightJSON = new JSONStorableFloat($"Weight {animationName}", 1f, val =>
            {
                foreach (var clip in animation.GetClips(animationName))
                    clip.weight = val;
            }, 0f, 1f)
            {
                valNoCallback = animation.GetClips(animationName).First().weight,
                isStorable = false,
                isRestorable = false
            };
            RegisterFloat(weightJSON);

            _clipStorables.Add(new AnimStorableActionMap
            {
                animationName = animationName,
                playJSON = playClipJSON,
                speedJSON = speedClipJSON,
                weightJSON = weightJSON
            });
        }

        private void OnAnimationParametersChanged()
        {
            try
            {
                scrubberJSON.max = animationEditContext.current.animationLength;
                scrubberJSON.valNoCallback = animationEditContext.clipTime;
                timeJSON.valNoCallback = animationEditContext.playTime;
                speedJSON.valNoCallback = animation.speed;

                BroadcastToControllers(nameof(IRemoteControllerPlugin.OnTimelineAnimationParametersChanged));
            }
            catch (Exception exc)
            {
                SuperController.LogError($"Timeline.{nameof(AtomPlugin)}.{nameof(OnAnimationParametersChanged)}: {exc}");
            }
        }

        private void OnEditorSettingsChanged(string propertyName)
        {
            try
            {
                lockedJSON.valNoCallback = animationEditContext.locked;
            }
            catch (Exception exc)
            {
                SuperController.LogError($"Timeline.{nameof(AtomPlugin)}.{nameof(OnEditorSettingsChanged)}: {exc}");
            }
        }

        private void OnIsPlayingChanged(AtomAnimationClip clip)
        {
            isPlayingJSON.valNoCallback = animation.isPlaying;
            _freeControllerHook.enabled = !animation.isPlaying;
            peers.SendPlaybackState(clip);
        }

        private void OnClipIsPlayingChanged(AtomAnimationClip clip)
        {
            if (animation.master && clip.playbackEnabled && animation.sequencing)
                peers.SendMasterClipState(clip);
        }

        private void OnPauseChanged()
        {
            pausedJSON.valNoCallback = animation.paused;
            peers.SendPaused();
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
                    plugin.SendMessage(methodName, this, SendMessageOptions.RequireReceiver);
                }
            }
        }

        #endregion

        #region Callbacks

        public void ChangeAnimationLegacy(string animationName)
        {
            if (string.IsNullOrEmpty(animationName)) return;

            try
            {
                if (animation.isPlaying) animation.PlayClips(animationName, true);
            }
            catch (Exception exc)
            {
                SuperController.LogError($"Timeline.{nameof(AtomPlugin)}.{nameof(ChangeAnimationLegacy)}: {exc}");
            }
        }

        public void ChangeScreen(string screenName, object screenArg)
        {
            if (ui == null) return;

            ui.screensManager.ChangeScreen(screenName, screenArg);
            // If the selection cannot be dispatched, change the controller injected ui up front
            if (!ui.isActiveAndEnabled && controllerInjectedUI != null)
            {
                controllerInjectedUI.screensManager.ChangeScreen(screenName, screenArg);
            }
        }

        #endregion

        #region Controller integration

        public void VamTimelineConnectController(Dictionary<string, object> dict)
        {
            var proxy = SyncProxy.Wrap(dict);
            // TODO: This or just use the storables dict already on storable??
            proxy.animation = animationLegacyJSON;
            proxy.isPlaying = isPlayingJSON;
            proxy.nextFrame = nextFrameJSON;
            proxy.play = playJSON;
            proxy.playIfNotPlaying = playIfNotPlayingJSON;
            proxy.previousFrame = previousFrameJSON;
            proxy.stop = stopJSON;
            proxy.stopAndReset = stopAndResetJSON;
            proxy.time = timeJSON;
            proxy.connected = true;
        }

        public void VamTimelineRequestControlPanel(GameObject container)
        {
            StartCoroutine(InjectControlPanelDeferred(container));
        }

        private IEnumerator InjectControlPanelDeferred(GameObject container)
        {
            while (ui == null && container != null) { yield return 0; }

            if (container == null) yield break;

            controllerInjectedUI = container.GetComponent<Editor>();
            if (controllerInjectedUI == null)
            {
                controllerInjectedUI = Editor.Configure(container);
                controllerInjectedUI.popupParent = controllerInjectedUI.transform.parent;
                controllerInjectedUI.Bind(this, ui.screensManager.GetDefaultScreen());
                controllerInjectedUI.screensManager.onScreenChanged.AddListener(args =>
                {
                    ui.screensManager.ChangeScreen(args.screenName, args.screenArg);
                    peers.SendScreen(args.screenName, args.screenArg);
                });
            }
            if (controllerInjectedUI.animationEditContext != animationEditContext)
                controllerInjectedUI.Bind(animationEditContext);
        }

        private void DestroyControllerPanel()
        {
            if (controllerInjectedUI == null) return;
            var go = controllerInjectedUI.gameObject;
            go.transform.SetParent(null, false);
            Destroy(go);
            controllerInjectedUI = null;
        }

        #endregion

        #region Sync Events

        public void OnTimelineAnimationReady(MVRScript storable)
        {
            peers.OnTimelineAnimationReady(storable);
        }

        public void OnTimelineAnimationDisabled(MVRScript storable)
        {
            peers.OnTimelineAnimationDisabled(storable);
        }

        public void OnTimelineEvent(object[] e)
        {
            peers.OnTimelineEvent(e);
        }

        #endregion

        #region Keybindings

        private OperationsFactory operations => new OperationsFactory(containingAtom, animation, animationEditContext.current);

        public void OnBindingsListRequested(List<object> bindings)
        {
            bindings.Add(new []
            {
                new KeyValuePair<string, string>("Namespace", "Timeline")
            });
            bindings.Add(new JSONStorableAction("OpenUI", SelectAndOpenUI));
            bindings.Add(new JSONStorableAction("OpenUI_AnimationsTab", () => { ChangeScreen(AnimationsScreen.ScreenName, null); SelectAndOpenUI(); }));
            bindings.Add(new JSONStorableAction("OpenUI_AddAnimationsTab", () => { ChangeScreen(AddAnimationScreen.ScreenName, null); SelectAndOpenUI(); }));
            bindings.Add(new JSONStorableAction("OpenUI_ManageAnimationsTab", () => { ChangeScreen(ManageAnimationsScreen.ScreenName, null); SelectAndOpenUI(); }));
            bindings.Add(new JSONStorableAction("OpenUI_TargetsTab", () => { ChangeScreen(TargetsScreen.ScreenName, null); SelectAndOpenUI(); }));
            bindings.Add(new JSONStorableAction("OpenUI_AddRemoveTargetsTab", () => { ChangeScreen(AddRemoveTargetsScreen.ScreenName, null); SelectAndOpenUI(); }));
            bindings.Add(new JSONStorableAction("OpenUI_EditTab", () => { ChangeScreen(EditAnimationScreen.ScreenName, null); SelectAndOpenUI(); }));
            bindings.Add(new JSONStorableAction("OpenUI_SequenceTab", () => { ChangeScreen(SequencingScreen.ScreenName, null); SelectAndOpenUI(); }));
            bindings.Add(new JSONStorableAction("OpenUI_MoreTab", () => { ChangeScreen(MoreScreen.ScreenName, null); SelectAndOpenUI(); }));
            bindings.Add(new JSONStorableAction("OpenUI_MoreTab_ImportExportAnimations", () => { ChangeScreen(ImportExportScreen.ScreenName, null); SelectAndOpenUI(); }));
            bindings.Add(new JSONStorableAction("OpenUI_MoreTab_BulkChanges", () => { ChangeScreen(BulkScreen.ScreenName, null); SelectAndOpenUI(); }));
            bindings.Add(new JSONStorableAction("OpenUI_MoreTab_Mocap", () => { ChangeScreen(MocapScreen.ScreenName, null); SelectAndOpenUI(); }));
            bindings.Add(new JSONStorableAction("OpenUI_MoreTab_AdvancedKeyframeTools", () => { ChangeScreen(AdvancedKeyframeToolsScreen.ScreenName, null); SelectAndOpenUI(); }));
            bindings.Add(new JSONStorableAction("OpenUI_MoreTab_Options", () => { ChangeScreen(OptionsScreen.ScreenName, null); SelectAndOpenUI(); }));
            bindings.Add(new JSONStorableAction("PreviousFrame", animationEditContext.PreviousFrame));
            bindings.Add(new JSONStorableAction("NextFrame", animationEditContext.NextFrame));
            bindings.Add(new JSONStorableAction("PreviousAnimationInCurrentLayer", previousAnimationLegacyJSON.actionCallback));
            bindings.Add(new JSONStorableAction("NextAnimationInCurrentLayer", nextAnimationLegacyJSON.actionCallback));
            bindings.Add(new JSONStorableAction("PlayCurrentClip", animationEditContext.PlayCurrentClip));
            bindings.Add(new JSONStorableAction("PlayAll", animationEditContext.PlayAll));
            bindings.Add(new JSONStorableAction("Stop", animationEditContext.Stop));
            bindings.Add(new JSONStorableAction("StopAndReset", animationEditContext.StopAndReset));
            bindings.Add(new JSONStorableAction("TogglePause", () => animation.paused = !animation.paused));
            bindings.Add(new JSONStorableAction("RewindSecond", () => animationEditContext.RewindSeconds(1f)));
            bindings.Add(new JSONStorableAction("RewindTenthOfASecond", () => animationEditContext.RewindSeconds(0.1f)));
            bindings.Add(new JSONStorableAction("SnapToSecond", () => animationEditContext.SnapTo(1f)));
            bindings.Add(new JSONStorableAction("ForwardTenthOfASecond", () => animationEditContext.ForwardSeconds(0.1f)));
            bindings.Add(new JSONStorableAction("ForwardSecond", () => animationEditContext.ForwardSeconds(1f)));
            bindings.Add(new JSONStorableAction("AddTarget_SelectedController", () => operations.Targets().AddSelectedController()));
            bindings.Add(new JSONStorableAction("Keyframe_Cut", () => animationEditContext.Cut()));
            bindings.Add(new JSONStorableAction("Keyframe_Copy", () => animationEditContext.Copy()));
            bindings.Add(new JSONStorableAction("Keyframe_Paste", () => animationEditContext.Paste()));
            bindings.Add(new JSONStorableAction("Keyframe_Delete", () => animationEditContext.Delete()));
            bindings.Add(new JSONStorableAction("Keyframe_Add_CurrentController", () => operations.Keyframes().AddSelectedController()));
            bindings.Add(new JSONStorableAction("Keyframe_Add_AllControllerTargets", () => operations.Keyframes().AddAllControllers()));

            bindings.Add(_scrubberAnalogControlJSON);
        }

        private void SelectAndOpenUI()
        {
            if(containingAtom == null) SuperController.LogError("Timeline: No containing atom");
            #if (VAM_GT_1_20)
            SuperController.singleton.SelectController(containingAtom.mainController, false, false, true);
            #else
            SuperController.singleton.SelectController(containingAtom.mainController);
            #endif
            SuperController.singleton.ShowMainHUDMonitor();
            StartCoroutine(WaitForUI());
        }

        private IEnumerator WaitForUI()
        {
            var expiration = Time.unscaledTime + 1f;
            while (Time.unscaledTime < expiration)
            {
                yield return 0;
                var selector = containingAtom.gameObject.GetComponentInChildren<UITabSelector>();
                if(selector == null) continue;
                selector.SetActiveTab("Plugins");
                if (UITransform == null) SuperController.LogError("Timeline: No UI");
                UITransform.gameObject.SetActive(true);
                yield break;
            }
        }

        #endregion
    }
}

