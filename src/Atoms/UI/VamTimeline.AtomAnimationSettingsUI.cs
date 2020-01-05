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
                AnimationUpdated();
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
            AnimationUpdated();
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

        public override void Remove()
        {
            base.Remove();
        }
    }
}

