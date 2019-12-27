using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

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
            public JSONStorableFloat Scrubber;
        }

        private JSONStorableFloat _scrubberJSON;
        private JSONStorableStringChooser _targetJSON;
        private LinkedAnimation _mainLinkedAnimation;
        private Atom _atom;
        private Transform _scrubberTransform;
        private UIDynamicSlider _scrubberSlider;
        private readonly List<LinkedAnimation> _linkedAnimations = new List<LinkedAnimation>();

        public override void Init()
        {
            try
            {
                _atom = GetAtom();

                UIDynamicButton linkButton = null;

                _scrubberJSON = new JSONStorableFloat("Time", 0f, v => ChangeTime(v), 0f, 5f, true);
                RegisterFloat(_scrubberJSON);

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
                CreateUISliderInCanvas();
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimelineController Enable: " + exc);
            }
        }

        private void CreateUISliderInCanvas()
        {
            var canvas = _atom.GetComponentInChildren<Canvas>();
            if (canvas == null) throw new NullReferenceException("Could not find a canvas to attach to");

            _scrubberTransform = Instantiate(manager.configurableSliderPrefab.transform);
            if (_scrubberTransform == null) throw new NullReferenceException("Could not instantiate configurableSliderPrefab");
            _scrubberTransform.SetParent(canvas.transform, false);
            _scrubberTransform.gameObject.SetActive(true);

            _scrubberSlider = _scrubberTransform.GetComponent<UIDynamicSlider>();
            if (_scrubberSlider == null) throw new NullReferenceException("Could not find a UIDynamicSlider component");
            _scrubberSlider.Configure("Time", 0, 5f, 0f, true, "F2", true, false);
            _scrubberJSON.slider = _scrubberSlider.slider;
            _scrubberSlider.slider.interactable = true;

            _scrubberTransform.Translate(Vector3.down * 0.3f, Space.Self);
            _scrubberTransform.Translate(Vector3.right * 0.35f, Space.Self);
        }

        public void OnDisable()
        {
            if (_scrubberTransform == null) return;

            try
            {
                _scrubberJSON.slider = null;
                Destroy(_scrubberTransform.gameObject);
                _scrubberTransform = null;
                _scrubberSlider = null;
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
            // TODO: This is not saved anywhere
            var atom = SuperController.singleton.GetAtomByUid(uid);
            var link = new LinkedAnimation { Atom = atom };
            if (atom.type == "AnimationPattern")
            {
                // TODO
            }
            else
            {
                var storableId = atom.GetStorableIDs().FirstOrDefault(id => id.EndsWith("VamTimeline.MainScript"));
                var storable = atom.GetStorableByID(storableId);
                link.Scrubber = storable.GetFloatJSONParam("Time");
                _linkedAnimations.Add(link);
                if (_mainLinkedAnimation == null)
                {
                    _mainLinkedAnimation = link;
                    // TODO: Needs a refresh setting
                    _scrubberJSON.max = _mainLinkedAnimation.Scrubber.max;
                    _scrubberJSON.valNoCallback = _mainLinkedAnimation.Scrubber.val;
                }
                _scrubberSlider.slider.interactable = true;
            }
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
    }
}
