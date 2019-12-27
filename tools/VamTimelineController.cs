using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AcidBubbles.VamTimeline.Tools
{
    /*
    Notes:
    - I want a way to control a series of animations, some of them loops, some of them transitions.
    - I want to control multiple atoms at the same time throughout those animations.
    - I want to control morphs using an animation pattern.
    Idea: Create a single timeline using an animation pattern, which controls everything, including switching animations.
          Add an option to loop or not
          Add an option to select which animation to select on _all_ targets
          Add an option to select which timeline to control (animation pattern OR one of the animations)
    */

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
            private static JSONStorable Storable;
            public JSONStorableFloat Scrubber;
            public JSONStorableStringChooser Animations;

            public static LinkedAnimation FromAtom(Atom atom)
            {
                var link = new LinkedAnimation();
                var storableId = atom.GetStorableIDs().FirstOrDefault(id => id.EndsWith("VamTimeline.MainScript"));
                Storable = atom.GetStorableByID(storableId);
                link.Scrubber = Storable.GetFloatJSONParam("Time");
                link.Animations = Storable.GetStringChooserJSONParam("Animation");
                link.Atom = atom;
                return link;
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
                if (Animations.choices.Contains(name))
                    Animations.val = name;
            }
        }

        private Atom _atom;
        private JSONStorableFloat _scrubberJSON;
        private JSONStorableStringChooser _animationJSON;
        private JSONStorableStringChooser _targetJSON;
        private LinkedAnimation _mainLinkedAnimation;
        private List<Transform> _canvasComponents;
        private readonly List<LinkedAnimation> _linkedAnimations = new List<LinkedAnimation>();

        public override void Init()
        {
            try
            {
                _atom = GetAtom();

                UIDynamicButton linkButton = null;

                _scrubberJSON = new JSONStorableFloat("Time", 0f, v => ChangeTime(v), 0f, 5f, true);
                RegisterFloat(_scrubberJSON);

                _animationJSON = new JSONStorableStringChooser("Animation", new List<string>(), "", "Animation", (string v) => ChangeAnimation(v));
                RegisterStringChooser(_animationJSON);

                _targetJSON = new JSONStorableStringChooser("Target", GetAtomsWithVamTimeline().ToList(), "", "Target", v => linkButton.button.interactable = !string.IsNullOrEmpty(v));
                var targetPopup = CreateScrollablePopup(_targetJSON, true);
                targetPopup.popupPanelHeight = 800f;
                targetPopup.popup.onOpenPopupHandlers += () => _targetJSON.choices = GetAtomsWithVamTimeline().ToList();

                linkButton = CreateButton("Link", true);
                linkButton.button.interactable = false;
                linkButton.button.onClick.AddListener(() => LinkAtom(_targetJSON.val));

                OnEnable();
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimelineController Init: " + exc);
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
                CreateUISliderInCanvas(_scrubberJSON, x, 0.30f);
                CreateUIPopupInCanvas(_animationJSON, x, 0.50f);
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

        private static IEnumerable<string> GetAtomsWithVamTimeline()
        {
            var atoms = SuperController.singleton.GetAtoms();
            foreach (var atom in atoms)
            {
                // TODO: Handle this, it will allow for morph animations too!
                if (atom.type == "AnimationPattern")
                    yield return atom.uid;
                if (atom.GetStorableIDs().Any(id => id.EndsWith("VamTimeline.MainScript")))
                    yield return atom.uid;
            }
        }

        private void LinkAtom(string uid)
        {
            try
            {
                // TODO: This is not saved anywhere
                var atom = SuperController.singleton.GetAtomByUid(uid);
                var link = LinkedAnimation.FromAtom(atom);
                _linkedAnimations.Add(link);
                // TODO: Instead add a drop down of which animation to control
                if (_mainLinkedAnimation == null)
                {
                    _mainLinkedAnimation = link;
                    VamTimelineAnimationUpdated(atom.uid);
                }
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimelineController LinkAtom: " + exc);
            }
        }

        public void VamTimelineAnimationUpdated(string uid)
        {
            if (_mainLinkedAnimation == null || _mainLinkedAnimation.Atom.uid == uid)
                return;

            _scrubberJSON.slider.interactable = true;
            _scrubberJSON.max = _mainLinkedAnimation.Scrubber.max;
            _scrubberJSON.valNoCallback = _mainLinkedAnimation.Scrubber.val;
            _animationJSON.choices = _mainLinkedAnimation.Animations.choices;
            _animationJSON.valNoCallback = _mainLinkedAnimation.Animations.val;
        }

        public void Update()
        {
            try
            {
                if (_mainLinkedAnimation == null) return;

                // TODO: Track "versions" on every animation, if one changes, refresh everything.

                if (_mainLinkedAnimation.Scrubber.val != _scrubberJSON.val)
                    _scrubberJSON.valNoCallback = _mainLinkedAnimation.Scrubber.val;
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimelineController Update: " + exc);
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
            foreach (var animation in _linkedAnimations)
            {
                animation.NextFrame();
            }
        }

        private void PreviousFrame()
        {
            foreach (var animation in _linkedAnimations)
            {
                animation.PreviousFrame();
            }
        }

        private void ChangeAnimation(string name)
        {
            foreach (var animation in _linkedAnimations)
            {
                animation.ChangeAnimation(name);
            }
        }
    }
}
