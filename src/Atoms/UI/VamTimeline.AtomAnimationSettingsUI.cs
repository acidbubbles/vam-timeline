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
        private UIDynamicButton _toggleControllerUI;

        public AtomAnimationSettingsUI(IAtomPlugin plugin)
            : base(plugin)
        {

        }
        public override void Init()
        {
            base.Init();

            // Left side

            InitPlaybackUI(false);

            InitFrameNavUI(false);

            var changeCurveUI = Plugin.CreatePopup(Plugin.ChangeCurveJSON, false);
            changeCurveUI.popupPanelHeight = 800f;
            _linkedStorables.Add(Plugin.ChangeCurveJSON);

            var smoothAllFramesUI = Plugin.CreateButton("Smooth All Frames", false);
            smoothAllFramesUI.button.onClick.AddListener(() => Plugin.SmoothAllFramesJSON.actionCallback());
            _components.Add(smoothAllFramesUI);

            InitClipboardUI(false);

            // Right side

            InitLockedUI(true);

            InitAnimationSettingsUI(true);

            var addControllerUI = Plugin.CreateScrollablePopup(Plugin.AddControllerListJSON, true);
            addControllerUI.popupPanelHeight = 800f;
            _linkedStorables.Add(Plugin.AddControllerListJSON);

            _toggleControllerUI = Plugin.CreateButton("Add/Remove Controller", true);
            _toggleControllerUI.button.onClick.AddListener(() => Plugin.ToggleControllerJSON.actionCallback());
            _components.Add(_toggleControllerUI);

            var linkedAnimationPatternUI = Plugin.CreateScrollablePopup(Plugin.LinkedAnimationPatternJSON, true);
            linkedAnimationPatternUI.popupPanelHeight = 800f;
            linkedAnimationPatternUI.popup.onOpenPopupHandlers += () => Plugin.LinkedAnimationPatternJSON.choices = new[] { "" }.Concat(SuperController.singleton.GetAtoms().Where(a => a.type == "AnimationPattern").Select(a => a.uid)).ToList();
            _linkedStorables.Add(Plugin.LinkedAnimationPatternJSON);

            InitDisplayUI(true);
        }


        protected void InitAnimationSettingsUI(bool rightSide)
        {
            var addAnimationUI = Plugin.CreateButton("Add New Animation", rightSide);
            addAnimationUI.button.onClick.AddListener(() => Plugin.AddAnimationJSON.actionCallback());
            _components.Add(addAnimationUI);

            Plugin.CreateSlider(Plugin.LengthJSON, rightSide);
            _linkedStorables.Add(Plugin.LengthJSON);

            Plugin.CreateSlider(Plugin.SpeedJSON, rightSide);
            _linkedStorables.Add(Plugin.SpeedJSON);

            Plugin.CreateSlider(Plugin.BlendDurationJSON, rightSide);
            _linkedStorables.Add(Plugin.BlendDurationJSON);
        }

        public override void UIUpdated()
        {
            base.UIUpdated();
            UpdateToggleAnimatedControllerButton(Plugin.AddControllerListJSON.val);
        }

        private void UpdateToggleAnimatedControllerButton(string name)
        {
            if (_toggleControllerUI == null) return;
            var btnText = _toggleControllerUI.button.GetComponentInChildren<Text>();
            if (string.IsNullOrEmpty(name))
            {
                btnText.text = "Add/Remove Controller";
                _toggleControllerUI.button.interactable = false;
                return;
            }

            _toggleControllerUI.button.interactable = true;
            if (Plugin.Animation.Current.TargetControllers.Any(c => c.Controller.name == name))
                btnText.text = "Remove Controller";
            else
                btnText.text = "Add Controller";
        }
    }
}

