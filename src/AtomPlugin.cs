using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SimpleJSON;
using UnityEngine;
using UnityEngine.UI;

namespace VamTimeline
{
    [RequireComponent(typeof(Editor))]
    public class AtomPlugin : MVRScript, IAtomPlugin
    {
        public AtomAnimation animation { get; private set; }
        public AtomAnimationEditContext animationEditContext { get; private set; }
        public new Atom containingAtom => base.containingAtom;
        public new MVRPluginManager manager => base.manager;
        public AtomAnimationSerializer serializer { get; private set; }

        private Editor _ui;
        private Editor _controllerInjectedUI;
        public PeerManager peers { get; private set; }

        private JSONStorableStringChooser _animationJSON;
        private JSONStorableAction _nextAnimationJSON;
        private JSONStorableAction _previousAnimationJSON;
        private JSONStorableAction _nextAnimationInMainLayerJSON;
        private JSONStorableAction _previousAnimationInMainLayerJSON;
        private JSONStorableFloat _scrubberJSON;
        private JSONStorableFloat _timeJSON;
        private JSONStorableAction _playJSON;
        private JSONStorableBool _isPlayingJSON;
        private JSONStorableAction _playIfNotPlayingJSON;
        private JSONStorableAction _stopJSON;
        private JSONStorableAction _stopIfPlayingJSON;
        private JSONStorableAction _stopAndResetJSON;
        private JSONStorableAction _nextFrameJSON;
        private JSONStorableAction _previousFrameJSON;
        private JSONStorableFloat _speedJSON;
        private JSONStorableFloat _weightJSON;
        private JSONStorableBool _lockedJSON;
        private JSONStorableBool _pausedJSON;
        public JSONStorableAction deleteJSON { get; private set; }
        public JSONStorableAction cutJSON { get; private set; }
        public JSONStorableAction copyJSON { get; private set; }
        public JSONStorableAction pasteJSON { get; private set; }

        private JSONStorableFloat _scrubberAnalogControlJSON;
        private bool _scrubbing;

        private bool _restoring;
        private string _legacyAnimationNext;
        private FreeControllerV3Hook _freeControllerHook;
        public Logger logger { get; private set; }
        public OperationsFactory operations => new OperationsFactory(containingAtom, animation, animationEditContext.current, peers);

        private class AnimStorableActionMap
        {
            public string animationName;
            public JSONStorableAction playJSON;
            public JSONStorableFloat speedJSON;
            public JSONStorableFloat weightJSON;
        }
        private readonly List<AnimStorableActionMap> _clipStorables = new List<AnimStorableActionMap>();

        #region Init

        private void Start()
        {
            _controllerInjectedUI = GetComponent<Editor>();
        }

        public override void Init()
        {
            base.Init();

            try
            {
                logger = new Logger(base.containingAtom);
                serializer = new AtomAnimationSerializer(base.containingAtom);
                peers = new PeerManager(base.containingAtom, this, logger);
                _freeControllerHook = gameObject.AddComponent<FreeControllerV3Hook>();
                _freeControllerHook.enabled = false;
                _freeControllerHook.containingAtom = base.containingAtom;
                InitStorables();
                SuperController.singleton.StartCoroutine(DeferredInit());


                SuperController.singleton.onAtomUIDRenameHandlers += OnAtomUIDRename;
                SuperController.singleton.onAtomRemovedHandlers += OnAtomRemoved;
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
            if (_ui != null || this == null) yield break;
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

            _ui = Editor.AddTo(scriptUI.fullWidthUIContent);
            _ui.popupParent = UITransform;
            _ui.Bind(this);
            if (animationEditContext != null) _ui.Bind(animationEditContext);
            _ui.screensManager.onScreenChanged.AddListener(args =>
            {
                if (_controllerInjectedUI != null) _controllerInjectedUI.screensManager.ChangeScreen(args.screenName, args.screenArg);
                peers.SendScreen(args.screenName, args.screenArg);
            });
        }

        #endregion

        #region Update

        public void Update()
        {
            if (ReferenceEquals(animation, null)) return;

            animation.simulationFrozen = IsPhysicsReset();

            if (animation.isPlaying)
            {
                _scrubberJSON.valNoCallback = animationEditContext.clipTime;
                _timeJSON.valNoCallback = animation.playTime;
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

        private const float _physicsResetTimeoutSeconds = 0.5f;
        private float _physicsResetTimeout;

        private bool IsPhysicsReset()
        {
            if (containingAtom.physicsSimulators.Length == 0) return false;
            var physicsSimulator = containingAtom.physicsSimulators[0];
            #if(VAM_GT_1_20_0_9)
            if (!physicsSimulator.resetSimulation)
            #else
            if (!physicsSimulator.pauseSimulation)
            #endif
            {
                _physicsResetTimeout = 0;
                return false;
            }
            if (_physicsResetTimeout == 0)
                _physicsResetTimeout = Time.time + _physicsResetTimeoutSeconds;
            return Time.time < _physicsResetTimeout;

        }

        #endregion

        #region Lifecycle

        public void OnEnable()
        {
            try
            {
                if (_ui != null)
                    _ui.enabled = true;
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
                if (_ui != null) _ui.enabled = false;
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
                SuperController.singleton.onAtomUIDRenameHandlers -= OnAtomUIDRename;
                SuperController.singleton.onAtomRemovedHandlers -= OnAtomRemoved;

                try { Destroy(animation); } catch (Exception exc) { SuperController.LogError($"Timeline.{nameof(OnDestroy)} [animations]: {exc}"); }
                try { Destroy(_ui); } catch (Exception exc) { SuperController.LogError($"Timeline.{nameof(OnDestroy)} [ui]: {exc}"); }
                try { DestroyControllerPanel(); } catch (Exception exc) { SuperController.LogError($"Timeline.{nameof(OnDestroy)} [panel]: {exc}"); }
                Destroy(_freeControllerHook);
                TimelinePrefabs.Destroy();
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
            _animationJSON = new JSONStorableStringChooser(StorableNames.Animation, new List<string>(), "", "Animation", val =>
            {
                if (string.IsNullOrEmpty(val)) return;
                if (logger.triggersReceived) logger.Log(logger.triggersCategory, $"Triggered '{StorableNames.Animation}' = '{val}'");
                _legacyAnimationNext = val;
                var clip = animation.index.ByName(animation.playingAnimationSegment, val).FirstOrDefault() ?? animation.index.ByName(val).FirstOrDefault();
                if (clip == null) return;
                if (animationEditContext.current != clip)
                    animationEditContext.SelectAnimation(clip);
                else if (animation.isPlaying)
                    animation.PlayClipByName(val, true);
                _animationJSON.valNoCallback = "";
            })
            {
                isStorable = false,
                isRestorable = false
            };
            RegisterStringChooser(_animationJSON);

            _nextAnimationJSON = new JSONStorableAction(StorableNames.NextAnimation, () =>
            {
                if (logger.triggersReceived) logger.Log(logger.triggersCategory, $"Triggered '{StorableNames.NextAnimation}'");
                animationEditContext.GoToNextAnimation(animation.clips[0].animationLayerQualifiedId);
            });
            RegisterAction(_nextAnimationJSON);

            _previousAnimationJSON = new JSONStorableAction(StorableNames.PreviousAnimation, () =>
            {
                if (logger.triggersReceived) logger.Log(logger.triggersCategory, $"Triggered '{StorableNames.PreviousAnimation}'");
                animationEditContext.GoToPreviousAnimation(animation.clips[0].animationLayerQualifiedId);
            });
            RegisterAction(_previousAnimationJSON);

            _nextAnimationInMainLayerJSON = new JSONStorableAction(StorableNames.NextAnimationInMainLayer, () =>
            {
                if (logger.triggersReceived) logger.Log(logger.triggersCategory, $"Triggered '{StorableNames.NextAnimationInMainLayer}'");
                animationEditContext.GoToNextAnimation(animation.clips[0].animationLayerQualifiedId);
            });
            RegisterAction(_nextAnimationInMainLayerJSON);

            _previousAnimationInMainLayerJSON = new JSONStorableAction(StorableNames.PreviousAnimationInMainLayer, () =>
            {
                if (logger.triggersReceived) logger.Log(logger.triggersCategory, $"Triggered '{StorableNames.PreviousAnimationInMainLayer}'");
                animationEditContext.GoToPreviousAnimation(animation.clips[0].animationLayerQualifiedId);
            });
            RegisterAction(_previousAnimationInMainLayerJSON);

            _scrubberJSON = new JSONStorableFloat(StorableNames.Scrubber, 0f, v => animationEditContext.clipTime = v.Snap(animationEditContext.snap), 0f, AtomAnimationClip.DefaultAnimationLength)
            {
                isStorable = false,
                isRestorable = false
            };
            RegisterFloat(_scrubberJSON);

            _timeJSON = new JSONStorableFloat(StorableNames.Time, 0f, v => animationEditContext.clipTime = v, 0f, float.MaxValue)
            {
                isStorable = false,
                isRestorable = false
            };
            RegisterFloat(_timeJSON);

            _playJSON = new JSONStorableAction(StorableNames.Play, () => StorablePlay(StorableNames.Play));
            RegisterAction(_playJSON);

            _playIfNotPlayingJSON = new JSONStorableAction(StorableNames.PlayIfNotPlaying, () =>
            {
                if (animation.isPlaying) return;
                StorablePlay(StorableNames.PlayIfNotPlaying);
            });
            RegisterAction(_playIfNotPlayingJSON);

            _isPlayingJSON = new JSONStorableBool(StorableNames.IsPlaying, false, val =>
            {
                if (val)
                    _playIfNotPlayingJSON.actionCallback();
                else
                    _stopJSON.actionCallback();
            })
            {
                isStorable = false,
                isRestorable = false
            };
            RegisterBool(_isPlayingJSON);

            _stopJSON = new JSONStorableAction(StorableNames.Stop, () =>
            {
                if (logger.triggersReceived) logger.Log(logger.triggersCategory, $"Triggered '{StorableNames.Stop}'");
                if (animation.isPlaying)
                    animation.StopAll();
                else
                    animation.ResetAll();
            });
            RegisterAction(_stopJSON);

            _stopIfPlayingJSON = new JSONStorableAction(StorableNames.StopIfPlaying, () =>
            {
                if (!animation.isPlaying) return;
                if (logger.triggersReceived) logger.Log(logger.triggersCategory, $"Triggered '{StorableNames.StopIfPlaying}'");
                animation.StopAll();
            });
            RegisterAction(_stopIfPlayingJSON);

            _stopAndResetJSON = new JSONStorableAction(StorableNames.StopAndReset, () =>
            {
                if (logger.triggersReceived) logger.Log(logger.triggersCategory, $"Triggered '{StorableNames.StopAndReset}'");
                animationEditContext.StopAndReset();
                peers.SendStopAndReset();
            });
            RegisterAction(_stopAndResetJSON);

            _nextFrameJSON = new JSONStorableAction(StorableNames.NextFrame, () => animationEditContext.NextFrame());
            RegisterAction(_nextFrameJSON);

            _previousFrameJSON = new JSONStorableAction(StorableNames.PreviousFrame, () => animationEditContext.PreviousFrame());
            RegisterAction(_previousFrameJSON);

            deleteJSON = new JSONStorableAction("Delete", () => animationEditContext.Delete());
            cutJSON = new JSONStorableAction("Cut", () => animationEditContext.Cut());
            copyJSON = new JSONStorableAction("Copy", () => animationEditContext.Copy());
            pasteJSON = new JSONStorableAction("Paste", () => animationEditContext.Paste());

            _speedJSON = new JSONStorableFloat(StorableNames.Speed, 1f, v => animation.globalSpeed = v, -1f, 5f, false)
            {
                isStorable = false,
                isRestorable = false
            };
            RegisterFloat(_speedJSON);

            _weightJSON = new JSONStorableFloat(StorableNames.Weight, 1f, v => animation.globalWeight = v, 0f, 1f, true)
            {
                isStorable = false,
                isRestorable = false
            };
            RegisterFloat(_weightJSON);

            _lockedJSON = new JSONStorableBool(StorableNames.Locked, false, v => animationEditContext.locked = v)
            {
                isStorable = false,
                isRestorable = false
            };
            RegisterBool(_lockedJSON);

            _scrubberAnalogControlJSON = new JSONStorableFloat("Scrubber", 0f, -1f, 1f);

            _pausedJSON = new JSONStorableBool(StorableNames.Paused, false, v => animation.paused = v)
            {
                isStorable = false,
                isRestorable = false
            };
            RegisterBool(_pausedJSON);
        }

        private void StorablePlay(string storableName)
        {
            if (animation.paused)
            {
                animation.paused = false;
                if (logger.triggersReceived) logger.Log(logger.triggersCategory, $"Triggered '{storableName}' (unpause)");
                return;
            }

            AtomAnimationClip selected;
            if (string.IsNullOrEmpty(_legacyAnimationNext))
            {
                selected = animation.GetDefaultClip();
                if (logger.triggersReceived) logger.Log(logger.triggersCategory, $"Triggered '{storableName}' (Using default clip)");
            }
            else
            {
                selected = animation.index.ByName(_legacyAnimationNext).FirstOrDefault();
                if (logger.triggersReceived) logger.Log(logger.triggersCategory, $"Triggered '{storableName}' (Using 'Animation' = '{_legacyAnimationNext}')");
                if (selected == null)
                {
                    SuperController.LogError($"Timeline: Atom '{containingAtom.uid}' failed to play animation '{_legacyAnimationNext}' specified in Animations storable: no animation found with that name.");
                    _legacyAnimationNext = null;
                    return;
                }
            }

            animation.PlaySegment(selected);
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

                var confirmPanel = SuperController.singleton.errorLogPanel.parent.Find("UserConfirmCanvas");
                while (confirmPanel != null && confirmPanel.childCount > 0)
                {
                    yield return 0;
                    if (this == null) yield break;
                }

                serializer.RestoreMissingTriggers(animation);
                StartAutoPlay();
                yield break;
            }
            base.containingAtom.RestoreFromLast(this);
            if (animation != null)
            {
                yield return 0;
                if (this == null) yield break;
                serializer.RestoreMissingTriggers(animation);
                animationEditContext.clipTime = 0f;
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
            {
                animationEditContext.clipTime = 0f;
                animationEditContext.Sample();
            }
        }

        private void AddAnimationComponents()
        {
            if (animation != null) return;
            animation = gameObject.AddComponent<AtomAnimation>();
            if (animation == null) throw new InvalidOperationException("Could not add animation component");
            animation.logger = logger;
            animationEditContext = gameObject.AddComponent<AtomAnimationEditContext>();
            if (animationEditContext == null) throw new InvalidOperationException("Could not add animationEditContext component");
            animationEditContext.logger = logger;
            animationEditContext.peers = peers;
            animationEditContext.animation = animation;
        }

        private void StartAutoPlay()
        {
            // NOTE: When using segments, it's valid to play a non-default animation on each layer, but if multiple segments are selected, then it can be a problem.
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
            animation.onSegmentPlayed.AddListener(OnSegmentPlayed);
            animation.onClipIsPlayingChanged.AddListener(OnClipIsPlayingChanged);
            animation.onPausedChanged.AddListener(OnPauseChanged);
            animation.onSpeedChanged.AddListener(OnSpeedChanged);
            animation.onWeightChanged.AddListener(OnWeightChanged);
            animation.animatables.onControllersListChanged.AddListener(OnControllersListChanged);

            OnControllersListChanged();
            OnClipsListChanged();
            OnAnimationParametersChanged();
            OnSpeedChanged();
            OnWeightChanged();

            if(_ui != null) _ui.Bind(animationEditContext);
            peers.animationEditContext = animationEditContext;
            if (_freeControllerHook != null) _freeControllerHook.animationEditContext = animationEditContext;
            if (enabled) _freeControllerHook.enabled = true;

            _animationJSON.valNoCallback = "";

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

        private void OnControllersListChanged()
        {
            _freeControllerHook.SetControllers(animation.animatables.controllers.Select(c => c.controller));

            foreach (var animatable in animation.animatables.controllers.Where(c => c.weightJSON != null))
            {
                if(IsFloatJSONParam(animatable.weightJSON.name)) continue;
                RegisterFloat(animatable.weightJSON);
            }

            // TODO: Removing would be better.
        }

        private void OnTimeChanged(AtomAnimationEditContext.TimeChangedEventArgs time)
        {
            if (base.containingAtom == null) return; // Plugin destroyed
            try
            {
                // Update UI
                _scrubberJSON.valNoCallback = time.currentClipTime;
                _timeJSON.valNoCallback = time.time;

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
                var animationNames = animation.index.clipNames.ToList();

                _animationJSON.choices = animationNames;

                for (var i = 0; i < animation.index.segmentNames.Count; i++)
                {
                    RegisterPlaySegmentTrigger(animation.index.segmentNames[i]);
                }

                for (var i = 0; i < animationNames.Count; i++)
                {
                    var animName = animationNames[i];
                    if (_clipStorables.Any(a => a.animationName == animName)) continue;
                    CreateAndRegisterClipStorables(animName);
                }

                foreach (var group in animation.clips.GroupBy(c => c.animationNameGroup).Where(g => g.Key != null && g.Count() > 1))
                {
                    CreateAndRegisterGroupStorables(group.Key);
                }

                if (_clipStorables.Count > animationNames.Count)
                {
                    for (var i = 0; i < _clipStorables.Count; i++)
                    {
                        var action = _clipStorables[i];
                        if (!animationNames.Contains(action.animationName))
                        {
                            DeregisterAction(action);
                            _clipStorables.RemoveAt(i);
                            i--;
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

        private void RegisterPlaySegmentTrigger(string segmentName)
        {
            var playSegmentName = $"Play Segment {segmentName}";
            if (IsAction(playSegmentName)) return;
            var playSegmentJSON = new JSONStorableAction(playSegmentName, () =>
            {
                if (logger.triggersReceived) logger.Log(logger.triggersCategory, $"Triggered '{playSegmentName}'");
                animation.PlaySegment(segmentName);
            });
            RegisterAction(playSegmentJSON);
        }

        private void CreateAndRegisterGroupStorables(string groupKey)
        {
            var playRandomizedGroupName = $"Play {groupKey}{AtomAnimationClip.RandomizeGroupSuffix}";
            RegisterAction(new JSONStorableAction(playRandomizedGroupName, () =>
            {
                if (logger.triggersReceived) logger.Log(logger.triggersCategory, $"Triggered '{playRandomizedGroupName}'");
                animation.PlayRandom(groupKey);
            }));

            var setSpeedName = $"Set Speed {groupKey}{AtomAnimationClip.RandomizeGroupSuffix}";
            var setSpeedJSON = new JSONStorableFloat(setSpeedName, 0f, -1f, 5f, false);
            setSpeedJSON.setCallbackFunction = val =>
            {
                foreach (var clip in animation.clips.Where(c => c.animationNameGroup == groupKey))
                    clip.speed = val;
                setSpeedJSON.valNoCallback = 0;
            };
            RegisterFloat(setSpeedJSON);

            var setWeightJSON = new JSONStorableFloat($"Set Weight {groupKey}{AtomAnimationClip.RandomizeGroupSuffix}", 1f, 0f, 1f);
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
            var playName = $"Play {animationName}";
            var playClipJSON = new JSONStorableAction(playName, () =>
            {
                if (logger.triggersReceived) logger.Log(logger.triggersCategory, $"Triggered '{playName}'");
                animation.PlayClipByName(animationName, true);
            });
            RegisterAction(playClipJSON);

            var speedClipJSON = new JSONStorableFloat($"Speed {animationName}", 1f, val =>
            {
                foreach (var clip in animation.index.ByName(animationName))
                    clip.speed = val;
            }, -1f, 5f, false)
            {
                valNoCallback = animation.index.ByName(animationName).First().speed,
                isStorable = false,
                isRestorable = false
            };
            RegisterFloat(speedClipJSON);

            var weightJSON = new JSONStorableFloat($"Weight {animationName}", 1f, val =>
            {
                foreach (var clip in animation.index.ByName(animationName))
                    clip.weight = val;
            }, 0f, 1f)
            {
                valNoCallback = animation.index.ByName(animationName).First().weight,
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
                _scrubberJSON.max = animationEditContext.current.animationLength;
                _scrubberJSON.valNoCallback = animationEditContext.clipTime;
                _timeJSON.valNoCallback = animationEditContext.playTime;

                BroadcastToControllers(nameof(IRemoteControllerPlugin.OnTimelineAnimationParametersChanged));
            }
            catch (Exception exc)
            {
                SuperController.LogError($"Timeline.{nameof(AtomPlugin)}.{nameof(OnAnimationParametersChanged)}: {exc}");
            }
        }

        private void OnSpeedChanged()
        {
            _speedJSON.valNoCallback = animation.globalSpeed;
        }

        private void OnWeightChanged()
        {
            _weightJSON.valNoCallback = animation.globalWeight;
        }

        private void OnEditorSettingsChanged(string propertyName)
        {
            try
            {
                _lockedJSON.valNoCallback = animationEditContext.locked;
            }
            catch (Exception exc)
            {
                SuperController.LogError($"Timeline.{nameof(AtomPlugin)}.{nameof(OnEditorSettingsChanged)}: {exc}");
            }
        }

        private void OnIsPlayingChanged(AtomAnimationClip clip)
        {
            _isPlayingJSON.valNoCallback = animation.isPlaying;
            _freeControllerHook.enabled = !animation.isPlaying;
            if (animation.isPlaying)
                peers.SendPlaybackState(clip);
        }

        private void OnSegmentPlayed(AtomAnimationClip clip)
        {
            if (clip.isOnNoneSegment || clip.isOnSharedSegment)
                return;
            peers.SendPlaySegment(clip);
        }

        private void OnClipIsPlayingChanged(AtomAnimationClip clip)
        {
            if (animation.master && clip.playbackEnabled && animation.sequencing)
                peers.SendMasterClipState(clip);
        }

        private void OnPauseChanged()
        {
            _pausedJSON.valNoCallback = animation.paused;
            peers.SendPaused();
        }


        private void OnAtomRemoved(Atom atom)
        {
            if (animation == null) return;

            // Remove deleted controllers
            foreach (var controllerRef in animation.animatables.controllers.Where(r => !r.owned))
            {
                if (controllerRef.controller.containingAtom == atom)
                {
                    foreach (var clip in animation.clips)
                    {
                        var target = clip.targetControllers.FirstOrDefault(t => t.animatableRef == controllerRef);
                        if (target != null)
                        {
                            clip.Remove(target);
                        }
                    }
                    SuperController.LogError($"Timeline: Atom {atom.name} was removed from the scene, references to its controller {controllerRef.GetFullName()} were removed from atom {containingAtom.name}.");
                }
            }

            // Remove deleted float params
            foreach (var floatRef in animation.animatables.storableFloats.Where(r => !r.owned))
            {
                if (!floatRef.EnsureAvailable()) continue;
                if (floatRef.storable.containingAtom == atom)
                {
                    foreach (var clip in animation.clips)
                    {
                        var target = clip.targetFloatParams.FirstOrDefault(t => t.animatableRef == floatRef);
                        if (target != null)
                        {
                            clip.Remove(target);
                        }
                    }
                    SuperController.LogError($"Timeline: Atom {atom.name} was removed from the scene, references to its float param {floatRef.GetFullName()} were removed from atom {containingAtom.name}.");
                }
            }

            animation.CleanupAnimatables();

            // Update parenting
            foreach (var clip in animation.clips)
            {
                foreach (var target in clip.targetControllers)
                {
                    if (target.parentAtomId == atom.uid)
                    {
                        SuperController.LogError($"Timeline: Atom {atom.name} was removed from the scene, it was used as the parent of {target.animatableRef.controller.containingAtom.name} {target.animatableRef.controller.name} in animation {clip.animationNameQualified}. Animation will be broken.");
                        target.SetParent(null, null);
                    }
                }
            }
        }

        private void OnAtomUIDRename(string oldname, string newname)
        {
            if (animation == null) return;

            // Update parenting
            foreach (var clip in animation.clips)
            {
                foreach (var target in clip.targetControllers)
                {
                    if (target.parentAtomId == oldname)
                    {
                        target.SetParent(newname, target.parentRigidbodyId);
                    }
                }
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
                var clip = animation.index.ByName(animation.playingAnimationSegment, animationName).FirstOrDefault() ?? animation.index.ByName(animationName).FirstOrDefault();
                if (clip == null) return;
                if (animationEditContext.current != clip)
                    animationEditContext.SelectAnimation(clip);
                else if (animation.isPlaying)
                    animation.PlayClipByName(animationName, true);
                _animationJSON.valNoCallback = "";
            }
            catch (Exception exc)
            {
                SuperController.LogError($"Timeline.{nameof(AtomPlugin)}.{nameof(ChangeAnimationLegacy)}: {exc}");
            }
        }

        public void ChangeScreen(string screenName, object screenArg)
        {
            if (_ui == null) return;

            _ui.screensManager.ChangeScreen(screenName, screenArg);
            // If the selection cannot be dispatched, change the controller injected ui up front
            if (!_ui.isActiveAndEnabled && _controllerInjectedUI != null)
            {
                _controllerInjectedUI.screensManager.ChangeScreen(screenName, screenArg);
            }
        }

        #endregion

        #region Controller integration

        public void VamTimelineConnectController(Dictionary<string, object> dict)
        {
            var proxy = SyncProxy.Wrap(dict);
            proxy.animation = _animationJSON;
            proxy.isPlaying = _isPlayingJSON;
            proxy.nextFrame = _nextFrameJSON;
            proxy.play = _playJSON;
            proxy.playIfNotPlaying = _playIfNotPlayingJSON;
            proxy.previousFrame = _previousFrameJSON;
            proxy.stop = _stopJSON;
            proxy.stopAndReset = _stopAndResetJSON;
            proxy.time = _timeJSON;
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
                _controllerInjectedUI.popupParent = _controllerInjectedUI.transform.parent;
                _controllerInjectedUI.Bind(this, _ui.screensManager.GetDefaultScreen());
                _controllerInjectedUI.screensManager.onScreenChanged.AddListener(args =>
                {
                    _ui.screensManager.ChangeScreen(args.screenName, args.screenArg);
                    peers.SendScreen(args.screenName, args.screenArg);
                });
            }
            if (_controllerInjectedUI.animationEditContext != animationEditContext)
                _controllerInjectedUI.Bind(animationEditContext);
        }

        private void DestroyControllerPanel()
        {
            if (_controllerInjectedUI == null) return;
            var go = _controllerInjectedUI.gameObject;
            go.transform.SetParent(null, false);
            Destroy(go);
            _controllerInjectedUI = null;
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

        public void OnBindingsListRequested(List<object> bindings)
        {
            bindings.Add(new []
            {
                new KeyValuePair<string, string>("Namespace", "Timeline")
            });
            bindings.Add(new JSONStorableAction("OpenUI", SelectAndOpenUI));
            bindings.Add(new JSONStorableAction("OpenUI_AnimationsTab", () => { ChangeScreen(AnimationsScreen.ScreenName, null); SelectAndOpenUI(); }));
            bindings.Add(new JSONStorableAction("OpenUI_AddAnimationsTab", () => { ChangeScreen(AddAnimationsScreen.ScreenName, null); SelectAndOpenUI(); }));
            bindings.Add(new JSONStorableAction("OpenUI_ManageAnimationsTab", () => { ChangeScreen(ManageAnimationsScreen.ScreenName, null); SelectAndOpenUI(); }));
            bindings.Add(new JSONStorableAction("OpenUI_TargetsTab", () => { ChangeScreen(TargetsScreen.ScreenName, null); SelectAndOpenUI(); }));
            bindings.Add(new JSONStorableAction("OpenUI_AddRemoveTargetsTab", () => { ChangeScreen(AddRemoveTargetsScreen.ScreenName, null); SelectAndOpenUI(); }));
            bindings.Add(new JSONStorableAction("OpenUI_EditTab", () => { ChangeScreen(EditAnimationScreen.ScreenName, null); SelectAndOpenUI(); }));
            bindings.Add(new JSONStorableAction("OpenUI_SequenceTab", () => { ChangeScreen(SequencingScreen.ScreenName, null); SelectAndOpenUI(); }));
            bindings.Add(new JSONStorableAction("OpenUI_MoreTab", () => { ChangeScreen(MoreScreen.ScreenName, null); SelectAndOpenUI(); }));
            bindings.Add(new JSONStorableAction("OpenUI_MoreTab_ImportExportAnimations", () => { ChangeScreen(ImportExportScreen.ScreenName, null); SelectAndOpenUI(); }));
            bindings.Add(new JSONStorableAction("OpenUI_MoreTab_BulkChanges", () => { ChangeScreen(BulkScreen.ScreenName, null); SelectAndOpenUI(); }));
            bindings.Add(new JSONStorableAction("OpenUI_MoreTab_Mocap", () => { ChangeScreen(MocapScreen.ScreenName, null); SelectAndOpenUI(); }));
            bindings.Add(new JSONStorableAction("OpenUI_MoreTab_Record", () => { ChangeScreen(RecordScreen.ScreenName, null); SelectAndOpenUI(); }));
            bindings.Add(new JSONStorableAction("OpenUI_MoreTab_Reduce", () => { ChangeScreen(ReduceScreen.ScreenName, null); SelectAndOpenUI(); }));
            bindings.Add(new JSONStorableAction("OpenUI_MoreTab_AdvancedKeyframeTools", () => { ChangeScreen(AdvancedKeyframeToolsScreen.ScreenName, null); SelectAndOpenUI(); }));
            bindings.Add(new JSONStorableAction("OpenUI_MoreTab_Diagnostics", () => { ChangeScreen(DiagnosticsScreen.ScreenName, null); SelectAndOpenUI(); }));
            bindings.Add(new JSONStorableAction("OpenUI_MoreTab_Options", () => { ChangeScreen(OptionsScreen.ScreenName, null); SelectAndOpenUI(); }));
            bindings.Add(new JSONStorableAction("OpenUI_MoreTab_Logging", () => { ChangeScreen(LoggingScreen.ScreenName, null); SelectAndOpenUI(); }));
            bindings.Add(new JSONStorableAction("Toggle_DopeSheetMode", () => _ui.ToggleDopeSheetMode()));
            bindings.Add(new JSONStorableAction("Toggle_ExpandCollapseRightPanel", () => _ui.ToggleExpandCollapse()));
            bindings.Add(new JSONStorableAction("Toggle_SelectAllTargets", () => animationEditContext.SelectAll(!animationEditContext.current.GetAllTargets().Any(t => t.selected))));
            bindings.Add(new JSONStorableAction("PreviousFrame", animationEditContext.PreviousFrame));
            bindings.Add(new JSONStorableAction("NextFrame", animationEditContext.NextFrame));
            bindings.Add(new JSONStorableAction("PreviousAnimationInCurrentLayer", () => animationEditContext.GoToPreviousAnimation(animationEditContext.current.animationLayerQualifiedId)));
            bindings.Add(new JSONStorableAction("NextAnimationInCurrentLayer", () => animationEditContext.GoToNextAnimation(animationEditContext.current.animationLayerQualifiedId)));
            bindings.Add(new JSONStorableAction("PreviousAnimationInMainLayer", _previousAnimationInMainLayerJSON.actionCallback));
            bindings.Add(new JSONStorableAction("NextAnimationInMainLayer", _nextAnimationInMainLayerJSON.actionCallback));
            bindings.Add(new JSONStorableAction("Select_Animation#1", () => animationEditContext.SelectAnimation(animationEditContext.currentLayer.Skip(0).FirstOrDefault())));
            bindings.Add(new JSONStorableAction("Select_Animation#2", () => animationEditContext.SelectAnimation(animationEditContext.currentLayer.Skip(1).FirstOrDefault())));
            bindings.Add(new JSONStorableAction("Select_Animation#3", () => animationEditContext.SelectAnimation(animationEditContext.currentLayer.Skip(2).FirstOrDefault())));
            bindings.Add(new JSONStorableAction("Select_Animation#4", () => animationEditContext.SelectAnimation(animationEditContext.currentLayer.Skip(3).FirstOrDefault())));
            bindings.Add(new JSONStorableAction("Select_Animation#5", () => animationEditContext.SelectAnimation(animationEditContext.currentLayer.Skip(4).FirstOrDefault())));
            bindings.Add(new JSONStorableAction("Select_Animation#6", () => animationEditContext.SelectAnimation(animationEditContext.currentLayer.Skip(5).FirstOrDefault())));
            bindings.Add(new JSONStorableAction("Select_Animation#7", () => animationEditContext.SelectAnimation(animationEditContext.currentLayer.Skip(6).FirstOrDefault())));
            bindings.Add(new JSONStorableAction("Select_Animation#8", () => animationEditContext.SelectAnimation(animationEditContext.currentLayer.Skip(7).FirstOrDefault())));
            bindings.Add(new JSONStorableAction("Select_Animation#9", () => animationEditContext.SelectAnimation(animationEditContext.currentLayer.Skip(8).FirstOrDefault())));
            bindings.Add(new JSONStorableAction("Select_Layer#1", () => animationEditContext.SelectLayer(animationEditContext.currentSegment.layerNames.Skip(0).FirstOrDefault())));
            bindings.Add(new JSONStorableAction("Select_Layer#2", () => animationEditContext.SelectLayer(animationEditContext.currentSegment.layerNames.Skip(1).FirstOrDefault())));
            bindings.Add(new JSONStorableAction("Select_Layer#3", () => animationEditContext.SelectLayer(animationEditContext.currentSegment.layerNames.Skip(2).FirstOrDefault())));
            bindings.Add(new JSONStorableAction("Select_Layer#4", () => animationEditContext.SelectLayer(animationEditContext.currentSegment.layerNames.Skip(3).FirstOrDefault())));
            bindings.Add(new JSONStorableAction("Select_Layer#5", () => animationEditContext.SelectLayer(animationEditContext.currentSegment.layerNames.Skip(4).FirstOrDefault())));
            bindings.Add(new JSONStorableAction("Select_Layer#6", () => animationEditContext.SelectLayer(animationEditContext.currentSegment.layerNames.Skip(5).FirstOrDefault())));
            bindings.Add(new JSONStorableAction("Select_Layer#7", () => animationEditContext.SelectLayer(animationEditContext.currentSegment.layerNames.Skip(6).FirstOrDefault())));
            bindings.Add(new JSONStorableAction("Select_Layer#8", () => animationEditContext.SelectLayer(animationEditContext.currentSegment.layerNames.Skip(7).FirstOrDefault())));
            bindings.Add(new JSONStorableAction("Select_Layer#9", () => animationEditContext.SelectLayer(animationEditContext.currentSegment.layerNames.Skip(8).FirstOrDefault())));
            bindings.Add(new JSONStorableAction("Select_Segment#1", () => animationEditContext.SelectSegment(animation.index.segmentNames.Skip(0).FirstOrDefault())));
            bindings.Add(new JSONStorableAction("Select_Segment#2", () => animationEditContext.SelectSegment(animation.index.segmentNames.Skip(1).FirstOrDefault())));
            bindings.Add(new JSONStorableAction("Select_Segment#3", () => animationEditContext.SelectSegment(animation.index.segmentNames.Skip(2).FirstOrDefault())));
            bindings.Add(new JSONStorableAction("Select_Segment#4", () => animationEditContext.SelectSegment(animation.index.segmentNames.Skip(3).FirstOrDefault())));
            bindings.Add(new JSONStorableAction("Select_Segment#5", () => animationEditContext.SelectSegment(animation.index.segmentNames.Skip(4).FirstOrDefault())));
            bindings.Add(new JSONStorableAction("Select_Segment#6", () => animationEditContext.SelectSegment(animation.index.segmentNames.Skip(5).FirstOrDefault())));
            bindings.Add(new JSONStorableAction("Select_Segment#7", () => animationEditContext.SelectSegment(animation.index.segmentNames.Skip(6).FirstOrDefault())));
            bindings.Add(new JSONStorableAction("Select_Segment#8", () => animationEditContext.SelectSegment(animation.index.segmentNames.Skip(7).FirstOrDefault())));
            bindings.Add(new JSONStorableAction("Select_Segment#9", () => animationEditContext.SelectSegment(animation.index.segmentNames.Skip(8).FirstOrDefault())));
            bindings.Add(new JSONStorableAction("PreviousLayer", () => animationEditContext.GoToPreviousLayer()));
            bindings.Add(new JSONStorableAction("NextLayer", () => animationEditContext.GoToNextLayer()));
            bindings.Add(new JSONStorableAction("PreviousSegment", () => animationEditContext.GoToPreviousSegment()));
            bindings.Add(new JSONStorableAction("NextSegment", () => animationEditContext.GoToNextSegment()));
            bindings.Add(new JSONStorableAction("PlayCurrentClip", animationEditContext.PlayCurrentClip));
            bindings.Add(new JSONStorableAction("PlayAll", animationEditContext.PlayAll));
            bindings.Add(new JSONStorableAction("Stop", animationEditContext.Stop));
            bindings.Add(new JSONStorableAction("StopAndReset", animationEditContext.StopAndReset));
            bindings.Add(new JSONStorableAction("StopAllSceneAnimations", () => { animationEditContext.Stop(); SuperController.singleton.motionAnimationMaster.StopPlayback(); }));
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
            bindings.Add(new JSONStorableAction("Keyframe_SetCurveType_SmoothLocal", () => animationEditContext.ChangeCurveType(CurveTypeValues.SmoothLocal)));
            bindings.Add(new JSONStorableAction("Keyframe_SetCurveType_SmoothGlobal", () => animationEditContext.ChangeCurveType(CurveTypeValues.SmoothGlobal)));
            bindings.Add(new JSONStorableAction("Keyframe_SetCurveType_Linear", () => animationEditContext.ChangeCurveType(CurveTypeValues.Linear)));
            bindings.Add(new JSONStorableAction("Keyframe_SetCurveType_Flat", () => animationEditContext.ChangeCurveType(CurveTypeValues.Flat)));
            bindings.Add(new JSONStorableAction("Keyframe_Add_CurrentController", () => operations.Keyframes().AddSelectedController()));
            bindings.Add(new JSONStorableAction("Keyframe_Add_AllControllerTargets", () => operations.Keyframes().AddAllControllers()));
            bindings.Add(new JSONStorableAction("ZoomIn", () => animationEditContext.ZoomScrubberRangeIn()));
            bindings.Add(new JSONStorableAction("ZoomOut", () => animationEditContext.ZoomScrubberRangeOut()));
            bindings.Add(new JSONStorableAction("ZoomMoveBackward", () => animationEditContext.MoveScrubberRangeBackward()));
            bindings.Add(new JSONStorableAction("ZoomMoveForward", () => animationEditContext.MoveScrubberRangeForward()));
            bindings.Add(new JSONStorableAction("ZoomReset", () => animationEditContext.ResetScrubberRange()));
            bindings.Add(new JSONStorableAction("Lock", () => animationEditContext.locked = true));
            bindings.Add(new JSONStorableAction("Unlock", () => animationEditContext.locked = false));
            bindings.Add(new JSONStorableAction("ApplyPose", () => animationEditContext.current.pose?.Apply()));
            bindings.Add(new JSONStorableAction("SavePose", () => animationEditContext.current.pose = AtomPose.FromAtom(containingAtom, animationEditContext.current.pose)));
            bindings.Add(new JSONStorableAction("StartRecord", () =>
            {
                var targets = animationEditContext.GetSelectedTargets().OfType<ICurveAnimationTarget>().ToList();
                if (targets.Count == 0)
                {
                    SuperController.LogError("Timeline: No targets selected for recording");
                    return;
                }
                StartCoroutine(operations.Record().StartRecording(
                    this,
                    TimeModes.RealTime,
                    animationEditContext.current.GetAllCurveTargets().All(t => t.GetLeadCurve().length == 2),
                    animationEditContext.startRecordIn,
                    targets,
                    null,
                    false,
                    true
                ));
            }));
            bindings.Add(new JSONStorableAction("AddTarget_SelectControllerFromScene", () =>
            {
                SuperController.singleton.SelectModeControllers(targetCtrl =>
                {
                    operations.Targets().Add(targetCtrl);
                });
            }));
            bindings.Add(new JSONStorableAction("Logging_EnableDefaultThis", () =>
            {
                logger.clearOnPlay = true;
                logger.EnableDefault();
            }));
            bindings.Add(new JSONStorableAction("Logging_EnableDefaultAll", () =>
            {
                logger.clearOnPlay = true;
                logger.EnableDefault();
                peers.SendLoggingSettings();
            }));
            bindings.Add(new JSONStorableAction("ToggleFocusOnLayer", () => animation.focusOnLayer = !animation.focusOnLayer));

            bindings.Add(_scrubberAnalogControlJSON);
        }

        private void SelectAndOpenUI()
        {
            if (containingAtom == null) return;
            if (UITransform != null && UITransform.gameObject.activeInHierarchy) return;

            if (SuperController.singleton.gameMode != SuperController.GameMode.Edit) SuperController.singleton.gameMode = SuperController.GameMode.Edit;

            #if (VAM_GT_1_20)
            SuperController.singleton.SelectController(containingAtom.mainController, false, false, true);
            #else
            SuperController.singleton.SelectController(containingAtom.mainController);
            #endif
            SuperController.singleton.ShowMainHUDAuto();
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
                if (UITransform == null) continue;
                UITransform.gameObject.SetActive(true);
                yield break;
            }
        }

        #endregion
    }
}

