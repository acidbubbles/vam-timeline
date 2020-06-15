using System;
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
    public abstract class ScreenBase : IDisposable
    {
        public class ScreenChangeRequestedEvent : UnityEvent<string> { }

        public ScreenChangeRequestedEvent onScreenChangeRequested = new ScreenChangeRequestedEvent();
        public abstract string name { get; }

        protected AtomAnimation animation => plugin.animation;

        private readonly List<UIDynamic> _components = new List<UIDynamic>();
        private readonly List<JSONStorableParam> _storables = new List<JSONStorableParam>();
        protected IAtomPlugin plugin;
        protected AtomAnimationClip current;
        protected bool _disposing;

        protected ScreenBase(IAtomPlugin plugin)
        {
            this.plugin = plugin;
        }

        public virtual void Init()
        {
            plugin.animation.onCurrentAnimationChanged.AddListener(OnCurrentAnimationChanged);
            current = plugin.animation?.current;
        }

        protected virtual void OnCurrentAnimationChanged(AtomAnimation.CurrentAnimationChangedEventArgs args)
        {
            current = plugin.animation?.current;
        }

        protected void InitClipboardUI(bool rightSide)
        {
            var container = plugin.CreateSpacer(rightSide);
            RegisterComponent(container);
            container.height = 60f;

            var group = container.gameObject.AddComponent<GridLayoutGroup>();
            group.constraint = GridLayoutGroup.Constraint.Flexible;
            group.constraintCount = 3;
            group.spacing = Vector2.zero;
            group.cellSize = new Vector2(512f / 3f, 50f);
            group.childAlignment = TextAnchor.MiddleCenter;

            {
                var btn = UnityEngine.Object.Instantiate(plugin.manager.configurableButtonPrefab).GetComponent<UIDynamicButton>();
                btn.gameObject.transform.SetParent(group.transform, false);
                btn.label = "Cut";
                btn.button.onClick.AddListener(() => plugin.cutJSON.actionCallback());
            }

            {
                var btn = UnityEngine.Object.Instantiate(plugin.manager.configurableButtonPrefab).GetComponent<UIDynamicButton>();
                btn.gameObject.transform.SetParent(group.transform, false);
                btn.label = "Copy";
                btn.button.onClick.AddListener(() => plugin.copyJSON.actionCallback());
            }

            {
                var btn = UnityEngine.Object.Instantiate(plugin.manager.configurableButtonPrefab).GetComponent<UIDynamicButton>();
                btn.gameObject.transform.SetParent(group.transform, false);
                btn.label = "Paste";
                btn.button.onClick.AddListener(() => plugin.pasteJSON.actionCallback());
            }
        }

        protected UIDynamic CreateSpacer(bool rightSide)
        {
            var spacerUI = plugin.CreateSpacer(rightSide);
            spacerUI.height = 30f;
            RegisterComponent(spacerUI);
            return spacerUI;
        }

        protected UIDynamicButton CreateChangeScreenButton(string label, string screenName, bool rightSide)
        {
            var ui = plugin.CreateButton(label, rightSide);
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

        public virtual void Dispose()
        {
            onScreenChangeRequested.RemoveAllListeners();
            plugin.animation.onCurrentAnimationChanged.RemoveListener(OnCurrentAnimationChanged);

            _disposing = true;
            foreach (var component in _storables)
            {
                if (component == null) continue;

                if (component is JSONStorableStringChooser)
                    plugin.RemovePopup((JSONStorableStringChooser)component);
                else if (component is JSONStorableFloat)
                    plugin.RemoveSlider((JSONStorableFloat)component);
                else if (component is JSONStorableString)
                    plugin.RemoveTextField((JSONStorableString)component);
                else if (component is JSONStorableBool)
                    plugin.RemoveToggle((JSONStorableBool)component);
                else
                    SuperController.LogError($"VamTimeline: Cannot remove component {component}");
            }
            _storables.Clear();

            foreach (var component in _components)
            {
                if (component is UIDynamicButton)
                    plugin.RemoveButton((UIDynamicButton)component);
                else if (component is UIDynamicPopup)
                    plugin.RemovePopup((UIDynamicPopup)component);
                else if (component is UIDynamicSlider)
                    plugin.RemoveSlider((UIDynamicSlider)component);
                else if (component is UIDynamicTextField)
                    plugin.RemoveTextField((UIDynamicTextField)component);
                else if (component is UIDynamicToggle)
                    plugin.RemoveToggle((UIDynamicToggle)component);
                else
                    plugin.RemoveSpacer(component);
            }
            _components.Clear();
        }
    }
}

