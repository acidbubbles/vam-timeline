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
    public class ControllerPlugin : MVRScript
    {
        private const string AllAtoms = "(All Atoms)";
        private const string AllControllers = "(All Controllers)";
        private Atom _atom;
        private SimpleSignUI _ui;
        private JSONStorableStringChooser _atomsJSON;
        private JSONStorableStringChooser _animationJSON;
        private JSONStorableFloat _scrubberJSON;
        private JSONStorableAction _playJSON;
        private JSONStorableAction _playIfNotPlayingJSON;
        private JSONStorableAction _stopJSON;
        private JSONStorableStringChooser _targetJSON;
        private LinkedAnimation _mainLinkedAnimation;
        private JSONStorableString _savedAtomsJSON;
        private JSONStorableStringChooser _controllerJSON;
        private JSONStorableString _displayJSON;
        private UIDynamicButton _linkButton;
        private readonly List<LinkedAnimation> _linkedAnimations = new List<LinkedAnimation>();

        #region Initialization

        public override void Init()
        {
            try
            {
                _atom = GetAtom();
                InitStorables();
                InitCustomUI();
                OnEnable();
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.ControllerPlugin.Init: " + exc);
            }
        }

        private Atom GetAtom()
        {
            // Note: Yeah, that's horrible, but containingAtom is null
            var container = gameObject?.transform?.parent?.parent?.parent?.parent?.parent?.gameObject;
            if (container == null)
                throw new NullReferenceException($"Could not find the parent gameObject");
            var atom = container.GetComponent<Atom>();
            if (atom == null)
                throw new NullReferenceException($"Could not find the parent atom in {container.name}");
            if (atom.type != "SimpleSign")
                throw new InvalidOperationException("Can only be applied on SimpleSign");
            return atom;
        }

        private void InitStorables()
        {
            _atomsJSON = new JSONStorableStringChooser("Atoms", new List<string>(), AllAtoms, "Atoms", (string v) => SelectCurrentAtom(v))
            {
                isStorable = false
            };
            RegisterStringChooser(_atomsJSON);

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

            _controllerJSON = new JSONStorableStringChooser("Selected Controller", new List<string>(), AllControllers, "Selected Controller", (string v) => SelectControllerFilter(v))
            {
                isStorable = false
            };
            RegisterStringChooser(_controllerJSON);

            _scrubberJSON = new JSONStorableFloat("Time", 0f, v => ChangeTime(v), 0f, 5f, true)
            {
                isStorable = false
            };
            RegisterFloat(_scrubberJSON);

            _displayJSON = new JSONStorableString("Display", "")
            {
                isStorable = false
            };
            RegisterString(_displayJSON);

            var atoms = GetAtomsWithVamTimeline().ToList();
            _targetJSON = new JSONStorableStringChooser("Target", atoms, atoms.FirstOrDefault() ?? "", "Add", v => _linkButton.button.interactable = !string.IsNullOrEmpty(v));

            _savedAtomsJSON = new JSONStorableString("Atoms", "", (string v) => StartCoroutine(RestoreAtomsLink(v)));
            RegisterString(_savedAtomsJSON);
        }

        private IEnumerable<string> GetAtomsWithVamTimeline()
        {
            var atoms = SuperController.singleton.GetAtoms();
            foreach (var atom in atoms)
            {
                // TODO: Handle this, it will allow for morph animations too!
                if (atom.type == "AnimationPattern")
                    yield return atom.uid;
                if (atom.GetStorableIDs().Any(id => id.EndsWith("VamTimeline.Atom")))
                {
                    if (_linkedAnimations.Any(la => la.Atom.uid == atom.uid)) continue;

                    yield return atom.uid;
                }
            }
        }

        private IEnumerator RestoreAtomsLink(string savedAtoms)
        {
            // This is an ugly way to wait for the target atom restore
            yield return new WaitForEndOfFrame();

            if (!string.IsNullOrEmpty(savedAtoms))
            {
                foreach (var atomUid in savedAtoms.Split(';'))
                {
                    LinkAtom(atomUid);
                }
            }
        }

        private void InitCustomUI()
        {
            var targetPopup = CreateScrollablePopup(_targetJSON);
            targetPopup.popupPanelHeight = 800f;
            targetPopup.popup.onOpenPopupHandlers += () => _targetJSON.choices = GetAtomsWithVamTimeline().ToList();

            _linkButton = CreateButton("Link");
            _linkButton.button.interactable = _atomsJSON.choices.Count > 0;
            _linkButton.button.onClick.AddListener(() => LinkAtom(_targetJSON.val));
        }

        #endregion

        #region Lifecycle

        public void OnEnable()
        {
            if (_atom == null) return;

            try
            {
                var x = 0f;
                var y = -0.39f;
                // const float baseWidth = 1160f;
                _ui = new SimpleSignUI(_atom, this);
                _ui.CreateUIPopupInCanvas(_animationJSON, x, y + 0.355f);
                _ui.CreateUISliderInCanvas(_scrubberJSON, x, y + 0.14f);
                _ui.CreateUIButtonInCanvas("\u25B6 Play", x - 0.105f, y + 0.60f, 810f, 100f).button.onClick.AddListener(() => Play());
                _ui.CreateUIButtonInCanvas("\u25A0 Stop", x + 0.257f, y + 0.60f, 300f, 100f).button.onClick.AddListener(() => Stop());
                _ui.CreateUIPopupInCanvas(_atomsJSON, x, y + 0.585f);
                _ui.CreateUIPopupInCanvas(_controllerJSON, x, y + 0.655f);
                _ui.CreateUIButtonInCanvas("\u2190 Previous Frame", x - 0.182f, y + 0.82f, 550f, 100f).button.onClick.AddListener(() => PreviousFrame());
                _ui.CreateUIButtonInCanvas("Next Frame \u2192", x + 0.182f, y + 0.82f, 550f, 100f).button.onClick.AddListener(() => NextFrame());
                _ui.CreateUITextfieldInCanvas(_displayJSON, x, y + 0.62f);
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.ControllerPlugin.OnEnable: " + exc);
            }
        }

        public void OnDisable()
        {
            if (_ui == null) return;

            try
            {
                _scrubberJSON.slider = null;
                _ui.Dispose();
                _ui = null;
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.ControllerPlugin.OnDisable: " + exc);
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
                foreach (var link in _linkedAnimations)
                    link.Scrubber.val = v;
                VamTimelineContextChanged(_mainLinkedAnimation.Atom.uid);
            }
        }


        private void LinkAtom(string uid)
        {
            try
            {
                if (_linkedAnimations.Any(la => la.Atom.uid == uid)) return;

                var atom = SuperController.singleton.GetAtomByUid(uid);
                if (atom == null) return;
                var link = new LinkedAnimation(atom);
                _linkedAnimations.Add(link);
                _atomsJSON.choices = _linkedAnimations.Select(la => la.Atom.uid).ToList();
                if (_mainLinkedAnimation == null)
                    SelectCurrentAtom(atom.uid);
                // TODO: If an atom contains ';' it won't work
                _savedAtomsJSON.val = string.Join(";", _atomsJSON.choices.ToArray());
                _targetJSON.choices = GetAtomsWithVamTimeline().ToList();
                _targetJSON.val = _targetJSON.choices.FirstOrDefault() ?? "";
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.ControllerPlugin.LinkAtom: " + exc);
            }
        }

        public void VamTimelineAnimationUpdated(string uid)
        {
            VamTimelineContextChanged(uid);

            if (_mainLinkedAnimation == null || _mainLinkedAnimation.Atom.uid != uid)
                return;

            _scrubberJSON.slider.interactable = true;
            _scrubberJSON.valNoCallback = _mainLinkedAnimation.Scrubber.val;
            _animationJSON.choices = _mainLinkedAnimation.Animation.choices;
            _controllerJSON.choices = _mainLinkedAnimation.SelectedController.choices;
        }

        public void VamTimelineContextChanged(string uid)
        {
            if (_linkedAnimations.Count == 0) return;

            var firstAnimationName = _linkedAnimations[0].Animation.val;
            if (_linkedAnimations.Skip(1).All(la => la.Animation.val == firstAnimationName))
                _animationJSON.valNoCallback = firstAnimationName;
            else
                _animationJSON.valNoCallback = "(Multiple animations selected)";

            if (_mainLinkedAnimation == null || _mainLinkedAnimation.Atom.uid != uid)
                return;

            _scrubberJSON.max = _mainLinkedAnimation.Scrubber.max;
            _controllerJSON.valNoCallback = _mainLinkedAnimation.SelectedController.val;
            _displayJSON.valNoCallback = _mainLinkedAnimation.Display.val;
        }

        public void Update()
        {
            try
            {
                if (_mainLinkedAnimation == null) return;

                var scrubber = _mainLinkedAnimation.Scrubber;
                if (scrubber.val != _scrubberJSON.val)
                    _scrubberJSON.valNoCallback = scrubber.val;
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.ControllerPlugin.Update: " + exc);
            }
        }

        private void SelectCurrentAtom(string uid)
        {
            if (string.IsNullOrEmpty(uid) || uid == AllAtoms)
            {
                _mainLinkedAnimation = null;
                return;
            }
            _mainLinkedAnimation = _linkedAnimations.FirstOrDefault(la => la.Atom.uid == uid);
            if (_mainLinkedAnimation == null) return;
            _atomsJSON.valNoCallback = _mainLinkedAnimation.Atom.uid;
            VamTimelineAnimationUpdated(_mainLinkedAnimation.Atom.uid);
            VamTimelineContextChanged(_mainLinkedAnimation.Atom.uid);
        }

        private void ChangeAnimation(string name)
        {
            foreach (var animation in _linkedAnimations)
            {
                animation.ChangeAnimation(name);
            }
        }

        private void Play()
        {
            foreach (var animation in _linkedAnimations)
            {
                animation.Play();
            }
        }

        private void PlayIfNotPlaying()
        {
            foreach (var animation in _linkedAnimations)
            {
                animation.PlayIfNotPlaying();
            }
        }

        private void Stop()
        {
            foreach (var animation in _linkedAnimations)
            {
                animation.Stop();
            }
        }

        private void NextFrame()
        {
            if (_mainLinkedAnimation == null) return;
            _mainLinkedAnimation.NextFrame();
            var time = _mainLinkedAnimation.Scrubber.val;
            foreach (var animation in _linkedAnimations.Where(la => la != _mainLinkedAnimation))
            {
                animation.Scrubber.val = time;
            }
        }

        private void PreviousFrame()
        {
            if (_mainLinkedAnimation == null) return;
            _mainLinkedAnimation.PreviousFrame();
            var time = _mainLinkedAnimation.Scrubber.val;
            foreach (var animation in _linkedAnimations.Where(la => la != _mainLinkedAnimation))
            {
                animation.Scrubber.val = time;
            }
        }

        private void SelectControllerFilter(string v)
        {
            if (_mainLinkedAnimation == null) return;
            if (string.IsNullOrEmpty(v) || v == AllControllers)
            {
                _mainLinkedAnimation.SelectedController.val = null;
                return;
            }
            _mainLinkedAnimation.SelectedController.val = v;
        }
    }
}
