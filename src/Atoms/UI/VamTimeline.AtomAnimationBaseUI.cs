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
    public abstract class AtomAnimationBaseUI
    {
        public abstract string Name { get; }

        private UIDynamicButton _undoUI;
        protected List<UIDynamic> _components = new List<UIDynamic>();
        protected List<JSONStorableParam> _linkedStorables = new List<JSONStorableParam>();
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
            UIUpdated();
        }

        public virtual void AnimationFrameUpdated()
        {
        }

        public virtual void UIUpdated()
        {
        }

        protected void InitPlaybackUI(bool rightSide)
        {
            var scrubberUI = Plugin.CreateSlider(Plugin.ScrubberJSON);
            scrubberUI.valueFormat = "F3";
            _linkedStorables.Add(Plugin.ScrubberJSON);

            var playUI = Plugin.CreateButton("\u25B6 Play", rightSide);
            playUI.button.onClick.AddListener(() => Plugin.PlayJSON.actionCallback());
            _components.Add(playUI);

            var stopUI = Plugin.CreateButton("\u25A0 Stop", rightSide);
            stopUI.button.onClick.AddListener(() => Plugin.StopJSON.actionCallback());
            _components.Add(stopUI);
        }

        protected void InitAnimationSelectorUI(bool rightSide)
        {
            var animationUI = Plugin.CreateScrollablePopup(Plugin.AnimationJSON, rightSide);
            animationUI.popupPanelHeight = 800f;
            _linkedStorables.Add(Plugin.AnimationJSON);
        }

        protected void InitFrameNavUI(bool rightSide)
        {
            var selectedControllerUI = Plugin.CreateScrollablePopup(Plugin.FilterAnimationTargetJSON, rightSide);
            selectedControllerUI.popupPanelHeight = 600f;
            _linkedStorables.Add(Plugin.FilterAnimationTargetJSON);

            var nextFrameUI = Plugin.CreateButton("\u2192 Next Frame", rightSide);
            nextFrameUI.button.onClick.AddListener(() => Plugin.NextFrameJSON.actionCallback());
            _components.Add(nextFrameUI);

            var previousFrameUI = Plugin.CreateButton("\u2190 Previous Frame", rightSide);
            previousFrameUI.button.onClick.AddListener(() => Plugin.PreviousFrameJSON.actionCallback());
            _components.Add(previousFrameUI);

        }

        protected void InitClipboardUI(bool rightSide)
        {
            var cutUI = Plugin.CreateButton("Cut / Delete Frame", rightSide);
            cutUI.button.onClick.AddListener(() => Plugin.CutJSON.actionCallback());
            _components.Add(cutUI);

            var copyUI = Plugin.CreateButton("Copy Frame", rightSide);
            copyUI.button.onClick.AddListener(() => Plugin.CopyJSON.actionCallback());
            _components.Add(copyUI);

            var pasteUI = Plugin.CreateButton("Paste Frame", rightSide);
            pasteUI.button.onClick.AddListener(() => Plugin.PasteJSON.actionCallback());
            _components.Add(pasteUI);

            _undoUI = Plugin.CreateButton("Undo", rightSide);
            _undoUI.button.onClick.AddListener(() => Plugin.UndoJSON.actionCallback());
            _components.Add(_undoUI);
        }

        protected void InitDisplayUI(bool rightSide, float height = 300f)
        {
            var displayUI = Plugin.CreateTextField(Plugin.DisplayJSON, rightSide);
            displayUI.height = height;
            _components.Add(displayUI);
            _linkedStorables.Add(Plugin.DisplayJSON);
        }

        protected void CreateSpacer(bool rightSide)
        {
            var spacerUI = Plugin.CreateSpacer(rightSide);
            spacerUI.height = 30f;
            _components.Add(spacerUI);
        }

        public virtual void Remove()
        {
            foreach (var component in _linkedStorables)
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
            _linkedStorables.Clear();

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

