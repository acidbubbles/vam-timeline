using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AcidBubbles.VamTimeline.Tools
{
    /// <summary>
    /// VaM Timeline Controller
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class VamTimelineController : MVRScript
    {
        private class LinkedAnimation
        {
            public Atom Atom;

            public LinkedAnimation(Atom atom)
            {
                Atom = atom;
            }

            private JSONStorable Storable
            {
                get
                {
                    var storableId = Atom.GetStorableIDs().FirstOrDefault(id => id.EndsWith("VamTimeline.MainScript"));
                    return Atom.GetStorableByID(storableId);
                }
            }
            public JSONStorableFloat Scrubber
            {
                get
                {
                    return Storable.GetFloatJSONParam("Time");
                }
            }
            public JSONStorableStringChooser Animation
            {
                get
                {
                    return Storable.GetStringChooserJSONParam("Animation");
                }
            }

            public void Play()
            {
                Storable.CallAction("Play");
            }

            public void Stop()
            {
                Storable.CallAction("Stop");
            }

            public void NextFrame()
            {
                Storable.CallAction("Next Frame");
            }

            public void PreviousFrame()
            {
                Storable.CallAction("Previous Frame");
            }

            public void ChangeAnimation(string name)
            {
                if (Animation.choices.Contains(name))
                    Animation.val = name;
            }
        }

        private Atom _atom;
        private JSONStorableStringChooser _atomsJSON;
        private JSONStorableStringChooser _animationJSON;
        private JSONStorableFloat _scrubberJSON;
        private JSONStorableStringChooser _targetJSON;
        private LinkedAnimation _mainLinkedAnimation;
        private List<Transform> _canvasComponents;
        private JSONStorableString _savedAtomsJSON;
        private readonly List<LinkedAnimation> _linkedAnimations = new List<LinkedAnimation>();

        public override void Init()
        {
            try
            {
                _atom = GetAtom();

                UIDynamicButton linkButton = null;

                _atomsJSON = new JSONStorableStringChooser("Atoms", new List<string>(), "", "Atoms", (string v) => SelectCurrentAtom(v));
                _atomsJSON.isStorable = false;
                RegisterStringChooser(_atomsJSON);

                _animationJSON = new JSONStorableStringChooser("Animation", new List<string>(), "", "Animation", (string v) => ChangeAnimation(v));
                _animationJSON.isStorable = false;
                RegisterStringChooser(_animationJSON);

                _scrubberJSON = new JSONStorableFloat("Time", 0f, v => ChangeTime(v), 0f, 5f, true);
                _scrubberJSON.isStorable = false;
                RegisterFloat(_scrubberJSON);

                _targetJSON = new JSONStorableStringChooser("Target", GetAtomsWithVamTimeline().ToList(), "", "Add", v => linkButton.button.interactable = !string.IsNullOrEmpty(v));
                var targetPopup = CreateScrollablePopup(_targetJSON);
                targetPopup.popupPanelHeight = 800f;
                targetPopup.popup.onOpenPopupHandlers += () => _targetJSON.choices = GetAtomsWithVamTimeline().ToList();

                linkButton = CreateButton("Link");
                linkButton.button.interactable = false;
                linkButton.button.onClick.AddListener(() => LinkAtom(_targetJSON.val));

                _savedAtomsJSON = new JSONStorableString("Atoms", "", (string v) => StartCoroutine(Restore(v)));
                RegisterString(_savedAtomsJSON);

                OnEnable();
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimelineController Init: " + exc);
            }
        }

        private IEnumerator Restore(string v)
        {
            // TODO: This is an ugly way to wait for the target atom restore
            yield return new WaitForEndOfFrame();

            if (!string.IsNullOrEmpty(v))
            {
                foreach (var atomUid in v.Split(';'))
                {
                    LinkAtom(atomUid);
                }
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

        public void OnEnable()
        {
            if (_atom == null) return;

            try
            {
                var x = 0.40f;
                _canvasComponents = new List<Transform>();
                CreateUIPopupInCanvas(_atomsJSON, x, 0.40f);
                CreateUIPopupInCanvas(_animationJSON, x, 0.50f);
                CreateUISliderInCanvas(_scrubberJSON, x, 0.30f);
                CreateUIButtonInCanvas("Play", x, 0.75f).button.onClick.AddListener(() => Play());
                CreateUIButtonInCanvas("Stop", x, 0.80f).button.onClick.AddListener(() => Stop());
                CreateUIButtonInCanvas("Next Frame", x, 0.85f).button.onClick.AddListener(() => NextFrame());
                CreateUIButtonInCanvas("Previous Frame", x, 0.90f).button.onClick.AddListener(() => PreviousFrame());
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimelineController Enable: " + exc);
            }
        }

        private UIDynamicSlider CreateUISliderInCanvas(JSONStorableFloat jsf, float x, float y)
        {
            var canvas = _atom.GetComponentInChildren<Canvas>();
            if (canvas == null) throw new NullReferenceException("Could not find a canvas to attach to");

            var transform = Instantiate(manager.configurableSliderPrefab.transform);
            if (transform == null) throw new NullReferenceException("Could not instantiate configurableSliderPrefab");
            _canvasComponents.Add(transform);
            transform.SetParent(canvas.transform, false);
            transform.gameObject.SetActive(true);

            var ui = transform.GetComponent<UIDynamicSlider>();
            if (ui == null) throw new NullReferenceException("Could not find a UIDynamicSlider component");
            ui.Configure(jsf.name, jsf.min, jsf.max, jsf.defaultVal, jsf.constrained, "F2", true, !jsf.constrained);
            jsf.slider = ui.slider;
            ui.slider.interactable = true;

            transform.Translate(Vector3.down * y, Space.Self);
            transform.Translate(Vector3.right * x, Space.Self);

            return ui;
        }

        private UIDynamicPopup CreateUIPopupInCanvas(JSONStorableStringChooser jssc, float x, float y)
        {
            var canvas = _atom.GetComponentInChildren<Canvas>();
            if (canvas == null) throw new NullReferenceException("Could not find a canvas to attach to");

            var transform = Instantiate(manager.configurableScrollablePopupPrefab.transform);
            if (transform == null) throw new NullReferenceException("Could not instantiate configurableScrollablePopupPrefab");
            _canvasComponents.Add(transform);
            transform.SetParent(canvas.transform, false);
            transform.gameObject.SetActive(true);

            var ui = transform.GetComponent<UIDynamicPopup>();
            if (ui == null) throw new NullReferenceException("Could not find a UIDynamicPopup component");
            ui.popupPanelHeight = 1000;
            jssc.popup = ui.popup;
            ui.label = jssc.label;

            transform.Translate(Vector3.down * y, Space.Self);
            transform.Translate(Vector3.right * x, Space.Self);

            return ui;
        }

        private UIDynamicButton CreateUIButtonInCanvas(string label, float x, float y)
        {
            var canvas = _atom.GetComponentInChildren<Canvas>();
            if (canvas == null) throw new NullReferenceException("Could not find a canvas to attach to");

            var transform = Instantiate(manager.configurableButtonPrefab.transform);
            if (transform == null) throw new NullReferenceException("Could not instantiate configurableButtonPrefab");
            _canvasComponents.Add(transform);
            transform.SetParent(canvas.transform, false);
            transform.gameObject.SetActive(true);

            var ui = transform.GetComponent<UIDynamicButton>();
            ui.label = label;
            if (ui == null) throw new NullReferenceException("Could not find a UIDynamicButton component");

            transform.Translate(Vector3.down * y, Space.Self);
            transform.Translate(Vector3.right * x, Space.Self);

            return ui;
        }

        public void OnDisable()
        {
            if (_canvasComponents == null) return;

            try
            {
                _scrubberJSON.slider = null;
                foreach (var component in _canvasComponents)
                {
                    Destroy(component.gameObject);
                }
                _canvasComponents = null;
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimelineController Disable: " + exc);
            }
        }

        public void OnDestroy()
        {
            OnDisable();
        }

        private void ChangeTime(float v)
        {
            {
                if (_mainLinkedAnimation == null) return;
                foreach (var link in _linkedAnimations)
                    link.Scrubber.val = v;
            }
        }

        private IEnumerable<string> GetAtomsWithVamTimeline()
        {
            var atoms = SuperController.singleton.GetAtoms();
            foreach (var atom in atoms)
            {
                // TODO: Handle this, it will allow for morph animations too!
                if (atom.type == "AnimationPattern")
                    yield return atom.uid;
                if (atom.GetStorableIDs().Any(id => id.EndsWith("VamTimeline.MainScript")))
                {
                    if (_linkedAnimations.Any(la => la.Atom.uid == atom.uid)) continue;

                    yield return atom.uid;
                }
            }
        }

        private void LinkAtom(string uid)
        {
            try
            {
                if (_linkedAnimations.Any(la => la.Atom.uid == uid)) return;

                // TODO: This is not saved anywhere
                var atom = SuperController.singleton.GetAtomByUid(uid);
                if (atom == null) return;
                var link = new LinkedAnimation(atom);
                _linkedAnimations.Add(link);
                _atomsJSON.choices = _linkedAnimations.Select(la => la.Atom.uid).ToList();
                if (_mainLinkedAnimation == null)
                    SelectCurrentAtom(atom.uid);
                // TODO: If an atom contains ';' it won't work
                _savedAtomsJSON.val = string.Join(";", _atomsJSON.choices.ToArray());
                _targetJSON.val = "";
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimelineController LinkAtom: " + exc);
            }
        }

        public void VamTimelineAnimationUpdated(string uid)
        {
            if (_mainLinkedAnimation == null || _mainLinkedAnimation.Atom.uid != uid)
                return;

            _scrubberJSON.slider.interactable = true;
            _scrubberJSON.max = _mainLinkedAnimation.Scrubber.max;
            _scrubberJSON.valNoCallback = _mainLinkedAnimation.Scrubber.val;
            _animationJSON.choices = _mainLinkedAnimation.Animation.choices;
            _animationJSON.valNoCallback = _mainLinkedAnimation.Animation.val;
        }

        public void VamTimelineContextChanged(string uid)
        {
            if (_mainLinkedAnimation == null || _mainLinkedAnimation.Atom.uid != uid)
                return;

            _animationJSON.valNoCallback = _mainLinkedAnimation.Animation.val;
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
                SuperController.LogError("VamTimelineController Update: " + exc);
            }
        }

        private void SelectCurrentAtom(string uid)
        {
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

        private void Stop()
        {
            foreach (var animation in _linkedAnimations)
            {
                animation.Stop();
            }
        }

        private void NextFrame()
        {
            _mainLinkedAnimation.NextFrame();
            var time = _mainLinkedAnimation.Scrubber.val;
            foreach (var animation in _linkedAnimations.Where(la => la != _mainLinkedAnimation))
            {
                animation.Scrubber.val = time;
            }
        }

        private void PreviousFrame()
        {
            _mainLinkedAnimation.PreviousFrame();
            var time = _mainLinkedAnimation.Scrubber.val;
            foreach (var animation in _linkedAnimations.Where(la => la != _mainLinkedAnimation))
            {
                animation.Scrubber.val = time;
            }
        }
    }
}
