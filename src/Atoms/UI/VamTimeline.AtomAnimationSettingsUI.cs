using System.Linq;
using UnityEngine.UI;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class AtomAnimationSettingsUI : AtomAnimationBaseUI
    {
        private UIDynamicButton _undoUI;
        private UIDynamicButton _toggleControllerUI;

        public AtomAnimationSettingsUI(IAtomPlugin plugin)
            : base(plugin)
        {

        }
        public override void Init()
        {
            // Left side

            InitPlaybackUI(false);

            InitFrameNavUI(false);

            var changeCurveUI = _plugin.CreatePopup(_plugin._changeCurveJSON, false);
            changeCurveUI.popupPanelHeight = 800f;

            var smoothAllFramesUI = _plugin.CreateButton("Smooth All Frames", false);
            smoothAllFramesUI.button.onClick.AddListener(() => _plugin._smoothAllFramesJSON.actionCallback());

            InitClipboardUI(false);

            // Right side

            InitAnimationSettingsUI(true);

            var addControllerUI = _plugin.CreateScrollablePopup(_plugin._addControllerListJSON, true);
            addControllerUI.popupPanelHeight = 800f;

            _toggleControllerUI = _plugin.CreateButton("Add/Remove Controller", true);
            _toggleControllerUI.button.onClick.AddListener(() => _plugin._toggleControllerJSON.actionCallback());

            var linkedAnimationPatternUI = _plugin.CreateScrollablePopup(_plugin._linkedAnimationPatternJSON, true);
            linkedAnimationPatternUI.popupPanelHeight = 800f;
            linkedAnimationPatternUI.popup.onOpenPopupHandlers += () => _plugin._linkedAnimationPatternJSON.choices = new[] { "" }.Concat(SuperController.singleton.GetAtoms().Where(a => a.type == "AnimationPattern").Select(a => a.uid)).ToList();

            InitDisplayUI(true);
        }

        protected void InitPlaybackUI(bool rightSide)
        {
            var animationUI = _plugin.CreateScrollablePopup(_plugin._animationJSON, rightSide);
            animationUI.popupPanelHeight = 800f;

            _plugin.CreateSlider(_plugin._scrubberJSON);

            var playUI = _plugin.CreateButton("\u25B6 Play", rightSide);
            playUI.button.onClick.AddListener(() => _plugin._playJSON.actionCallback());

            var stopUI = _plugin.CreateButton("\u25A0 Stop", rightSide);
            stopUI.button.onClick.AddListener(() => _plugin._stopJSON.actionCallback());
        }

        protected void InitFrameNavUI(bool rightSide)
        {
            var selectedControllerUI = _plugin.CreateScrollablePopup(_plugin._filterAnimationTargetJSON, rightSide);
            selectedControllerUI.popupPanelHeight = 800f;

            var nextFrameUI = _plugin.CreateButton("\u2192 Next Frame", rightSide);
            nextFrameUI.button.onClick.AddListener(() => _plugin._nextFrameJSON.actionCallback());

            var previousFrameUI = _plugin.CreateButton("\u2190 Previous Frame", rightSide);
            previousFrameUI.button.onClick.AddListener(() => _plugin._previousFrameJSON.actionCallback());

        }

        protected void InitClipboardUI(bool rightSide)
        {
            var cutUI = _plugin.CreateButton("Cut / Delete Frame", rightSide);
            cutUI.button.onClick.AddListener(() => _plugin._cutJSON.actionCallback());

            var copyUI = _plugin.CreateButton("Copy Frame", rightSide);
            copyUI.button.onClick.AddListener(() => _plugin._copyJSON.actionCallback());

            var pasteUI = _plugin.CreateButton("Paste Frame", rightSide);
            pasteUI.button.onClick.AddListener(() => _plugin._pasteJSON.actionCallback());

            _undoUI = _plugin.CreateButton("Undo", rightSide);
            _undoUI.button.interactable = false;
            _undoUI.button.onClick.AddListener(() => _plugin._undoJSON.actionCallback());
        }

        protected void InitAnimationSettingsUI(bool rightSide)
        {
            var lockedUI = _plugin.CreateToggle(_plugin._lockedJSON, rightSide);
            lockedUI.label = "Locked (Performance Mode)";

            var addAnimationUI = _plugin.CreateButton("Add New Animation", rightSide);
            addAnimationUI.button.onClick.AddListener(() => _plugin._addAnimationJSON.actionCallback());

            _plugin.CreateSlider(_plugin._lengthJSON, rightSide);

            _plugin.CreateSlider(_plugin._speedJSON, rightSide);

            _plugin.CreateSlider(_plugin._blendDurationJSON, rightSide);
        }

        protected void InitDisplayUI(bool rightSide)
        {
            _plugin.CreatePopup(_plugin._displayModeJSON, rightSide);

            _plugin.CreateTextField(_plugin._displayJSON, rightSide);
        }

        public override void UIUpdated()
        {
            base.UIUpdated();
            UpdateToggleAnimatedControllerButton(_plugin._addControllerListJSON.val);
        }

        private void UpdateToggleAnimatedControllerButton(string name)
        {
            var btnText = _toggleControllerUI.button.GetComponentInChildren<Text>();
            if (string.IsNullOrEmpty(name))
            {
                btnText.text = "Add/Remove Controller";
                _toggleControllerUI.button.interactable = false;
                return;
            }

            _toggleControllerUI.button.interactable = true;
            if (_plugin._animation.Current.TargetControllers.Any(c => c.Controller.name == name))
                btnText.text = "Remove Controller";
            else
                btnText.text = "Add Controller";
        }
    }
}

