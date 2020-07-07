using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class ControllerPlugin : MVRScript, ITimelineListener
    {
        private const string _atomSeparator = ";";

        private Atom _atom;
        private SimpleSignUI _ui;
        private JSONStorableBool _autoPlayJSON;
        private JSONStorableBool _hideJSON;
        private JSONStorableBool _enableKeyboardShortcuts;
        private JSONStorableBool _lockedJSON;
        private JSONStorableStringChooser _atomsJSON;
        private JSONStorableStringChooser _animationJSON;
        private JSONStorableFloat _timeJSON;
        private JSONStorableAction _playJSON;
        private JSONStorableAction _playIfNotPlayingJSON;
        private JSONStorableAction _stopJSON;
        private UIDynamic _injectedUIContainer;
        private GameObject _injectedUI;
        private readonly List<SyncProxy> _links = new List<SyncProxy>();
        private SyncProxy _selectedLink;
        private bool _ignoreVamTimelineAnimationFrameUpdated;

        #region Initialization

        public override void Init()
        {
            try
            {
                _atom = GetAtom();
                InitStorables();
                InitCustomUI();
                if (!_hideJSON.val)
                    OnEnable();
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(ControllerPlugin)}.{nameof(Init)}: " + exc);
            }
        }

        private Atom GetAtom()
        {
            // Note: Yeah, that's horrible, but containingAtom is null
            var container = gameObject?.transform?.parent?.parent?.parent?.parent?.parent?.gameObject;
            if (container == null)
                throw new NullReferenceException($"Could not find the parent gameObject.");
            var atom = container.GetComponent<Atom>();
            if (atom == null)
                throw new NullReferenceException($"Could not find the parent atom in {container.name}.");
            if (atom.type != "SimpleSign")
                throw new InvalidOperationException("Can only be applied on SimpleSign. This plugin is used to synchronize multiple atoms; use VamTimeline.AtomAnimation.cslist to animate an atom.");
            return atom;
        }

        private void InitStorables()
        {
            _autoPlayJSON = new JSONStorableBool("Auto Play", false);
            RegisterBool(_autoPlayJSON);

            _hideJSON = new JSONStorableBool("Hide", false, (bool val) => Hide(val));
            RegisterBool(_hideJSON);

            _enableKeyboardShortcuts = new JSONStorableBool("Enable Keyboard Shortcuts", false);
            RegisterBool(_enableKeyboardShortcuts);

            _lockedJSON = new JSONStorableBool("Locked (Performance)", false, (bool val) => Lock(val))
            {
                isStorable = false,
                isRestorable = false
            };
            RegisterBool(_lockedJSON);

            _atomsJSON = new JSONStorableStringChooser("Atoms Selector", new List<string>(), "", "Atoms", (string v) => SelectCurrentAtom(v))
            {
                isStorable = false,
                isRestorable = false
            };

            _animationJSON = new JSONStorableStringChooser("Animation", new List<string>(), "", "Animation", (string v) => ChangeAnimation(v))
            {
                isStorable = false,
                isRestorable = false
            };
            RegisterStringChooser(_animationJSON);

            _playJSON = new JSONStorableAction("Play", () => Play());
            RegisterAction(_playJSON);

            _playIfNotPlayingJSON = new JSONStorableAction("Play If Not Playing", () => PlayIfNotPlaying());
            RegisterAction(_playIfNotPlayingJSON);

            _stopJSON = new JSONStorableAction("Stop", () => Stop());
            RegisterAction(_stopJSON);

            _timeJSON = new JSONStorableFloat("Time", 0f, v => _selectedLink.time.val = v, 0f, 2f, true)
            {
                isStorable = false,
                isRestorable = false
            };
            RegisterFloat(_timeJSON);

            var nextAnimationJSON = new JSONStorableAction(StorableNames.NextAnimation, () =>
            {
                var i = _animationJSON.choices.IndexOf(_selectedLink?.animation.val);
                if (i < 0 || i > _animationJSON.choices.Count - 2) return;
                _animationJSON.val = _animationJSON.choices[i + 1];
            });
            RegisterAction(nextAnimationJSON);

            var previousAnimationJSON = new JSONStorableAction(StorableNames.PreviousAnimation, () =>
            {
                var i = _animationJSON.choices.IndexOf(_selectedLink?.animation.val);
                if (i < 1 || i > _animationJSON.choices.Count - 1) return;
                _animationJSON.val = _animationJSON.choices[i - 1];
            });
            RegisterAction(previousAnimationJSON);

            StartCoroutine(InitDeferred());
        }

        private IEnumerator InitDeferred()
        {
            if (_hideJSON.val)
                OnDisable();

            while (SuperController.singleton.isLoading)
                yield return 0;

            yield return 0;

            ScanForAtoms();

            while (SuperController.singleton.freezeAnimation)
                yield return 0;

            yield return 0;

            if (_autoPlayJSON.val && _selectedLink != null)
                PlayIfNotPlaying();
        }

        private void ScanForAtoms()
        {
            foreach (var atom in SuperController.singleton.GetAtoms())
            {
                TryConnectAtom(atom);
            }
        }

        public void OnTimelineAnimationReady(JSONStorable storable)
        {
            var link = TryConnectAtom(storable);
            if (GetOrDispose(_selectedLink)?.storable == storable)
            {
                RequestControlPanelInjection();
            }
        }

        public void OnTimelineAnimationDisabled(JSONStorable storable)
        {
            var link = _links.FirstOrDefault(l => l.storable == storable);
            if (link == null) return;
            _links.Remove(link);
            _atomsJSON.choices = _links.Select(l => l.storable.containingAtom.uid).ToList();
            if (_selectedLink == link)
                _atomsJSON.val = _links.Select(GetOrDispose).FirstOrDefault()?.storable.containingAtom.uid;
            link.Dispose();
        }

        private SyncProxy TryConnectAtom(Atom atom)
        {
            if (atom == null) return null;
            var storableId = atom.GetStorableIDs().FirstOrDefault(id => id.EndsWith("VamTimeline.AtomPlugin"));
            if (storableId == null) return null;
            var storable = atom.GetStorableByID(storableId);
            return TryConnectAtom(storable);
        }

        private SyncProxy TryConnectAtom(JSONStorable storable)
        {
            foreach (var l in _links.ToArray())
            {
                GetOrDispose(l);
            }

            var existing = _links.FirstOrDefault(a => a.storable == storable);
            if (existing != null) { return existing; }

            var proxy = new SyncProxy()
            {
                storable = storable
            };

            storable.SendMessage(nameof(IRemoteAtomPlugin.VamTimelineConnectController), proxy.dict, SendMessageOptions.RequireReceiver);

            if (!proxy.connected)
            {
                proxy.Dispose();
                return null;
            }

            _links.Add(proxy);
            _links.Sort((SyncProxy s1, SyncProxy s2) => string.Compare(s1.storable.containingAtom.name, s2.storable.containingAtom.name));

            var list = new List<string>(_atomsJSON.choices)
            {
                proxy.storable.containingAtom.uid
            };
            list.Sort();
            _atomsJSON.choices = list;

            if (_selectedLink == null)
            {
                _selectedLink = proxy;
                proxy.main = true;
                _atomsJSON.val = proxy.storable.containingAtom.uid;
            }

            return proxy;
        }

        private void Hide(bool val)
        {
            if (val)
                OnDisable();
            else
                OnEnable();
        }

        private void InitCustomUI()
        {
            var resyncButton = CreateButton("Re-Sync Atom Plugins");
            resyncButton.button.onClick.AddListener(() =>
            {
                ScanForAtoms();
            });

            CreateToggle(_autoPlayJSON, true);

            CreateToggle(_hideJSON, true);

            CreateToggle(_enableKeyboardShortcuts, true);
        }

        #endregion

        #region Lifecycle

        public void OnEnable()
        {
            if (_atom == null || _ui != null) return;

            try
            {
                _ui = new SimpleSignUI(_atom, this);
                _ui.CreateUIPopupInCanvas(_atomsJSON);
                _injectedUIContainer = _ui.CreateUISpacerInCanvas(1060f);
                ScanForAtoms();
                if (_selectedLink != null)
                    RequestControlPanelInjection();
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(ControllerPlugin)}.{nameof(OnEnable)}: " + exc);
            }
        }

        public void OnDisable()
        {
            if (_ui == null) return;

            try
            {
                foreach (var link in _links)
                {
                    link.Dispose();
                }
                _links.Clear();
                DestroyControlPanelContainer();
                _timeJSON.slider = null;
                _ui.Dispose();
                _ui = null;
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(ControllerPlugin)}.{nameof(OnDisable)}: " + exc);
            }
        }

        public void OnDestroy()
        {
            OnDisable();
        }

        #endregion

        public void OnTimelineAnimationParametersChanged(JSONStorable storable)
        {
            var proxy = GetOrDispose(_selectedLink);
            if (proxy == null || proxy.storable != storable)
                return;

            OnTimelineTimeChanged(storable);

            var remoteTime = proxy.time;
            _timeJSON.max = remoteTime.max;
            _timeJSON.valNoCallback = remoteTime.val;
            var remoteAnimation = proxy.animation;
            _animationJSON.choices = remoteAnimation.choices;
            _animationJSON.valNoCallback = remoteAnimation.val;
            _lockedJSON.valNoCallback = proxy.locked.val;
        }

        public void OnTimelineTimeChanged(JSONStorable storable)
        {
            if (_ignoreVamTimelineAnimationFrameUpdated) return;
            _ignoreVamTimelineAnimationFrameUpdated = true;

            try
            {
                var proxy = GetOrDispose(_selectedLink);
                if (proxy == null || proxy.storable != storable)
                    return;

                var animationName = proxy.animation.val;
                var isPlaying = proxy.isPlaying.val;
                var time = proxy.time.val;
            }
            finally
            {
                _ignoreVamTimelineAnimationFrameUpdated = false;
            }
        }

        public void Update()
        {
            try
            {
                HandleKeyboardShortcuts();

                if (Time.frameCount % 2 == 0) return;

                var proxy = GetOrDispose(_selectedLink);
                if (proxy == null) return;

                var time = proxy.time;
                if (time != null && time.val != _timeJSON.val)
                {
                    _timeJSON.valNoCallback = time.val;
                }
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(ControllerPlugin)}.{nameof(Update)}: " + exc);
                _atomsJSON.val = "";
            }
        }

        private void HandleKeyboardShortcuts()
        {
            var proxy = GetOrDispose(_selectedLink);
            if (proxy == null) return;
            if (!_enableKeyboardShortcuts.val) return;

            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                PreviousFrame();
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                NextFrame();
            }
            else if (Input.GetKeyDown(KeyCode.Space))
            {
                if (proxy.isPlaying.val)
                    Stop();
                else
                    Play();
            }
            else if (Input.GetKeyDown(KeyCode.Escape))
            {
                Stop();
            }
            else if (Input.GetKeyDown(KeyCode.PageUp))
            {
                if (_atomsJSON.choices.Count > 1 && _atomsJSON.val != _atomsJSON.choices[0])
                    _atomsJSON.val = _atomsJSON.choices.ElementAtOrDefault(_atomsJSON.choices.IndexOf(_atomsJSON.val) - 1);
            }
            else if (Input.GetKeyDown(KeyCode.PageDown))
            {
                if (_atomsJSON.choices.Count > 1 && _atomsJSON.val != _atomsJSON.choices[_atomsJSON.choices.Count - 1])
                    _atomsJSON.val = _atomsJSON.choices.ElementAtOrDefault(_atomsJSON.choices.IndexOf(_atomsJSON.val) + 1);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                ChangeAnimation(proxy.animation.choices.ElementAtOrDefault(0));
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                ChangeAnimation(proxy.animation.choices.ElementAtOrDefault(1));
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                ChangeAnimation(proxy.animation.choices.ElementAtOrDefault(2));
            }
            else if (Input.GetKeyDown(KeyCode.Alpha4))
            {
                ChangeAnimation(proxy.animation.choices.ElementAtOrDefault(3));
            }
            else if (Input.GetKeyDown(KeyCode.Alpha5))
            {
                ChangeAnimation(proxy.animation.choices.ElementAtOrDefault(4));
            }
            else if (Input.GetKeyDown(KeyCode.Alpha6))
            {
                ChangeAnimation(proxy.animation.choices.ElementAtOrDefault(5));
            }
            else if (Input.GetKeyDown(KeyCode.Alpha7))
            {
                ChangeAnimation(proxy.animation.choices.ElementAtOrDefault(6));
            }
            else if (Input.GetKeyDown(KeyCode.Alpha8))
            {
                ChangeAnimation(proxy.animation.choices.ElementAtOrDefault(7));
            }
            else if (Input.GetKeyDown(KeyCode.Alpha9))
            {
                ChangeAnimation(proxy.animation.choices.ElementAtOrDefault(8));
            }
        }

        private void Lock(bool val)
        {
            foreach (var animation in _links)
            {
                animation.locked.val = val;
            }
        }

        private void SelectCurrentAtom(string uid)
        {
            if (_selectedLink != null)
            {
                _selectedLink.main = false;
                _selectedLink = null;
            }
            if (string.IsNullOrEmpty(uid))
            {
                return;
            }
            var mainLinkedAnimation = _links.Select(GetOrDispose).FirstOrDefault(la => la.storable.containingAtom.uid == uid);
            if (mainLinkedAnimation == null)
            {
                _atomsJSON.valNoCallback = "";
                return;
            }

            _selectedLink = mainLinkedAnimation;
            _selectedLink.main = true;
            RequestControlPanelInjection();

            _atomsJSON.valNoCallback = _selectedLink.storable.containingAtom.uid;
        }

        private void RequestControlPanelInjection()
        {
            if (_injectedUIContainer == null) return;

            DestroyControlPanelContainer();

            var proxy = GetOrDispose(_selectedLink);
            if (proxy == null) return;

            _injectedUI = new GameObject();
            _injectedUI.transform.SetParent(_injectedUIContainer.transform, false);

            var rect = _injectedUI.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.sizeDelta = new Vector2(0, _injectedUIContainer.height);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(0, 0);

            proxy.storable.SendMessage(nameof(IRemoteAtomPlugin.VamTimelineRequestControlPanel), _injectedUI, SendMessageOptions.RequireReceiver);
        }

        private void DestroyControlPanelContainer()
        {
            if (_injectedUI == null) return;
            _injectedUI.transform.SetParent(null, false);
            Destroy(_injectedUI);
            _injectedUI = null;
        }

        private SyncProxy GetOrDispose(SyncProxy proxy)
        {
            if (proxy == null) return null;
            if (proxy.storable == null)
            {
                var link = _links.FirstOrDefault(l => l == proxy);
                if (link != null)
                {
                    _links.Remove(link);
                }
                proxy.Dispose();
                if (_selectedLink == proxy)
                    _selectedLink = null;
                return null;
            }
            return proxy;
        }

        private void ChangeAnimation(string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            foreach (var la in _links.Select(GetOrDispose))
            {
                if (la.animation.choices.Contains(name))
                    la.animation.val = name;
            }
        }

        private void Play()
        {
            GetOrDispose(_selectedLink)?.play.actionCallback();
        }

        private void PlayIfNotPlaying()
        {
            GetOrDispose(_selectedLink)?.playIfNotPlaying.actionCallback();
        }

        private void Stop()
        {
            GetOrDispose(_selectedLink)?.stop.actionCallback();
        }

        private void NextFrame()
        {
            GetOrDispose(_selectedLink)?.nextFrame.actionCallback();
        }

        private void PreviousFrame()
        {
            GetOrDispose(_selectedLink)?.previousFrame.actionCallback();
        }
    }
}
