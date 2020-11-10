using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace VamTimeline
{
    public class SimpleSignUI : IDisposable
    {
        private readonly MVRScript _owner;
        private readonly GameObject _child;
        private readonly VerticalLayoutGroup _container;

        public SimpleSignUI(Atom atom, MVRScript owner)
        {
            _owner = owner;

            var canvas = atom.GetComponentsInChildren<Canvas>().FirstOrDefault(c => c.gameObject.name == "Sign");
            if (canvas == null) throw new NullReferenceException($"Expected Sign Canvas, but found: {string.Join(",", atom.GetComponentsInChildren<Canvas>().Select(c => c.gameObject.name).ToArray())}");

            _child = new GameObject("Simple Sign UI");
            _child.transform.SetParent(canvas.transform, false);

            var rect = _child.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(-30f, 50f);
            rect.offsetMax = new Vector2(30f, 210f);

            var bg = _child.AddComponent<GradientImage>();
            var color = new Color(0.30f, 0.36f, 0.52f);
            bg.top = Color.Lerp(color, Color.black, 0.18f);
            bg.bottom = Color.Lerp(color, Color.white, 0.03f);

            _container = _child.AddComponent<VerticalLayoutGroup>();
            _container.spacing = 20f;
            _container.padding = new RectOffset(20, 20, 20, 20);
            _container.childControlHeight = true;
            _container.childControlWidth = true;
            _container.childForceExpandHeight = false;
            _container.childForceExpandWidth = true;
        }

        public UIDynamicSlider CreateUISliderInCanvas(JSONStorableFloat jsf)
        {
            var transform = Object.Instantiate(_owner.manager.configurableSliderPrefab.transform);
            if (transform == null) throw new NullReferenceException("Could not instantiate configurableSliderPrefab");
            transform.SetParent(_container.transform, false);
            transform.gameObject.SetActive(true);

            var ui = transform.GetComponent<UIDynamicSlider>();
            if (ui == null) throw new NullReferenceException("Could not find a UIDynamicSlider component");
            ui.Configure(jsf.name, jsf.min, jsf.max, jsf.defaultVal, jsf.constrained, "F3", true, !jsf.constrained);
            jsf.slider = ui.slider;
            ui.slider.interactable = true;

            return ui;
        }

        public UIDynamicPopup CreateUIPopupInCanvas(JSONStorableStringChooser jssc)
        {
            var transform = Object.Instantiate(_owner.manager.configurableScrollablePopupPrefab.transform);
            if (transform == null) throw new NullReferenceException("Could not instantiate configurableScrollablePopupPrefab");
            transform.SetParent(_container.transform, false);
            transform.gameObject.SetActive(true);

            var ui = transform.GetComponent<UIDynamicPopup>();
            if (ui == null) throw new NullReferenceException("Could not find a UIDynamicPopup component");
            ui.popupPanelHeight = 1000;
            jssc.popup = ui.popup;
            ui.label = jssc.label;

            return ui;
        }

        public UIDynamicButton CreateUIButtonInCanvas(string label)
        {
            var transform = Object.Instantiate(_owner.manager.configurableButtonPrefab.transform);
            if (transform == null) throw new NullReferenceException("Could not instantiate configurableButtonPrefab");
            transform.SetParent(_container.transform, false);
            transform.gameObject.SetActive(true);

            var ui = transform.GetComponent<UIDynamicButton>();
            if (ui == null) throw new NullReferenceException("Could not find a UIDynamicButton component");
            ui.label = label;

            return ui;
        }

        public UIDynamicTextField CreateUITextfieldInCanvas(JSONStorableString jss)
        {
            var transform = Object.Instantiate(_owner.manager.configurableTextFieldPrefab.transform);
            if (transform == null) throw new NullReferenceException("Could not instantiate configurableButtonPrefab");
            transform.SetParent(_container.transform, false);
            transform.gameObject.SetActive(true);

            var ui = transform.GetComponent<UIDynamicTextField>();
            if (ui == null) throw new NullReferenceException("Could not find a UIDynamicTextField component");
            jss.dynamicText = ui;

            return ui;
        }

        public UIDynamicToggle CreateUIToggleInCanvas(JSONStorableBool jsb)
        {
            var transform = Object.Instantiate(_owner.manager.configurableTogglePrefab.transform);
            if (transform == null) throw new NullReferenceException("Could not instantiate configurableTogglePrefab");
            transform.SetParent(_container.transform, false);
            transform.gameObject.SetActive(true);

            var ui = transform.GetComponent<UIDynamicToggle>();
            if (ui == null) throw new NullReferenceException("Could not find a UIDynamicToggle component");
            ui.label = jsb.name;
            jsb.toggle = ui.toggle;

            return ui;
        }

        public UIDynamic CreateUISpacerInCanvas(float height)
        {
            var transform = Object.Instantiate(_owner.manager.configurableSpacerPrefab.transform);
            if (transform == null) throw new NullReferenceException("Could not instantiate configurableSpacerPrefab");
            transform.SetParent(_container.transform, false);
            transform.gameObject.SetActive(true);

            var ui = transform.GetComponent<UIDynamic>();
            if (ui == null) throw new NullReferenceException("Could not find a UIDynamic component");
            ui.height = height;

            return ui;
        }

        public void Dispose()
        {
            Object.Destroy(_child);
        }
    }
}
