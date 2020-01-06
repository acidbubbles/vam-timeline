using System;
using System.Collections.Generic;
using System.Linq;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class AtomAnimationFloatParamsUI : AtomAnimationBaseUI
    {
        public const string ScreenName = "Params";
        public override string Name => ScreenName;

        private class FloatParamJSONRef
        {
            public JSONStorable Storable;
            public JSONStorableFloat SourceFloatParam;
            public JSONStorableFloat Proxy;
            public UIDynamicSlider Slider;
            internal JSONStorableBool KeyframeJSON;
            internal FloatParamAnimationTarget Target;
        }

        private List<FloatParamJSONRef> _jsfJSONRefs;


        public AtomAnimationFloatParamsUI(IAtomPlugin plugin)
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

            InitClipboardUI(false);

            // Right side

            InitDisplayUI(true);
        }

        public override void UpdatePlaying()
        {
            base.UpdatePlaying();
            UpdateFloatParamSliders();
        }

        public override void AnimationModified()
        {
            base.AnimationModified();
            RefreshFloatParamsListUI();
        }

        private void UpdateFloatParamSliders()
        {
            if (_jsfJSONRefs != null)
            {
                var time = Plugin.Animation.Time;
                foreach (var jsfJSONRef in _jsfJSONRefs)
                {
                    jsfJSONRef.Proxy.valNoCallback = jsfJSONRef.SourceFloatParam.val;
                    jsfJSONRef.KeyframeJSON.valNoCallback = jsfJSONRef.Target.Value.keys.Any(k => k.time == time);
                }
            }
        }

        public override void AnimationFrameUpdated()
        {
            UpdateFloatParamSliders();
            base.AnimationFrameUpdated();
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
            var time = Plugin.Animation.Time;
            // TODO: This is expensive, though rarely occuring
            _jsfJSONRefs = new List<FloatParamJSONRef>();
            foreach (var target in Plugin.Animation.Current.TargetFloatParams)
            {
                var jsfJSONRef = target.FloatParam;
                var keyframeJSON = new JSONStorableBool($"{target.Storable.name}/{jsfJSONRef.name} Keyframe", target.Value.keys.Any(k => k.time == time), (bool val) => ToggleKeyframe(target, val));
                var keyframeUI = Plugin.CreateToggle(keyframeJSON, true);
                var jsfJSONProxy = new JSONStorableFloat($"{target.Storable.name}/{jsfJSONRef.name}", jsfJSONRef.defaultVal, (float val) => UpdateFloatParam(target, jsfJSONRef, val), jsfJSONRef.min, jsfJSONRef.max, jsfJSONRef.constrained, true)
                {
                    valNoCallback = jsfJSONRef.val
                };
                var slider = Plugin.CreateSlider(jsfJSONProxy, true);
                _jsfJSONRefs.Add(new FloatParamJSONRef
                {
                    Target = target,
                    Storable = target.Storable,
                    SourceFloatParam = jsfJSONRef,
                    Proxy = jsfJSONProxy,
                    Slider = slider,
                    KeyframeJSON = keyframeJSON
                });
            }
        }

        private void ToggleKeyframe(FloatParamAnimationTarget target, bool val)
        {
            // TODO: This should be done by the controller (updating the animation resets the time)
            var time = Plugin.Animation.Time;
            if (val)
            {
                target.SetKeyframe(time, target.FloatParam.val);
            }
            else
            {
                var idx = Array.FindIndex(target.Value.keys, k => k.time == time);
                if (idx > -1)
                    target.Value.RemoveKey(idx);
            }
            Plugin.Animation.RebuildAnimation();
            Plugin.AnimationModified();
        }

        private void RemoveJsonRefs()
        {
            if (_jsfJSONRefs == null) return;
            foreach (var jsfJSONRef in _jsfJSONRefs)
            {
                // TODO: Take care of keeping track of those separately
                Plugin.RemoveToggle(jsfJSONRef.KeyframeJSON);
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
            var jsfJSONRef = _jsfJSONRefs.FirstOrDefault(j => j.SourceFloatParam == sourceFloatParam);
            jsfJSONRef.KeyframeJSON.valNoCallback = true;
            // TODO: Breaks sliders
            // Plugin.AnimationModified();
        }
    }
}

