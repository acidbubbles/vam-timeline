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
    public class MorphsPluginImpl : PluginImplBase<JSONStorableFloatAnimation, JSONStorableFloatAnimationClip, JSONStorableFloatAnimationTarget>
    {
        private class MorphJSONRef
        {
            public JSONStorableFloat Original;
            public JSONStorableFloat Local;
            public UIDynamicSlider Slider;
        }

        private MorphsList _morphsList;
        private List<MorphJSONRef> _morphJSONRefs;

        // Storables
        private JSONStorableStringChooser _addMorphListJSON;

        // UI
        private UIDynamicButton _toggleMorphUI;

        // Backup
        protected override string BackupStorableName => StorableNames.MorphsAnimationBackup;

        public MorphsPluginImpl(IAnimationPlugin plugin)
            : base(plugin)
        {
        }

        #region Initialization

        public void Init()
        {
            if (_plugin.ContainingAtom.type != "Person")
            {
                SuperController.LogError("VamTimeline.MorphsAnimation can only be applied on a Person atom.");
                return;
            }

            _morphsList = new MorphsList(_plugin.ContainingAtom);

            RegisterSerializer(new MorphsAnimationSerializer(_plugin.ContainingAtom));
            InitStorables();
            InitCustomUI();
            // Try loading from backup
            _plugin.StartCoroutine(CreateAnimationIfNoneIsLoaded());
        }

        private void InitStorables()
        {
            InitCommonStorables();

            _morphsList.Refresh();
            var animatableMorphs = _morphsList.GetAnimatableMorphs().Select(m => m.name).ToList();
            _addMorphListJSON = new JSONStorableStringChooser("Animate Morph", animatableMorphs, animatableMorphs.FirstOrDefault(), "Animate Morph", (string name) => UpdateToggleAnimatedMorphButton(name))
            {
                isStorable = false
            };
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

            var addMorphListUI = _plugin.CreateScrollablePopup(_addMorphListJSON, true);
            addMorphListUI.popupPanelHeight = 800f;

            _toggleMorphUI = _plugin.CreateButton("Add/Remove Morph", true);
            _toggleMorphUI.button.onClick.AddListener(() => ToggleAnimatedMorph());

            RefreshMorphsListUI();
        }

        private void RefreshMorphsListUI()
        {
            _morphsList.Refresh();
            if (_morphJSONRefs != null)
            {
                foreach (var morphJSONRef in _morphJSONRefs)
                {
                    _plugin.RemoveSlider(morphJSONRef.Slider);
                }
            }
            if (_animation == null) return;
            var morphJSONRefs = _morphsList.GetAnimatableMorphs().ToList();
            _morphJSONRefs = new List<MorphJSONRef>();
            foreach (var target in _animation.Current.Targets)
            {
                var morphJSONRef = morphJSONRefs.FirstOrDefault(m => m.name == target.Name);
                var morphJSON = new JSONStorableFloat($"Morph:{morphJSONRef.name}", morphJSONRef.defaultVal, (float val) => UpdateMorph(morphJSONRef, val), morphJSONRef.min, morphJSONRef.max, morphJSONRef.constrained, true);
                var slider = _plugin.CreateSlider(morphJSON, true);
                _morphJSONRefs.Add(new MorphJSONRef
                {
                    Original = morphJSONRef,
                    Local = morphJSON,
                    Slider = slider
                });
            }
        }

        #endregion

        #region Lifecycle

        protected override void UpdatePlaying()
        {
            _animation.Update();

            if (!_lockedJSON.val)
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

        private void UpdateToggleAnimatedMorphButton(string name)
        {
            var btnText = _toggleMorphUI.button.GetComponentInChildren<Text>();
            if (string.IsNullOrEmpty(name))
            {
                btnText.text = "Add/Remove Morph";
                _toggleMorphUI.button.interactable = false;
                return;
            }

            _toggleMorphUI.button.interactable = true;
            if (_animation.Current.Targets.Any(c => c.Storable.name == name))
                btnText.text = "Remove Morph";
            else
                btnText.text = "Add Morph";
        }

        private void ToggleAnimatedMorph()
        {
            try
            {
                var morphName = _addMorphListJSON.val;
                var morphJSONRef = _morphsList.GetAnimatableMorphs().FirstOrDefault(m => m.name == morphName);
                if (morphJSONRef == null)
                {
                    SuperController.LogError($"Morph {morphName} in atom {_plugin.ContainingAtom.uid} does not exist");
                    return;
                }
                if (_animation.Current.Targets.Any(c => c.Storable == morphJSONRef))
                {
                    _animation.Current.Targets.Remove(_animation.Current.Targets.First(c => c.Storable == morphJSONRef));
                }
                else
                {
                    var target = new JSONStorableFloatAnimationTarget(morphJSONRef, _animation.AnimationLength);
                    target.SetKeyframe(0, morphJSONRef.val);
                    _animation.Current.Targets.Add(target);
                }
                RefreshMorphsListUI();
                AnimationUpdated();
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.MorphsPlugin.ToggleAnimatedMorph: " + exc);
            }
        }

        private void UpdateMorph(JSONStorableFloat morphJSONRef, float val)
        {
            morphJSONRef.val = val;
            // TODO: This should be done by the controller (updating the animation resets the time)
            var target = _animation.Current.Targets.FirstOrDefault(m => m.Name == morphJSONRef.name);
            if (target == null)
            {
                SuperController.LogError($"Morph {morphJSONRef.name} was not registed");
                return;
            }
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
            RefreshMorphsListUI();
        }

        protected override void AnimationUpdatedCustom()
        {
        }

        protected override void ContextUpdatedCustom()
        {
            if (_morphJSONRefs != null)
            {
                foreach (var morphJSONRef in _morphJSONRefs)
                    morphJSONRef.Local.valNoCallback = morphJSONRef.Original.val;
            }
        }

        #endregion
    }
}
