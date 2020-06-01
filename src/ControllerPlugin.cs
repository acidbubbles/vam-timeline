using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class ControllerPlugin : MVRScript, IAnimationController
    {
        private const string _atomSeparator = ";";
        private Atom _atom;
        private SimpleSignUI _ui;
        private JSONStorableBool _autoPlayJSON;
        private JSONStorableBool _hideJSON;
        private JSONStorableBool _lockedJSON;
        private JSONStorableStringChooser _atomsJSON;
        private JSONStorableStringChooser _animationJSON;
        private JSONStorableFloat _scrubberJSON;
        private JSONStorableAction _playJSON;
        private JSONStorableAction _playIfNotPlayingJSON;
        private JSONStorableAction _stopJSON;
        private JSONStorableStringChooser _atomsToLink;
        private LinkedAnimation _mainLinkedAnimation;
        private JSONStorableString _savedAtomsJSON;
        private UIDynamicButton _linkButton;
        private bool _ignoreVamTimelineAnimationFrameUpdated;
        private JSONStorableBool _enableKeyboardShortcuts;
        private UIDynamic _controlPanelSpacer;
        private GameObject _controlPanelContainer;
        private readonly List<LinkedAnimation> _linkedAnimations = new List<LinkedAnimation>();
        private bool _ready = false;

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
                isStorable = false
            };
            RegisterBool(_lockedJSON);

            _atomsJSON = new JSONStorableStringChooser("Atoms Selector", new List<string> { "" }, "", "Atoms", (string v) => SelectCurrentAtom(v))
            {
                isStorable = false
            };

            _animationJSON = new JSONStorableStringChooser("Animation", new List<string>(), "", "Animation", (string v) => ChangeAnimation(v))
            {
                isStorable = false
            };
            RegisterStringChooser(_animationJSON);

            _playJSON = new JSONStorableAction("Play", () => Play());
            RegisterAction(_playJSON);

            _playIfNotPlayingJSON = new JSONStorableAction("Play If Not Playing", () => PlayIfNotPlaying());
            RegisterAction(_playIfNotPlayingJSON);

            _stopJSON = new JSONStorableAction("Stop", () => Stop());
            RegisterAction(_stopJSON);

            _scrubberJSON = new JSONStorableFloat("Time", 0f, v => ChangeTime(v), 0f, 2f, true)
            {
                isStorable = false
            };
            RegisterFloat(_scrubberJSON);

            var atoms = GetAtomsWithVamTimelinePlugin().ToList();
            _atomsToLink = new JSONStorableStringChooser("Atom To Link", atoms, atoms.FirstOrDefault() ?? "", "Add");

            _savedAtomsJSON = new JSONStorableString("Atoms", "", (string v) => RestoreAtomsLink(v));
            RegisterString(_savedAtomsJSON);

            var nextAnimationJSON = new JSONStorableAction(StorableNames.NextAnimation, () =>
            {
                var i = _animationJSON.choices.IndexOf(_mainLinkedAnimation?.Animation.val);
                if (i < 0 || i > _animationJSON.choices.Count - 2) return;
                _animationJSON.val = _animationJSON.choices[i + 1];
            });
            RegisterAction(nextAnimationJSON);

            var previousAnimationJSON = new JSONStorableAction(StorableNames.PreviousAnimation, () =>
            {
                var i = _animationJSON.choices.IndexOf(_mainLinkedAnimation?.Animation.val);
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

            _ready = true;

            yield return 0;

            if (string.IsNullOrEmpty(_savedAtomsJSON.val)) yield break;

            RestoreAtomsLink(_savedAtomsJSON.val);

            RequestControlPanelInjection();

            while (SuperController.singleton.freezeAnimation)
                yield return 0;

            yield return 0;

            if (_autoPlayJSON.val && _mainLinkedAnimation != null)
                PlayIfNotPlaying();
        }

        private void Hide(bool val)
        {
            if (val)
                OnDisable();
            else
                OnEnable();
        }

        private IEnumerable<string> GetAtomsWithVamTimelinePlugin()
        {
            var atoms = SuperController.singleton.GetAtoms();
            foreach (var atom in atoms)
            {
                if (atom.GetStorableIDs().Any(id => id.EndsWith("VamTimeline.AtomPlugin")))
                {
                    if (_linkedAnimations.Any(la => la.Atom.uid == atom.uid)) continue;

                    yield return atom.uid;
                }
            }
        }

        private void RestoreAtomsLink(string savedAtoms)
        {
            if (!_ready) return;

            if (!string.IsNullOrEmpty(savedAtoms))
            {
                foreach (var atomUid in savedAtoms.Split(';'))
                {
                    LinkAtom(atomUid);
                }
            }

            if (_mainLinkedAnimation == null && _linkedAnimations.Count > 0)
                SelectCurrentAtom(_linkedAnimations[0].Label);
        }

        private void InitCustomUI()
        {
            var atomsToLinkUI = CreateScrollablePopup(_atomsToLink);
            atomsToLinkUI.popupPanelHeight = 800f;
            atomsToLinkUI.popup.onOpenPopupHandlers += () => _atomsToLink.choices = GetAtomsWithVamTimelinePlugin().ToList();

            _linkButton = CreateButton("Link");
            _linkButton.button.interactable = _atomsToLink.choices.Count > 0;
            _linkButton.button.onClick.AddListener(() => LinkAtom(_atomsToLink.val));

            var resyncButton = CreateButton("Re-Sync Atom Plugins");
            resyncButton.button.onClick.AddListener(() =>
            {
                DestroyControlPanelContainer();
                _mainLinkedAnimation = null;
                RestoreAtomsLink(_savedAtomsJSON.val);
                _atomsToLink.choices = GetAtomsWithVamTimelinePlugin().ToList();
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
                var x = 0f;
                var y = -0.37f;
                _ui = new SimpleSignUI(_atom, this);
                _ui.CreateUIToggleInCanvas(_lockedJSON, x, y + 0.1f);
                _ui.CreateUIPopupInCanvas(_atomsJSON, x, y + 0.355f);
                _ui.CreateUIPopupInCanvas(_animationJSON, x, y + 0.425f);
                _controlPanelSpacer = _ui.CreateUISpacerInCanvas(x, y + 0.375f, 780f);
                if (_mainLinkedAnimation != null)
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
                DestroyControlPanelContainer();
                _scrubberJSON.slider = null;
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

        private void ChangeTime(float v)
        {
            {
                if (_mainLinkedAnimation == null) return;
                var scrubber = _mainLinkedAnimation.Scrubber;
                scrubber.val = v;
                foreach (var link in _linkedAnimations.Where(la => la != _mainLinkedAnimation))
                    link.Time.val = scrubber.val;
                OnTimelineTimeChanged(_mainLinkedAnimation.Atom.uid);
            }
        }

        private void LinkAtom(string uid)
        {
            try
            {
                if (uid.IndexOf(_atomSeparator) > -1)
                {
                    SuperController.LogError($"VamTimeline: Atom '{uid}' cannot contain '{_atomSeparator}'.");
                    return;
                }
                if (_linkedAnimations.Any(la => la.Atom.uid == uid)) return;

                var atom = SuperController.singleton.GetAtomByUid(uid);
                if (atom == null)
                {
                    SuperController.LogError($"VamTimeline: Atom '{uid}' cannot be found. Re-link the atom to fix.");
                    return;
                }

                var link = LinkedAnimation.TryCreate(atom);
                if (link == null)
                {
                    SuperController.LogError($"VamTimeline: Atom '{uid}' did not have the Timeline plugin. Add the plugin and re-link the atom to fix.");
                    return;
                }
                _linkedAnimations.Add(link);
                _atomsJSON.choices = _linkedAnimations.Select(la => la.Label).ToList();
                if (_mainLinkedAnimation == null)
                {
                    SelectCurrentAtom(link.Label);
                }
                _savedAtomsJSON.val = string.Join(_atomSeparator, _linkedAnimations.Select(la => la.Atom.uid).Distinct().ToArray());
                _atomsToLink.choices = GetAtomsWithVamTimelinePlugin().ToList();
                _atomsToLink.val = _atomsToLink.choices.FirstOrDefault() ?? "";
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(ControllerPlugin)}.{nameof(LinkAtom)}: " + exc);
            }
        }

        public void OnTimelineAnimationParametersChanged(string uid)
        {
            if (_mainLinkedAnimation == null || _mainLinkedAnimation.Atom.uid != uid)
                return;

            OnTimelineTimeChanged(uid);

            var remoteScrubber = _mainLinkedAnimation.Scrubber;
            _scrubberJSON.max = remoteScrubber.max;
            _scrubberJSON.valNoCallback = remoteScrubber.val;
            _animationJSON.choices = _mainLinkedAnimation.Animation.choices;
            _animationJSON.valNoCallback = _mainLinkedAnimation.Animation.val;
            _lockedJSON.valNoCallback = _mainLinkedAnimation.Locked.val;
        }

        public void OnTimelineTimeChanged(string uid)
        {
            if (_ignoreVamTimelineAnimationFrameUpdated) return;
            _ignoreVamTimelineAnimationFrameUpdated = true;

            try
            {
                if (_mainLinkedAnimation == null || _mainLinkedAnimation.Atom.uid != uid)
                    return;

                var animationName = _mainLinkedAnimation.Animation.val;
                var isPlaying = _mainLinkedAnimation.IsPlaying.val;
                var time = _mainLinkedAnimation.Time.val;

                foreach (var slave in _linkedAnimations.Where(la => la != _mainLinkedAnimation))
                {
                    if (slave.Animation.val != animationName)
                        slave.ChangeAnimation(animationName);

                    if (isPlaying)
                        slave.PlayIfNotPlaying();
                    else
                        slave.StopIfPlaying();

                    var slaveTime = slave.Time;
                    if (slaveTime.val < time - 0.0005f || slaveTime.val > time + 0.0005f)
                        slaveTime.val = time;
                }
            }
            finally
            {
                _ignoreVamTimelineAnimationFrameUpdated = false;
            }
        }

        public void OnTimelineAnimationReady(string uid)
        {
            if (_mainLinkedAnimation?.Atom.uid == uid)
            {
                RequestControlPanelInjection();
            }
        }

        public void Update()
        {
            try
            {
                if (_mainLinkedAnimation == null) return;

                var scrubber = _mainLinkedAnimation.Scrubber;
                if (scrubber != null && scrubber.val != _scrubberJSON.val)
                {
                    _scrubberJSON.valNoCallback = scrubber.val;
                }

                HandleKeyboardShortcuts();
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(ControllerPlugin)}.{nameof(Update)}: " + exc);
                _atomsJSON.val = "";
            }
        }

        private void HandleKeyboardShortcuts()
        {
            if (_mainLinkedAnimation == null) return;
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
                if (_mainLinkedAnimation.IsPlaying.val)
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
                ChangeAnimation(_mainLinkedAnimation.Animation.choices.ElementAtOrDefault(0));
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                ChangeAnimation(_mainLinkedAnimation.Animation.choices.ElementAtOrDefault(1));
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                ChangeAnimation(_mainLinkedAnimation.Animation.choices.ElementAtOrDefault(2));
            }
            else if (Input.GetKeyDown(KeyCode.Alpha4))
            {
                ChangeAnimation(_mainLinkedAnimation.Animation.choices.ElementAtOrDefault(3));
            }
            else if (Input.GetKeyDown(KeyCode.Alpha5))
            {
                ChangeAnimation(_mainLinkedAnimation.Animation.choices.ElementAtOrDefault(4));
            }
            else if (Input.GetKeyDown(KeyCode.Alpha6))
            {
                ChangeAnimation(_mainLinkedAnimation.Animation.choices.ElementAtOrDefault(5));
            }
            else if (Input.GetKeyDown(KeyCode.Alpha7))
            {
                ChangeAnimation(_mainLinkedAnimation.Animation.choices.ElementAtOrDefault(6));
            }
            else if (Input.GetKeyDown(KeyCode.Alpha8))
            {
                ChangeAnimation(_mainLinkedAnimation.Animation.choices.ElementAtOrDefault(7));
            }
            else if (Input.GetKeyDown(KeyCode.Alpha9))
            {
                ChangeAnimation(_mainLinkedAnimation.Animation.choices.ElementAtOrDefault(8));
            }
        }

        private void Lock(bool val)
        {
            foreach (var animation in _linkedAnimations)
            {
                animation.Locked.val = val;
            }
        }

        private void SelectCurrentAtom(string label)
        {
            if (string.IsNullOrEmpty(label))
            {
                _mainLinkedAnimation = null;
                return;
            }
            var mainLinkedAnimation = _linkedAnimations.FirstOrDefault(la => la.Label == label);
            if (_mainLinkedAnimation == mainLinkedAnimation) return;
            _mainLinkedAnimation = mainLinkedAnimation;
            if (_mainLinkedAnimation == null) return;
            _atomsJSON.valNoCallback = _mainLinkedAnimation.Label;
            StartCoroutine(InitializeMainAtom(_mainLinkedAnimation.Atom.uid));
        }

        private IEnumerator InitializeMainAtom(string uid)
        {
            yield return 0;
            OnTimelineAnimationParametersChanged(uid);
            RequestControlPanelInjection();
        }

        private void RequestControlPanelInjection()
        {
            if (_controlPanelSpacer == null) return;

            DestroyControlPanelContainer();

            if (_mainLinkedAnimation == null) return;

            _controlPanelContainer = new GameObject();
            _controlPanelContainer.transform.SetParent(_controlPanelSpacer.transform, false);

            var rect = _controlPanelContainer.AddComponent<RectTransform>();
            rect.StretchParent();

            _mainLinkedAnimation.Storable.gameObject.BroadcastMessage(nameof(IAnimatedAtom.VamTimelineRequestControlPanelInjection), _controlPanelContainer);
        }

        private void DestroyControlPanelContainer()
        {
            if (_controlPanelContainer == null) return;
            _controlPanelContainer.transform.SetParent(null, false);
            Destroy(_controlPanelContainer);
            _controlPanelContainer = null;
        }

        private void ChangeAnimation(string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            foreach (var la in _linkedAnimations)
                la.ChangeAnimation(name);
        }

        private void Play()
        {
            if (_mainLinkedAnimation == null) return;
            _mainLinkedAnimation.Play();
        }

        private void PlayIfNotPlaying()
        {
            if (_mainLinkedAnimation == null) return;
            _mainLinkedAnimation.PlayIfNotPlaying();
        }

        private void Stop()
        {
            if (_mainLinkedAnimation == null) return;
            _mainLinkedAnimation.Stop();
        }

        private void NextFrame()
        {
            if (_mainLinkedAnimation == null) return;
            _mainLinkedAnimation.NextFrame();
        }

        private void PreviousFrame()
        {
            if (_mainLinkedAnimation == null) return;
            _mainLinkedAnimation.PreviousFrame();
        }
    }
}
