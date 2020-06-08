using UnityEngine;
using UnityEngine.UI;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class ControllerTargetFrame : TargetFrameBase<FreeControllerAnimationTarget>
    {
        public ControllerTargetFrame()
            : base()
        {
        }

        protected override void CreateCustom()
        {
        }

        public override void SetTime(float time, bool stopped)
        {
            base.SetTime(time, stopped);

            if (stopped)
            {
                var pos = Target.controller.transform.position;
                ValueText.text = $"x: {pos.x:0.000} y: {pos.y:0.000} z: {pos.z:0.000}";
            }
        }

        public override void ToggleKeyframe(bool enable)
        {
            if (Plugin.animation.IsPlaying()) return;
            var time = Plugin.animation.Time.Snap();
            if (time.IsSameFrame(0f) || time.IsSameFrame(Clip.animationLength))
            {
                if (!enable)
                    SetToggle(true);
                return;
            }
            if (enable)
            {
                if (Plugin.autoKeyframeAllControllersJSON.val)
                {
                    foreach (var target1 in Clip.TargetControllers)
                        SetControllerKeyframe(time, target1);
                }
                else
                {
                    SetControllerKeyframe(time, Target);
                }
            }
            else
            {
                if (Plugin.autoKeyframeAllControllersJSON.val)
                {
                    foreach (var target1 in Clip.TargetControllers)
                        target1.DeleteFrame(time);
                }
                else
                {
                    Target.DeleteFrame(time);
                }
            }
        }

        private void SetControllerKeyframe(float time, FreeControllerAnimationTarget target)
        {
            Plugin.animation.SetKeyframeToCurrentTransform(target, time);
            if (target.settings[time.ToMilliseconds()]?.curveType == CurveTypeValues.CopyPrevious)
                Clip.ChangeCurve(time, CurveTypeValues.Smooth);
        }
    }
}
