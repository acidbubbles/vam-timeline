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

        public override void ToggleKeyframe(bool enable)
        {
            if (Plugin.Animation.IsPlaying()) return;
            var time = Plugin.Animation.Time.Snap();
            if (time.IsSameFrame(0f) || time.IsSameFrame(Clip.AnimationLength))
            {
                // TODO: Cancel
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
