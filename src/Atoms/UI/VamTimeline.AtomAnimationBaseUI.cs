using System;
using System.Collections.Generic;
using CurveEditor.UI;
using UnityEngine;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public abstract class AtomAnimationBaseUI : IDisposable
    {
        public abstract string Name { get; }

        private readonly List<UIDynamic> _components = new List<UIDynamic>();
        private readonly List<JSONStorableParam> _storables = new List<JSONStorableParam>();
        protected IAtomPlugin Plugin;
        private UICurveEditor _curveUI;
        private UIDynamic _curveEditorContainer;
        private readonly List<CurveLine> _lines = new List<CurveLine>();

        protected AtomAnimationBaseUI(IAtomPlugin plugin)
        {
            Plugin = plugin;
        }

        public virtual void Init()
        {
        }

        public virtual void UpdatePlaying()
        {
        }

        public virtual void AnimationModified()
        {
            foreach (var line in _lines)
            {
                line.SetPointsFromCurve();
            }
        }

        public virtual void AnimationFrameUpdated()
        {
            _curveUI.SetScrubber(Plugin.Animation.Time);
        }

        protected void InitPlaybackUI(bool rightSide)
        {
            if (Plugin.ScrubberJSON.slider != null) throw new InvalidOperationException("Another screen was not fully unregistered, scrubber is still associated with another slider.");

            RegisterStorable(Plugin.ScrubberJSON);
            var scrubberUI = Plugin.CreateSlider(Plugin.ScrubberJSON);
            scrubberUI.valueFormat = "F3";
            RegisterComponent(scrubberUI);

            var playUI = Plugin.CreateButton("\u25B6 Play", rightSide);
            playUI.button.onClick.AddListener(() => Plugin.PlayJSON.actionCallback());
            RegisterComponent(playUI);

            var stopUI = Plugin.CreateButton("\u25A0 Stop", rightSide);
            stopUI.button.onClick.AddListener(() => Plugin.StopJSON.actionCallback());
            RegisterComponent(stopUI);
        }

        protected void InitAnimationSelectorUI(bool rightSide)
        {
            RegisterStorable(Plugin.AnimationDisplayJSON);
            var animationUI = Plugin.CreateScrollablePopup(Plugin.AnimationDisplayJSON, rightSide);
            animationUI.label = "Animation";
            animationUI.popupPanelHeight = 800f;
            RegisterComponent(animationUI);
        }

        protected void InitFrameNavUI(bool rightSide)
        {
            RegisterStorable(Plugin.FilterAnimationTargetJSON);
            var selectedControllerUI = Plugin.CreateScrollablePopup(Plugin.FilterAnimationTargetJSON, rightSide);
            selectedControllerUI.popupPanelHeight = 600f;
            RegisterComponent(selectedControllerUI);

            var nextFrameUI = Plugin.CreateButton("\u2192 Next Frame", rightSide);
            nextFrameUI.button.onClick.AddListener(() => Plugin.NextFrameJSON.actionCallback());
            RegisterComponent(nextFrameUI);

            var previousFrameUI = Plugin.CreateButton("\u2190 Previous Frame", rightSide);
            previousFrameUI.button.onClick.AddListener(() => Plugin.PreviousFrameJSON.actionCallback());
            RegisterComponent(previousFrameUI);
        }

        protected void InitClipboardUI(bool rightSide)
        {
            var cutUI = Plugin.CreateButton("Cut / Delete Frame", rightSide);
            cutUI.button.onClick.AddListener(() => Plugin.CutJSON.actionCallback());
            RegisterComponent(cutUI);

            var copyUI = Plugin.CreateButton("Copy Frame", rightSide);
            copyUI.button.onClick.AddListener(() => Plugin.CopyJSON.actionCallback());
            RegisterComponent(copyUI);

            var pasteUI = Plugin.CreateButton("Paste Frame", rightSide);
            pasteUI.button.onClick.AddListener(() => Plugin.PasteJSON.actionCallback());
            RegisterComponent(pasteUI);
        }

        protected void InitDisplayUI(bool rightSide, float height = 260f)
        {
            if (Plugin.Animation == null || Plugin.Animation.Current == null) return;
            _curveEditorContainer = Plugin.CreateSpacer(rightSide);
            _curveEditorContainer.height = height;
            RegisterComponent(_curveEditorContainer);
            _curveUI = new UICurveEditor(_curveEditorContainer, 520, _curveEditorContainer.height, buttons: new List<UIDynamicButton>())
            {
                readOnly = true,
                showScrubbers = true
            };
            foreach (var controllerTarget in Plugin.Animation.Current.GetAllOrSelectedControllerTargets())
            {
                _lines.Add(_curveUI.AddCurve(controllerTarget.StorableX, UICurveLineColors.CreateFrom(Color.red), 2));
                _lines.Add(_curveUI.AddCurve(controllerTarget.StorableY, UICurveLineColors.CreateFrom(Color.green), 2));
                _lines.Add(_curveUI.AddCurve(controllerTarget.StorableZ, UICurveLineColors.CreateFrom(Color.blue), 2));
            }
            foreach (var floatParamTarget in Plugin.Animation.Current.GetAllOrSelectedFloatParamTargets())
            {
                _lines.Add(_curveUI.AddCurve(floatParamTarget.StorableValue, UICurveLineColors.CreateFrom(Color.gray), 2));
            }
            RegisterComponent(_curveUI.container);
        }

        protected void CreateSpacer(bool rightSide)
        {
            var spacerUI = Plugin.CreateSpacer(rightSide);
            spacerUI.height = 30f;
            RegisterComponent(spacerUI);
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
                    SuperController.LogError($"Storable {v.name} of atom {Plugin.ContainingAtom.name} was not correctly unregistered.");
            }
            else if (v is JSONStorableFloat)
            {
                if (((JSONStorableFloat)v).slider != null)
                    SuperController.LogError($"Storable {v.name} of atom {Plugin.ContainingAtom.name} was not correctly unregistered.");
            }
            else if (v is JSONStorableString)
            {
                if (((JSONStorableString)v).inputField != null)
                    SuperController.LogError($"Storable {v.name} of atom {Plugin.ContainingAtom.name} was not correctly unregistered.");
            }
            else if (v is JSONStorableBool)
            {
                if (((JSONStorableBool)v).toggle != null)
                    SuperController.LogError($"Storable {v.name} of atom {Plugin.ContainingAtom.name} was not correctly unregistered.");
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
            foreach (var component in _storables)
            {
                if (component == null) continue;

                if (component is JSONStorableStringChooser)
                    Plugin.RemovePopup((JSONStorableStringChooser)component);
                else if (component is JSONStorableFloat)
                    Plugin.RemoveSlider((JSONStorableFloat)component);
                else if (component is JSONStorableString)
                    Plugin.RemoveTextField((JSONStorableString)component);
                else if (component is JSONStorableBool)
                    Plugin.RemoveToggle((JSONStorableBool)component);
                else
                    SuperController.LogError($"VamTimeline: Cannot remove component {component}");
            }
            _storables.Clear();

            foreach (var component in _components)
            {
                if (component is UIDynamicButton)
                    Plugin.RemoveButton((UIDynamicButton)component);
                else if (component is UIDynamicPopup)
                    Plugin.RemovePopup((UIDynamicPopup)component);
                else if (component is UIDynamicSlider)
                    Plugin.RemoveSlider((UIDynamicSlider)component);
                else if (component is UIDynamicTextField)
                    Plugin.RemoveTextField((UIDynamicTextField)component);
                else if (component is UIDynamicToggle)
                    Plugin.RemoveToggle((UIDynamicToggle)component);
                else
                    Plugin.RemoveSpacer(component);
            }
            _components.Clear();

            if (_curveUI != null)
            {
                _curveUI = null;
                _lines.Clear();
            }
        }
    }
}

