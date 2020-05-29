namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class FloatParamTargetFrame : TargetFrameBase<FloatParamAnimationTarget>
    {
        public FloatParamTargetFrame()
            : base()
        {
        }

        public override void SetTime(float time, bool stopped)
        {
            base.SetTime(time, stopped);

            if (stopped)
            {
                ValueText.text = Target.FloatParam.val.ToString("0.00");
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
                Plugin.Animation.SetKeyframe(Target, time, Target.FloatParam.val);
            }
            else
            {
                Target.DeleteFrame(time);
            }
            Plugin.Animation.RebuildAnimation();
            Plugin.AnimationModified();
        }
    }
}
