using System;
using System.Linq;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class AtomAnimationAdvancedUI : AtomAnimationBaseUI
    {
        public const string ScreenName = "Advanced";
        public override string Name => ScreenName;

        public AtomAnimationAdvancedUI(IAtomPlugin plugin)
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

            var keyframeCurrentPoseUI = Plugin.CreateButton("Keyframe Current Pose", true);
            keyframeCurrentPoseUI.button.onClick.AddListener(() => KeyframeCurrentPose());
            _components.Add(keyframeCurrentPoseUI);
        }

        private void KeyframeCurrentPose()
        {
            var time = Plugin.Animation.Time;
            foreach (var fc in Plugin.ContainingAtom.freeControllers)
            {
                if (!fc.name.EndsWith("Control")) continue;
                if (fc.currentPositionState != FreeControllerV3.PositionState.On) continue;
                if (fc.currentRotationState != FreeControllerV3.RotationState.On) continue;

                var target = Plugin.Animation.Current.TargetControllers.FirstOrDefault(tc => tc.Controller == fc);
                if (target == null)
                    target = Plugin.Animation.Add(fc);
                target.SetKeyframeToCurrentTransform(time);
            }
        }
    }
}

