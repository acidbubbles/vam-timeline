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
    public class AtomAnimationEditorUI : AtomAnimationBaseUI
    {
        public const string ScreenName = "Animation Editor";
        public override string Name => ScreenName;

        private class FloatParamJSONRef
        {
            public JSONStorable Storable;
            public JSONStorableFloat SourceFloatParam;
            public JSONStorableFloat Proxy;
            public UIDynamicSlider Slider;
        }

        private List<FloatParamJSONRef> _jsfJSONRefs;


        public AtomAnimationEditorUI(IAtomPlugin plugin)
            : base(plugin)
        {

        }
        public override void Init()
        {
            base.Init();

            // Left side

            InitAnimationSelectorUI(false);

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

            InitDisplayUI(true);

            RefreshFloatParamsListUI();
        }

        public override void UpdatePlaying()
        {
            UpdateFloatParamSliders();
        }

        private void UpdateFloatParamSliders()
        {
            if (_jsfJSONRefs != null)
            {
                foreach (var jsfJSONRef in _jsfJSONRefs)
                    jsfJSONRef.Proxy.valNoCallback = jsfJSONRef.SourceFloatParam.val;
            }
        }

        public override void AnimationFrameUpdated()
        {
            UpdateFloatParamSliders();
        }

        public override void Remove()
        {
            RemoveJsonRefs();
            base.Remove();
        }

        private void RefreshFloatParamsListUI()
        {
            RemoveJsonRefs();
            if (Plugin.Animation == null) return;
            // TODO: This is expensive, though rarely occuring
            _jsfJSONRefs = new List<FloatParamJSONRef>();
            foreach (var target in Plugin.Animation.Current.TargetFloatParams)
            {
                var jsfJSONRef = target.FloatParam;
                var jsfJSONProxy = new JSONStorableFloat($"{target.Storable.name}/{jsfJSONRef.name}", jsfJSONRef.defaultVal, (float val) => UpdateFloatParam(target, jsfJSONRef, val), jsfJSONRef.min, jsfJSONRef.max, jsfJSONRef.constrained, true);
                var slider = Plugin.CreateSlider(jsfJSONProxy, true);
                _jsfJSONRefs.Add(new FloatParamJSONRef
                {
                    Storable = target.Storable,
                    SourceFloatParam = jsfJSONRef,
                    Proxy = jsfJSONProxy,
                    Slider = slider
                });
            }
        }

        private void RemoveJsonRefs()
        {
            if (_jsfJSONRefs == null) return;
            foreach (var jsfJSONRef in _jsfJSONRefs)
            {
                // TODO: Take care of keeping track of those separately
                Plugin.RemoveSlider(jsfJSONRef.Proxy);
            }
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
    }
}

