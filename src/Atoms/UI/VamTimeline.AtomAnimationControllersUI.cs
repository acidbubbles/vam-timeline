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
    public class AtomAnimationControllersUI : AtomAnimationBaseUI
    {
        public const string ScreenName = "Controllers";
        public override string Name => ScreenName;

        private class TargetRef
        {
            internal JSONStorableBool KeyframeJSON;
            internal FreeControllerAnimationTarget Target;
        }

        private List<TargetRef> _targets;


        public AtomAnimationControllersUI(IAtomPlugin plugin)
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

            InitCurvesUI();

            InitClipboardUI(false);

            // Right side

            InitDisplayUI(true);
        }

        private void InitCurvesUI()
        {
            var changeCurveUI = Plugin.CreatePopup(Plugin.ChangeCurveJSON, false);
            changeCurveUI.popupPanelHeight = 800f;
            _linkedStorables.Add(Plugin.ChangeCurveJSON);

            var smoothAllFramesUI = Plugin.CreateButton("Smooth All Frames", false);
            smoothAllFramesUI.button.onClick.AddListener(() => Plugin.SmoothAllFramesJSON.actionCallback());
            _components.Add(smoothAllFramesUI);
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
                    targetRef.KeyframeJSON.valNoCallback = targetRef.Target.X.keys.Any(k => k.time == time);
                }
            }
        }

        private void RefreshTargetsList()
        {
            if (Plugin.Animation == null) return;
            if (_targets != null && Enumerable.SequenceEqual(Plugin.Animation.Current.TargetControllers, _targets.Select(t => t.Target)))
                return;
            RemoveTargets();
            var time = Plugin.Animation.Time;
            _targets = new List<TargetRef>();
            foreach (var target in Plugin.Animation.Current.TargetControllers)
            {
                var keyframeJSON = new JSONStorableBool($"{target.Name} Keyframe", target.X.keys.Any(k => k.time == time), (bool val) => ToggleKeyframe(target, val));
                var keyframeUI = Plugin.CreateToggle(keyframeJSON, true);
                _targets.Add(new TargetRef
                {
                    Target = target,
                    KeyframeJSON = keyframeJSON
                });
            }
        }

        private void ToggleKeyframe(FreeControllerAnimationTarget target, bool val)
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
                target.SetKeyframeToCurrentTransform(time);
            }
            else
            {
                target.DeleteFrame(time);
            }
            Plugin.Animation.RebuildAnimation();
            Plugin.AnimationModified();
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
            }
        }
    }
}

