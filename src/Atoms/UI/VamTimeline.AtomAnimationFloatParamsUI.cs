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
                foreach (var jsfJSONRef in _jsfJSONRefs)
                    jsfJSONRef.Proxy.valNoCallback = jsfJSONRef.SourceFloatParam.val;
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
            // TODO: This is expensive, though rarely occuring
            _jsfJSONRefs = new List<FloatParamJSONRef>();
            SuperController.LogMessage("Regenerate");
            foreach (var target in Plugin.Animation.Current.TargetFloatParams)
            {
                var jsfJSONRef = target.FloatParam;
                var jsfJSONProxy = new JSONStorableFloat($"{target.Storable.name}/{jsfJSONRef.name}", jsfJSONRef.defaultVal, (float val) => UpdateFloatParam(target, jsfJSONRef, val), jsfJSONRef.min, jsfJSONRef.max, jsfJSONRef.constrained, true)
                {
                    valNoCallback = jsfJSONRef.val
                };
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
            // TODO: Breaks sliders
            // Plugin.AnimationModified();
        }
    }
}

