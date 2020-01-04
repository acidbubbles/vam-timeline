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
    public class JSONStorableFloatsPluginImpl : PluginImplBase<JSONStorableFloatAnimation, JSONStorableFloatAnimationClip, JSONStorableFloatAnimationTarget>
    {
        private class JSONStorableFloatJSONRef
        {
            public JSONStorable Storable;
            public JSONStorableFloat SourceFloatParam;
            public JSONStorableFloat Proxy;
            public UIDynamicSlider Slider;
        }

        private List<JSONStorableFloatJSONRef> _jsfJSONRefs;

        // Storables
        private JSONStorableStringChooser _addStorableListJSON;
        private JSONStorableStringChooser _addParamListJSON;

        // UI
        private UIDynamicButton _toggleJSONStorableFloatUI;

        // Backup
        protected override string BackupStorableName => StorableNames.JSONStorableFloatsAnimationBackup;

        public JSONStorableFloatsPluginImpl(IAnimationPlugin plugin)
            : base(plugin)
        {
        }

        #region Initialization

        public void Init()
        {
            if (_plugin.ContainingAtom.type != "Person")
            {
                SuperController.LogError("VamTimeline.JSONStorableFloatsAnimation can only be applied on a Person atom.");
                return;
            }

            RegisterSerializer(new JSONStorableFloatsAnimationSerializer(_plugin.ContainingAtom));
            InitStorables();
            InitCustomUI();
            // Try loading from backup
            _plugin.StartCoroutine(CreateAnimationIfNoneIsLoaded());
        }

        private void InitStorables()
        {
            InitCommonStorables();

            var storables = _plugin.ContainingAtom.GetStorableIDs();
            _addStorableListJSON = new JSONStorableStringChooser("Animate Storable", storables, storables.Contains("geometry") ? "geometry" : storables.FirstOrDefault(), "Animate Storable", (string name) => RefreshStorableFloatsList())
            {
                isStorable = false
            };

            _addParamListJSON = new JSONStorableStringChooser("Animate Param", new List<string>(), "", "Animate Param", (string name) => UpdateToggleAnimatedJSONStorableFloatButton(name))
            {
                isStorable = false
            };

            RefreshStorableFloatsList();
        }

        private void RefreshStorableFloatsList()
        {
            if (string.IsNullOrEmpty(_addStorableListJSON.val))
            {
                _addParamListJSON.choices = new List<string>();
                _addParamListJSON.val = "";
                return;
            }
            var values = _plugin.ContainingAtom.GetStorableByID(_addStorableListJSON.val).GetFloatParamNames();
            if (!values.Contains(_addParamListJSON.val))
                _addParamListJSON.val = values.FirstOrDefault();
        }

        private void InitCustomUI()
        {
            // Left side

            InitPlaybackUI(false);

            InitFrameNavUI(false);

            InitClipboardUI(false);

            InitAnimationSettingsUI(false);

            // Right side

            InitDisplayUI(true);

            var addJSONStorableFloatListUI = _plugin.CreateScrollablePopup(_addStorableListJSON, true);
            addJSONStorableFloatListUI.popupPanelHeight = 800f;
            addJSONStorableFloatListUI.popup.onOpenPopupHandlers += () => _addStorableListJSON.choices = _plugin.ContainingAtom.GetStorableIDs();

            var addParamListUI = _plugin.CreateScrollablePopup(_addParamListJSON, true);
            addParamListUI.popupPanelHeight = 700f;
            addParamListUI.popup.onOpenPopupHandlers += () => RefreshJSONStorableFloatsListUI();

            _toggleJSONStorableFloatUI = _plugin.CreateButton("Add/Remove JSONStorableFloat", true);
            _toggleJSONStorableFloatUI.button.onClick.AddListener(() => ToggleAnimatedJSONStorableFloat());

            RefreshJSONStorableFloatsListUI();
        }

        private void RefreshJSONStorableFloatsListUI()
        {
            if (_jsfJSONRefs != null)
            {
                foreach (var jsfJSONRef in _jsfJSONRefs)
                {
                    _plugin.RemoveSlider(jsfJSONRef.Slider);
                }
            }
            if (_animation == null) return;
            // TODO: This is expensive, though rarely occuring
            _jsfJSONRefs = new List<JSONStorableFloatJSONRef>();
            foreach (var target in _animation.Current.Targets)
            {
                var jsfJSONRef = target.FloatParam;
                var jsfJSONProxy = new JSONStorableFloat($"{target.Storable.name}/{jsfJSONRef.name}", jsfJSONRef.defaultVal, (float val) => UpdateJSONStorableFloat(target, jsfJSONRef, val), jsfJSONRef.min, jsfJSONRef.max, jsfJSONRef.constrained, true);
                var slider = _plugin.CreateSlider(jsfJSONProxy, true);
                _jsfJSONRefs.Add(new JSONStorableFloatJSONRef
                {
                    Storable = target.Storable,
                    SourceFloatParam = jsfJSONRef,
                    Proxy = jsfJSONProxy,
                    Slider = slider
                });
            }
        }

        #endregion

        #region Lifecycle

        protected override void UpdatePlaying()
        {
            _animation.Update();

            if (!IsLocked)
                ContextUpdatedCustom();
        }

        protected override void UpdateNotPlaying()
        {
        }

        public void OnEnable()
        {
        }

        public void OnDisable()
        {
            if (_animation == null) return;

            _animation.Stop();
        }

        public void OnDestroy()
        {
        }

        #endregion

        #region Callbacks

        private void UpdateToggleAnimatedJSONStorableFloatButton(string name)
        {
            if (_toggleJSONStorableFloatUI == null) return;

            var btnText = _toggleJSONStorableFloatUI.button.GetComponentInChildren<Text>();
            if (_animation == null || string.IsNullOrEmpty(name))
            {
                btnText.text = "Add/Remove Param";
                _toggleJSONStorableFloatUI.button.interactable = false;
                return;
            }

            _toggleJSONStorableFloatUI.button.interactable = true;
            if (_animation.Current.Targets.Any(c => c.FloatParam.name == name))
                btnText.text = "Remove Param";
            else
                btnText.text = "Add Param";
        }

        private void ToggleAnimatedJSONStorableFloat()
        {
            try
            {
                var storable = _plugin.ContainingAtom.GetStorableByID(_addStorableListJSON.val);
                if (storable == null)
                {
                    SuperController.LogError($"Storable {_addStorableListJSON.val} in atom {_plugin.ContainingAtom.uid} does not exist");
                    return;
                }
                var sourceFloatParam = storable.GetFloatJSONParam(_addParamListJSON.val);
                if (sourceFloatParam == null)
                {
                    SuperController.LogError($"Param {_addParamListJSON.val} in atom {_plugin.ContainingAtom.uid} does not exist");
                    return;
                }
                if (_animation.Current.Targets.Any(c => c.FloatParam == sourceFloatParam))
                {
                    _animation.Current.Targets.Remove(_animation.Current.Targets.First(c => c.FloatParam == sourceFloatParam));
                }
                else
                {
                    var target = new JSONStorableFloatAnimationTarget(storable, sourceFloatParam, _animation.AnimationLength);
                    target.SetKeyframe(0, sourceFloatParam.val);
                    _animation.Current.Targets.Add(target);
                }
                RefreshJSONStorableFloatsListUI();
                AnimationUpdated();
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.JSONStorableFloatsPlugin.ToggleAnimatedJSONStorableFloat: " + exc);
            }
        }

        private void UpdateJSONStorableFloat(JSONStorableFloatAnimationTarget target, JSONStorableFloat sourceFloatParam, float val)
        {
            sourceFloatParam.val = val;
            // TODO: This should be done by the controller (updating the animation resets the time)
            var time = _animation.Time;
            target.SetKeyframe(time, val);
            _animation.RebuildAnimation();
            UpdateTime(time);
            AnimationUpdated();
        }

        #endregion

        #region Updates

        protected override void StateRestored()
        {
            UpdateToggleAnimatedJSONStorableFloatButton(_addParamListJSON.val);
            RefreshJSONStorableFloatsListUI();
        }

        protected override void AnimationUpdatedCustom()
        {
        }

        protected override void ContextUpdatedCustom()
        {
            if (_jsfJSONRefs != null)
            {
                foreach (var jsfJSONRef in _jsfJSONRefs)
                    jsfJSONRef.Proxy.valNoCallback = jsfJSONRef.SourceFloatParam.val;
            }
        }

        #endregion
    }
}
