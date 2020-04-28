using System;
using System.Collections.Generic;
using UnityEngine;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class SimpleSignUI : IDisposable
    {
        private readonly Atom _atom;
        private readonly MVRScript _owner;
        private readonly List<Transform> _components = new List<Transform>();

        public SimpleSignUI(Atom atom, MVRScript owner)
        {
            _atom = atom;
            _owner = owner;
        }

        public UIDynamicSlider CreateUISliderInCanvas(JSONStorableFloat jsf, float x, float y)
        {
            var canvas = _atom.GetComponentInChildren<Canvas>();
            if (canvas == null) throw new NullReferenceException("Could not find a canvas to attach to");

            var transform = UnityEngine.Object.Instantiate(_owner.manager.configurableSliderPrefab.transform);
            if (transform == null) throw new NullReferenceException("Could not instantiate configurableSliderPrefab");
            _components.Add(transform);
            transform.SetParent(canvas.transform, false);
            transform.gameObject.SetActive(true);

            var ui = transform.GetComponent<UIDynamicSlider>();
            if (ui == null) throw new NullReferenceException("Could not find a UIDynamicSlider component");
            ui.Configure(jsf.name, jsf.min, jsf.max, jsf.defaultVal, jsf.constrained, "F3", true, !jsf.constrained);
            jsf.slider = ui.slider;
            ui.slider.interactable = true;

            transform.Translate(Vector3.down * y, Space.Self);
            transform.Translate(Vector3.right * x, Space.Self);

            return ui;
        }

        public UIDynamicPopup CreateUIPopupInCanvas(JSONStorableStringChooser jssc, float x, float y)
        {
            var canvas = _atom.GetComponentInChildren<Canvas>();
            if (canvas == null) throw new NullReferenceException("Could not find a canvas to attach to");

            var transform = UnityEngine.Object.Instantiate(_owner.manager.configurableScrollablePopupPrefab.transform);
            if (transform == null) throw new NullReferenceException("Could not instantiate configurableScrollablePopupPrefab");
            _components.Add(transform);
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

        public UIDynamicButton CreateUIButtonInCanvas(string label, float x, float y, float w = 0, float h = 0)
        {
            var canvas = _atom.GetComponentInChildren<Canvas>();
            if (canvas == null) throw new NullReferenceException("Could not find a canvas to attach to");

            var transform = UnityEngine.Object.Instantiate(_owner.manager.configurableButtonPrefab.transform);
            if (transform == null) throw new NullReferenceException("Could not instantiate configurableButtonPrefab");
            _components.Add(transform);
            transform.SetParent(canvas.transform, false);
            transform.gameObject.SetActive(true);

            var ui = transform.GetComponent<UIDynamicButton>();
            if (ui == null) throw new NullReferenceException("Could not find a UIDynamicButton component");
            ui.label = label;
            if (w > 0)
                ui.button.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, w);
            if (h > 0)
                ui.button.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);

            transform.Translate(Vector3.down * y, Space.Self);
            transform.Translate(Vector3.right * x, Space.Self);

            return ui;
        }

        public UIDynamicTextField CreateUITextfieldInCanvas(JSONStorableString jss, float x, float y)
        {
            var canvas = _atom.GetComponentInChildren<Canvas>();
            if (canvas == null) throw new NullReferenceException("Could not find a canvas to attach to");

            var transform = UnityEngine.Object.Instantiate(_owner.manager.configurableTextFieldPrefab.transform);
            if (transform == null) throw new NullReferenceException("Could not instantiate configurableButtonPrefab");
            _components.Add(transform);
            transform.SetParent(canvas.transform, false);
            transform.gameObject.SetActive(true);

            var ui = transform.GetComponent<UIDynamicTextField>();
            if (ui == null) throw new NullReferenceException("Could not find a UIDynamicButton component");
            jss.dynamicText = ui;

            transform.Translate(Vector3.down * y, Space.Self);
            transform.Translate(Vector3.right * x, Space.Self);

            return ui;
        }

        public UIDynamicToggle CreateUIToggleInCanvas(JSONStorableBool jsb, float x, float y)
        {
            var canvas = _atom.GetComponentInChildren<Canvas>();
            if (canvas == null) throw new NullReferenceException("Could not find a canvas to attach to");

            var transform = UnityEngine.Object.Instantiate(_owner.manager.configurableTogglePrefab.transform);
            if (transform == null) throw new NullReferenceException("Could not instantiate configurableTogglePrefab");
            _components.Add(transform);
            transform.SetParent(canvas.transform, false);
            transform.gameObject.SetActive(true);

            var ui = transform.GetComponent<UIDynamicToggle>();
            if (ui == null) throw new NullReferenceException("Could not find a UIDynamicPopup component");
            ui.label = jsb.name;
            jsb.toggle = ui.toggle;

            transform.Translate(Vector3.down * y, Space.Self);
            transform.Translate(Vector3.right * x, Space.Self);

            return ui;
        }

        public void Dispose()
        {
            foreach (var component in _components)
            {
                UnityEngine.Object.Destroy(component.gameObject);
            }
            _components.Clear();
        }
    }
}
