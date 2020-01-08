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

        private class TargetRef
        {
            public JSONStorableFloat FloatParamProxyJSON;
            internal JSONStorableBool KeyframeJSON;
            internal FloatParamAnimationTarget Target;
        }

        private List<TargetRef> _targets;


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
            if (_targets != null)
            {
                var time = Plugin.Animation.Time;
                foreach (var targetRef in _targets)
                {
                    targetRef.FloatParamProxyJSON.valNoCallback = targetRef.Target.FloatParam.val;
                    targetRef.KeyframeJSON.valNoCallback = targetRef.Target.Value.keys.Any(k => k.time == time);
                }
            }
        }

        private void RefreshTargetsList()
        {
            if (Plugin.Animation == null) return;
            if (_targets != null && Enumerable.SequenceEqual(Plugin.Animation.Current.TargetFloatParams, _targets.Select(t => t.Target)))
                return;
            RemoveTargets();
            var time = Plugin.Animation.Time;
            _targets = new List<TargetRef>();
            foreach (var target in Plugin.Animation.Current.TargetFloatParams)
            {
                var sourceFloatParamJSON = target.FloatParam;
                var keyframeJSON = new JSONStorableBool($"{target.Storable.name}/{sourceFloatParamJSON.name} Keyframe", target.Value.keys.Any(k => k.time == time), (bool val) => ToggleKeyframe(target, val));
                var keyframeUI = Plugin.CreateToggle(keyframeJSON, true);
                var jsfJSONProxy = new JSONStorableFloat($"{target.Storable.name}/{sourceFloatParamJSON.name}", sourceFloatParamJSON.defaultVal, (float val) => SetFloatParamValue(target, val), sourceFloatParamJSON.min, sourceFloatParamJSON.max, sourceFloatParamJSON.constrained, true)
                {
                    valNoCallback = sourceFloatParamJSON.val
                };
                var slider = Plugin.CreateSlider(jsfJSONProxy, true);
                _targets.Add(new TargetRef
                {
                    Target = target,
                    FloatParamProxyJSON = jsfJSONProxy,
                    KeyframeJSON = keyframeJSON
                });
            }
        }

        private void ToggleKeyframe(FloatParamAnimationTarget target, bool val)
        {
            // TODO: This should be done by the controller (updating the animation resets the time)
            var time = Plugin.Animation.Time;
            if (time == 0f)
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
            target.FloatParam.val = val;
            // TODO: This should be done by the controller (updating the animation resets the time)
            var time = Plugin.Animation.Time;
            Plugin.Animation.SetKeyframe(target, time, val);
            Plugin.Animation.RebuildAnimation();
            var targetRef = _targets.FirstOrDefault(j => j.Target == target);
            targetRef.KeyframeJSON.valNoCallback = true;
            // TODO: Breaks sliders
            // Plugin.AnimationModified();
        }

        public override void Remove()
        {
            RemoveTargets();
            base.Remove();
        }

        private void RemoveTargets()
        {
            if (_targets == null) return;
            foreach (var targetRef in _targets)
            {
                // TODO: Take care of keeping track of those separately
                Plugin.RemoveToggle(targetRef.KeyframeJSON);
                Plugin.RemoveSlider(targetRef.FloatParamProxyJSON);
            }
        }
    }
}

