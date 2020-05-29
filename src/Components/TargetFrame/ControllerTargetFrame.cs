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
                var pos = Target.Controller.transform.position;
                ValueText.text = $"{pos.x:0.000}, {pos.y:0.000}, {pos.z:0.000}";
            }
        }

        public override void ToggleKeyframe(bool enable)
        {
            if (Plugin.Animation.IsPlaying()) return;
            var time = Plugin.Animation.Time.Snap();
            if (time.IsSameFrame(0f) || time.IsSameFrame(Clip.AnimationLength))
            {
                if (!enable)
                    Toggle.toggle.isOn = true;
                return;
            }
            if (enable)
            {
                if (Plugin.AutoKeyframeAllControllersJSON.val)
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
                if (Plugin.AutoKeyframeAllControllersJSON.val)
                {
                    foreach (var target1 in Clip.TargetControllers)
                        target1.DeleteFrame(time);
                }
                else
                {
                    Target.DeleteFrame(time);
                }
            }
            Plugin.Animation.RebuildAnimation();
            Plugin.AnimationModified();
        }

        private void SetControllerKeyframe(float time, FreeControllerAnimationTarget target)
        {
            Plugin.Animation.SetKeyframeToCurrentTransform(target, time);
            if (target.Settings[time.ToMilliseconds()]?.CurveType == CurveTypeValues.CopyPrevious)
                Clip.ChangeCurve(time, CurveTypeValues.Smooth);
        }
    }
}
