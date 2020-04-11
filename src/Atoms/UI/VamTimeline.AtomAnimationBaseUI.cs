using System;
using System.Collections.Generic;

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

        private List<UIDynamic> _components = new List<UIDynamic>();
        private List<JSONStorableParam> _storables = new List<JSONStorableParam>();
        protected IAtomPlugin Plugin;

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
        }

        public virtual void AnimationFrameUpdated()
        {
        }

        protected void InitPlaybackUI(bool rightSide)
        {
            if (Plugin.ScrubberJSON.slider != null) throw new InvalidOperationException("Another screen was not fully unregistered, scrubber is still associated with another slider.");

            var scrubberUI = Plugin.CreateSlider(Plugin.ScrubberJSON);
            scrubberUI.valueFormat = "F3";
            RegisterStorable(Plugin.ScrubberJSON);

            var playUI = Plugin.CreateButton("\u25B6 Play", rightSide);
            playUI.button.onClick.AddListener(() => Plugin.PlayJSON.actionCallback());
            RegisterComponent(playUI);

            var stopUI = Plugin.CreateButton("\u25A0 Stop", rightSide);
            stopUI.button.onClick.AddListener(() => Plugin.StopJSON.actionCallback());
            RegisterComponent(stopUI);
        }

        protected void InitAnimationSelectorUI(bool rightSide)
        {
            var animationUI = Plugin.CreateScrollablePopup(Plugin.AnimationJSON, rightSide);
            if (animationUI == null) throw new NullReferenceException(nameof(animationUI));
            animationUI.popupPanelHeight = 800f;
            RegisterStorable(Plugin.AnimationJSON);
        }

        protected void InitFrameNavUI(bool rightSide)
        {
            var selectedControllerUI = Plugin.CreateScrollablePopup(Plugin.FilterAnimationTargetJSON, rightSide);
            selectedControllerUI.popupPanelHeight = 600f;
            RegisterStorable(Plugin.FilterAnimationTargetJSON);

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

        protected void InitDisplayUI(bool rightSide, float height = 300f)
        {
            var displayUI = Plugin.CreateTextField(Plugin.DisplayJSON, rightSide);
            displayUI.height = height;
            RegisterComponent(displayUI);
            RegisterStorable(Plugin.DisplayJSON);
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
            RegisterStorable(v);
            return v;
        }

        protected T RegisterComponent<T>(T v)
            where T : UIDynamic
        {
            RegisterComponent(v);
            return v;
        }

        public virtual void Dispose()
        {
            foreach (var component in _storables)
            {
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
        }
    }
}

