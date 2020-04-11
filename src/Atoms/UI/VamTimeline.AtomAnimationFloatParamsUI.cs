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

        private class TargetRef
        {
            public JSONStorableFloat FloatParamProxyJSON;
            public JSONStorableBool KeyframeJSON;
            public FloatParamAnimationTarget Target;
            public UIDynamicSlider SliderUI;
            public UIDynamicToggle KeyframeUI;
        }

        private readonly List<TargetRef> _targets = new List<TargetRef>();


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

            InitDisplayUI(false);
        }

        public override void UpdatePlaying()
        {
            base.UpdatePlaying();
            UpdateValues();
        }

        public override void AnimationFrameUpdated()
        {
            UpdateValues();
            base.AnimationFrameUpdated();
        }

        public override void AnimationModified()
        {
            base.AnimationModified();
            RefreshTargetsList();
        }

        private void UpdateValues()
        {
            var time = Plugin.Animation.Time;
            foreach (var targetRef in _targets)
            {
                targetRef.FloatParamProxyJSON.valNoCallback = targetRef.Target.FloatParam.val;
                targetRef.KeyframeJSON.valNoCallback = targetRef.Target.Value.KeyframeBinarySearch(time) != -1;
            }
        }

        private void RefreshTargetsList()
        {
            if (Plugin.Animation == null) return;
            if (Enumerable.SequenceEqual(Plugin.Animation.Current.TargetFloatParams, _targets.Select(t => t.Target)))
                return;
            RemoveTargets();
            var time = Plugin.Animation.Time;
            foreach (var target in Plugin.Animation.Current.TargetFloatParams)
            {
                var sourceFloatParamJSON = target.FloatParam;
                var keyframeJSON = new JSONStorableBool($"{target.Storable.name}/{sourceFloatParamJSON.name} Keyframe", target.Value.KeyframeBinarySearch(time) != -1, (bool val) => ToggleKeyframe(target, val))
                {
                    isStorable= false
                };
                var keyframeUI = Plugin.CreateToggle(keyframeJSON, true);
                var jsfJSONProxy = new JSONStorableFloat($"{target.Storable.name}/{sourceFloatParamJSON.name}", sourceFloatParamJSON.defaultVal, (float val) => SetFloatParamValue(target, val), sourceFloatParamJSON.min, sourceFloatParamJSON.max, sourceFloatParamJSON.constrained, true)
                {
                    isStorable = false,
                    valNoCallback = sourceFloatParamJSON.val
                };
                var sliderUI = Plugin.CreateSlider(jsfJSONProxy, true);
                _targets.Add(new TargetRef
                {
                    Target = target,
                    FloatParamProxyJSON = jsfJSONProxy,
                    SliderUI = sliderUI,
                    KeyframeJSON = keyframeJSON,
                    KeyframeUI = keyframeUI
                });
            }
        }

        private void ToggleKeyframe(FloatParamAnimationTarget target, bool val)
        {
            if (Plugin.Animation.IsPlaying()) return;
            var time = Plugin.Animation.Time.Snap();
            if (time.IsSameFrame(0f) || time.IsSameFrame(Plugin.Animation.Current.AnimationLength))
            {
                _targets.First(t => t.Target == target).KeyframeJSON.valNoCallback = true;
                return;
            }
            if (val)
            {
                Plugin.Animation.SetKeyframe(target, time, target.FloatParam.val);
            }
            else
            {
                target.DeleteFrame(time);
            }
            Plugin.Animation.RebuildAnimation();
            Plugin.AnimationModified();
        }

        private void SetFloatParamValue(FloatParamAnimationTarget target, float val)
        {
            if (Plugin.Animation.IsPlaying()) return;
            target.FloatParam.val = val;
            var time = Plugin.Animation.Time;
            Plugin.Animation.SetKeyframe(target, time, val);
            Plugin.Animation.RebuildAnimation();
            var targetRef = _targets.FirstOrDefault(j => j.Target == target);
            targetRef.KeyframeJSON.valNoCallback = true;
        }

        public override void Dispose()
        {
            RemoveTargets();
            base.Dispose();
        }

        private void RemoveTargets()
        {
            foreach (var targetRef in _targets)
            {
                // TODO: Take care of keeping track of those separately
                Plugin.RemoveToggle(targetRef.KeyframeJSON);
                Plugin.RemoveToggle(targetRef.KeyframeUI);
                Plugin.RemoveSlider(targetRef.FloatParamProxyJSON);
                Plugin.RemoveSlider(targetRef.SliderUI);
            }
            _targets.Clear();
        }
    }
}

