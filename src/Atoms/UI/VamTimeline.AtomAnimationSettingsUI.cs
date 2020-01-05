using System;
using System.Collections.Generic;
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
        public const string ScreenName = "Animation Settings";
        public override string Name => ScreenName;

        private JSONStorableStringChooser _addControllerListJSON;
        private JSONStorableAction _toggleControllerJSON;
        private JSONStorableStringChooser _linkedAnimationPatternJSON;
        private JSONStorableStringChooser _addStorableListJSON;
        private JSONStorableStringChooser _addParamListJSON;
        private UIDynamicButton _toggleControllerUI;
        private UIDynamicButton _toggleFloatParamUI;

        public AtomAnimationSettingsUI(IAtomPlugin plugin)
            : base(plugin)
        {

        }
        public override void Init()
        {
            base.Init();

            // Left side

            InitAnimationSelectorUI(false);

            // Right side

            InitDisplayUI(false, 800f);

            InitAnimationSettingsUI(true);

            _addControllerListJSON = new JSONStorableStringChooser("Animate Controller", Plugin.ContainingAtom.freeControllers.Select(fc => fc.name).ToList(), Plugin.ContainingAtom.freeControllers.Select(fc => fc.name).FirstOrDefault(), "Animate controller", (string name) => UIUpdated())
            {
                isStorable = false
            };

            _toggleControllerJSON = new JSONStorableAction("Toggle Controller", () => ToggleAnimatedController());

            _linkedAnimationPatternJSON = new JSONStorableStringChooser("Linked Animation Pattern", new[] { "" }.Concat(SuperController.singleton.GetAtoms().Where(a => a.type == "AnimationPattern").Select(a => a.uid)).ToList(), "", "Linked Animation Pattern", (string uid) => LinkAnimationPattern(uid))
            {
                isStorable = false
            };

            var addControllerUI = Plugin.CreateScrollablePopup(_addControllerListJSON, true);
            addControllerUI.popupPanelHeight = 800f;
            _linkedStorables.Add(_addControllerListJSON);

            _toggleControllerUI = Plugin.CreateButton("Add/Remove Controller", true);
            _toggleControllerUI.button.onClick.AddListener(() => _toggleControllerJSON.actionCallback());
            _components.Add(_toggleControllerUI);

            var linkedAnimationPatternUI = Plugin.CreateScrollablePopup(_linkedAnimationPatternJSON, true);
            linkedAnimationPatternUI.popupPanelHeight = 800f;
            linkedAnimationPatternUI.popup.onOpenPopupHandlers += () => _linkedAnimationPatternJSON.choices = new[] { "" }.Concat(SuperController.singleton.GetAtoms().Where(a => a.type == "AnimationPattern").Select(a => a.uid)).ToList();
            _linkedStorables.Add(_linkedAnimationPatternJSON);

            InitFloatParamsCustomUI();
        }

        private void InitFloatParamsCustomUI()
        {
            var storables = GetInterestingStorableIDs().ToList();
            _addStorableListJSON = new JSONStorableStringChooser("Animate Storable", storables, storables.Contains("geometry") ? "geometry" : storables.FirstOrDefault(), "Animate Storable", (string name) => RefreshStorableFloatsList())
            {
                isStorable = false
            };

            _addParamListJSON = new JSONStorableStringChooser("Animate Param", new List<string>(), "", "Animate Param", (string name) => UIUpdated())
            {
                isStorable = false
            };

            var addFloatParamListUI = Plugin.CreateScrollablePopup(_addStorableListJSON, true);
            addFloatParamListUI.popupPanelHeight = 800f;
            addFloatParamListUI.popup.onOpenPopupHandlers += () => _addStorableListJSON.choices = GetInterestingStorableIDs().ToList();
            _linkedStorables.Add(_addStorableListJSON);

            var addParamListUI = Plugin.CreateScrollablePopup(_addParamListJSON, true);
            addParamListUI.popupPanelHeight = 700f;
            addParamListUI.popup.onOpenPopupHandlers += () => RefreshStorableFloatsList();
            _linkedStorables.Add(_addParamListJSON);

            _toggleFloatParamUI = Plugin.CreateButton("Add/Remove Param", true);
            _toggleFloatParamUI.button.onClick.AddListener(() => ToggleAnimatedFloatParam());
            _components.Add(_toggleFloatParamUI);
        }

        private void LinkAnimationPattern(string uid)
        {
            if (string.IsNullOrEmpty(uid))
            {
                Plugin.Animation.Current.AnimationPattern = null;
                return;
            }
            var animationPattern = SuperController.singleton.GetAtomByUid(uid)?.GetComponentInChildren<AnimationPattern>();
            if (animationPattern == null)
            {
                SuperController.LogError($"Could not find Animation Pattern '{uid}'");
                return;
            }
            Plugin.Animation.Current.AnimationPattern = animationPattern;
            animationPattern.SetBoolParamValue("autoPlay", false);
            animationPattern.SetBoolParamValue("pause", false);
            animationPattern.SetBoolParamValue("loop", false);
            animationPattern.SetBoolParamValue("loopOnce", false);
            animationPattern.SetFloatParamValue("speed", Plugin.Animation.Speed);
            animationPattern.ResetAnimation();
            Plugin.AnimationModified();
        }

        private void ToggleAnimatedController()
        {
            try
            {
                var uid = _addControllerListJSON.val;
                var controller = Plugin.ContainingAtom.freeControllers.Where(x => x.name == uid).FirstOrDefault();
                if (controller == null)
                {
                    SuperController.LogError($"Controller {uid} in atom {Plugin.ContainingAtom.uid} does not exist");
                    return;
                }
                if (Plugin.Animation.Current.TargetControllers.Any(c => c.Controller == controller))
                {
                    Plugin.Animation.Remove(controller);
                }
                else
                {
                    controller.currentPositionState = FreeControllerV3.PositionState.On;
                    controller.currentRotationState = FreeControllerV3.RotationState.On;
                    var animController = Plugin.Animation.Add(controller);
                    animController.SetKeyframeToCurrentTransform(0f);
                }
                Plugin.AnimationModified();
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.AtomPlugin.AddSelectedController: " + exc);
            }
        }

        private void ToggleAnimatedFloatParam()
        {
            try
            {
                var storable = Plugin.ContainingAtom.GetStorableByID(_addStorableListJSON.val);
                if (storable == null)
                {
                    SuperController.LogError($"Storable {_addStorableListJSON.val} in atom {Plugin.ContainingAtom.uid} does not exist");
                    return;
                }
                var sourceFloatParam = storable.GetFloatJSONParam(_addParamListJSON.val);
                if (sourceFloatParam == null)
                {
                    SuperController.LogError($"Param {_addParamListJSON.val} in atom {Plugin.ContainingAtom.uid} does not exist");
                    return;
                }
                if (Plugin.Animation.Current.TargetFloatParams.Any(c => c.FloatParam == sourceFloatParam))
                {
                    Plugin.Animation.Current.TargetFloatParams.Remove(Plugin.Animation.Current.TargetFloatParams.First(c => c.FloatParam == sourceFloatParam));
                }
                else
                {
                    var target = new FloatParamAnimationTarget(storable, sourceFloatParam, Plugin.Animation.AnimationLength);
                    target.SetKeyframe(0, sourceFloatParam.val);
                    Plugin.Animation.Current.TargetFloatParams.Add(target);
                }
                Plugin.AnimationModified();
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.AtomAnimationSettingsUI.ToggleAnimatedFloatParam: " + exc);
            }
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

        private IEnumerable<string> GetInterestingStorableIDs()
        {
            foreach (var storableId in Plugin.ContainingAtom.GetStorableIDs())
            {
                var storable = Plugin.ContainingAtom.GetStorableByID(storableId);
                if (storable.GetFloatParamNames().Count > 0)
                    yield return storableId;
            }
        }

        private void RefreshStorableFloatsList()
        {
            if (string.IsNullOrEmpty(_addStorableListJSON.val))
            {
                _addParamListJSON.choices = new List<string>();
                _addParamListJSON.val = "";
                return;
            }
            var values = Plugin.ContainingAtom.GetStorableByID(_addStorableListJSON.val)?.GetFloatParamNames() ?? new List<string>();
            _addParamListJSON.choices = values;
            if (!values.Contains(_addParamListJSON.val))
                _addParamListJSON.val = values.FirstOrDefault();
        }

        private void UpdateFloatParam(FloatParamAnimationTarget target, JSONStorableFloat sourceFloatParam, float val)
        {
            sourceFloatParam.val = val;
            // TODO: This should be done by the controller (updating the animation resets the time)
            var time = Plugin.Animation.Time;
            target.SetKeyframe(time, val);
            Plugin.Animation.RebuildAnimation();
            // TODO: Test if this works (was using Plugin.UpdateTime)
            Plugin.ScrubberJSON.val = time;
            Plugin.AnimationModified();
        }

        public override void UIUpdated()
        {
            base.UIUpdated();
            UpdateToggleAnimatedControllerButton(_addControllerListJSON.val);
        }

        public override void AnimationModified()
        {
            base.AnimationModified();
            _linkedAnimationPatternJSON.valNoCallback = Plugin.Animation.Current.AnimationPattern?.containingAtom.uid ?? "";
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

        public override void Remove()
        {
            base.Remove();
        }
    }
}

