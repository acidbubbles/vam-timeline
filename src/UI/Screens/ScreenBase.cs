using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public abstract class ScreenBase : MonoBehaviour
    {
        public class ScreenChangeRequestedEvent : UnityEvent<string> { }

        private static readonly Font _font = (Font)Resources.GetBuiltinResource(typeof(Font), "Arial.ttf");

        public ScreenChangeRequestedEvent onScreenChangeRequested = new ScreenChangeRequestedEvent();
        public abstract string screenId { get; }

        protected AtomAnimation animation => plugin.animation;

        private readonly List<UIDynamic> _components = new List<UIDynamic>();
        private readonly List<JSONStorableParam> _storables = new List<JSONStorableParam>();
        protected IAtomPlugin plugin;
        protected AtomAnimationClip current;
        protected bool _disposing;

        protected ScreenBase()
        {
        }

        public virtual void Init(IAtomPlugin plugin)
        {
            this.plugin = plugin;
            plugin.animation.onCurrentAnimationChanged.AddListener(OnCurrentAnimationChanged);
            current = plugin.animation?.current;
        }

        protected virtual void OnCurrentAnimationChanged(AtomAnimation.CurrentAnimationChangedEventArgs args)
        {
            current = plugin.animation?.current;
        }

        protected void InitClipboardUI(bool rightSide)
        {
            var container = CreateSpacer(rightSide);
            RegisterComponent(container);
            container.height = 60f;

            var group = container.gameObject.AddComponent<GridLayoutGroup>();
            group.constraint = GridLayoutGroup.Constraint.Flexible;
            group.constraintCount = 3;
            group.spacing = Vector2.zero;
            group.cellSize = new Vector2(512f / 3f, 50f);
            group.childAlignment = TextAnchor.MiddleCenter;

            {
                var btn = Instantiate(plugin.manager.configurableButtonPrefab).GetComponent<UIDynamicButton>();
                btn.gameObject.transform.SetParent(group.transform, false);
                btn.label = "Cut";
                btn.button.onClick.AddListener(() => plugin.cutJSON.actionCallback());
            }

            {
                var btn = Instantiate(plugin.manager.configurableButtonPrefab).GetComponent<UIDynamicButton>();
                btn.gameObject.transform.SetParent(group.transform, false);
                btn.label = "Copy";
                btn.button.onClick.AddListener(() => plugin.copyJSON.actionCallback());
            }

            {
                var btn = Instantiate(plugin.manager.configurableButtonPrefab).GetComponent<UIDynamicButton>();
                btn.gameObject.transform.SetParent(group.transform, false);
                btn.label = "Paste";
                btn.button.onClick.AddListener(() => plugin.pasteJSON.actionCallback());
            }
        }

        protected Text CreateHeader(string val)
        {
            var layerUI = CreateSpacer(true);
            RegisterComponent(layerUI);
            layerUI.height = 40f;

            var text = layerUI.gameObject.AddComponent<Text>();
            text.text = val;
            text.font = _font;
            text.fontSize = 28;
            text.color = Color.black;

            return text;
        }

        protected UIDynamicButton CreateChangeScreenButton(string label, string screenName, bool rightSide)
        {
            var ui = CreateButton(label, rightSide);
            RegisterComponent(ui);
            ui.button.onClick.AddListener(() => onScreenChangeRequested.Invoke(screenName));
            return ui;
        }

        protected T RegisterStorable<T>(T v)
            where T : JSONStorableParam
        {
            _storables.Add(v);
            ValidateStorableFreeToBind(v);
            return v;
        }

        private void ValidateStorableFreeToBind(JSONStorableParam v)
        {
            if (v is JSONStorableStringChooser)
            {
                if (((JSONStorableStringChooser)v).popup != null)
                    SuperController.LogError($"Storable {v.name} of atom {plugin.containingAtom.name} was not correctly unregistered.");
            }
            else if (v is JSONStorableFloat)
            {
                if (((JSONStorableFloat)v).slider != null)
                    SuperController.LogError($"Storable {v.name} of atom {plugin.containingAtom.name} was not correctly unregistered.");
            }
            else if (v is JSONStorableString)
            {
                if (((JSONStorableString)v).inputField != null)
                    SuperController.LogError($"Storable {v.name} of atom {plugin.containingAtom.name} was not correctly unregistered.");
            }
            else if (v is JSONStorableBool)
            {
                if (((JSONStorableBool)v).toggle != null)
                    SuperController.LogError($"Storable {v.name} of atom {plugin.containingAtom.name} was not correctly unregistered.");
            }
        }

        protected T RegisterComponent<T>(T v)
            where T : UIDynamic
        {
            _components.Add(v);
            return v;
        }

        public virtual void OnDestroy()
        {
            onScreenChangeRequested.RemoveAllListeners();
            plugin.animation.onCurrentAnimationChanged.RemoveListener(OnCurrentAnimationChanged);

            _disposing = true;
            foreach (var component in _storables)
            {
                if (component == null) continue;

                if (component is JSONStorableStringChooser)
                    RemovePopup((JSONStorableStringChooser)component);
                else if (component is JSONStorableFloat)
                    RemoveSlider((JSONStorableFloat)component);
                else if (component is JSONStorableString)
                    RemoveTextField((JSONStorableString)component);
                else if (component is JSONStorableBool)
                    RemoveToggle((JSONStorableBool)component);
                else
                    SuperController.LogError($"VamTimeline: Cannot remove component {component}");
            }
            _storables.Clear();

            foreach (var component in _components)
            {
                if (component is UIDynamicButton)
                    RemoveButton((UIDynamicButton)component);
                else if (component is UIDynamicPopup)
                    RemovePopup((UIDynamicPopup)component);
                else if (component is UIDynamicSlider)
                    RemoveSlider((UIDynamicSlider)component);
                else if (component is UIDynamicTextField)
                    RemoveTextField((UIDynamicTextField)component);
                else if (component is UIDynamicToggle)
                    RemoveToggle((UIDynamicToggle)component);
                else
                    RemoveSpacer(component);
            }
            _components.Clear();
        }

        protected UIDynamic CreateSpacer(bool rightSide = false)
        {
            var ui = Instantiate(plugin.manager.configurableSpacerPrefab).GetComponent<UIDynamic>();
            ui.gameObject.transform.SetParent(transform, false);
            ui.height = 30f;
            return ui;
        }
        protected UIDynamicSlider CreateSlider(JSONStorableFloat jsf, bool rightSide = false)
        {

            var ui = Instantiate(plugin.manager.configurableSliderPrefab).GetComponent<UIDynamicSlider>();
            ui.gameObject.transform.SetParent(transform, false);
            ui.Configure(jsf.name, jsf.min, jsf.max, jsf.val, jsf.constrained, "F2", true, !jsf.constrained);
            jsf.slider = ui.slider;
            return ui;
        }
        protected UIDynamicButton CreateButton(string label, bool rightSide = false)
        {
            var ui = Instantiate(plugin.manager.configurableButtonPrefab).GetComponent<UIDynamicButton>();
            ui.gameObject.transform.SetParent(transform, false);
            ui.label = label;
            return ui;
        }
        protected UIDynamicToggle CreateToggle(JSONStorableBool jsb, bool rightSide = false)
        {

            var ui = Instantiate(plugin.manager.configurableTogglePrefab).GetComponent<UIDynamicToggle>();
            ui.gameObject.transform.SetParent(transform, false);
            ui.label = jsb.name;
            jsb.toggle = ui.toggle;
            return ui;
        }
        protected UIDynamicTextField CreateTextField(JSONStorableString jss, bool rightSide = false)
        {
            var ui = Instantiate(plugin.manager.configurableTextFieldPrefab).GetComponent<UIDynamicTextField>();
            ui.gameObject.transform.SetParent(transform, false);
            jss.dynamicText = ui;
            return ui;
        }
        protected UIDynamicPopup CreateScrollablePopup(JSONStorableStringChooser jsc, bool rightSide = false)
        {
            var ui = Instantiate(plugin.manager.configurableScrollablePopupPrefab).GetComponent<UIDynamicPopup>();
            ui.gameObject.transform.SetParent(transform, false);
            ui.label = jsc.name;
            jsc.popup = ui.popup;
            return ui;
        }

        public UIDynamicTextField CreateTextInput(JSONStorableString jss, bool rightSide = false)
        {
            var textfield = CreateTextField(jss, rightSide);
            textfield.height = 20f;
            textfield.backgroundColor = Color.white;
            var input = textfield.gameObject.AddComponent<InputField>();
            var rect = input.GetComponent<RectTransform>().sizeDelta = new Vector2(1f, 0.4f);
            input.textComponent = textfield.UItext;
            jss.inputField = input;
            return textfield;
        }

        protected void RemovePopup(JSONStorableStringChooser component)
        {
            if (component.popup == null) return;
            component.popup = null;
        }

        protected void RemoveSlider(JSONStorableFloat component)
        {
            if (component.slider == null) return;
            component.slider = null;
        }

        protected void RemoveTextField(JSONStorableString component)
        {
            if (component.dynamicText == null) return;
            component.dynamicText = null;
        }

        protected void RemoveToggle(JSONStorableBool component)
        {
            if (component.toggle == null) return;
            component.toggle = null;
        }

        protected void RemoveButton(UIDynamicButton component)
        {
            if (component == null) return;
            Destroy(component.gameObject.transform);
        }

        protected void RemovePopup(UIDynamicPopup component)
        {
            if (component == null) return;
            Destroy(component.gameObject.transform);
        }

        protected void RemoveSlider(UIDynamicSlider component)
        {
            if (component == null) return;
            Destroy(component.gameObject.transform);
        }

        protected void RemoveTextField(UIDynamicTextField component)
        {
            if (component == null) return;
            Destroy(component.gameObject.transform);
        }

        protected void RemoveToggle(UIDynamicToggle component)
        {
            if (component == null) return;
            Destroy(component.gameObject.transform);
        }

        protected void RemoveSpacer(UIDynamic component)
        {
            if (component == null) return;
            Destroy(component.gameObject.transform);
        }
    }
}

