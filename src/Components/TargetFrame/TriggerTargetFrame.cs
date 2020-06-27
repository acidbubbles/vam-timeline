namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class ActionParamTargetFrame : TargetFrameBase<ActionParamAnimationTarget>
    {
        public ActionParamTargetFrame()
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
                ValueText.text = Target.Keyframes.Contains(time.ToMilliseconds()) ? "Trigger" : "-";
            }
        }

        public override void ToggleKeyframe(bool enable)
        {
            if (Plugin.Animation.IsPlaying()) return;
            var time = Plugin.Animation.Time.Snap();
            if (time.IsSameFrame(0f) || time.IsSameFrame(Clip.AnimationLength))
            {
                if (!enable)
                    SetToggle(true);
                return;
            }
            if (enable)
            {
                Target.SetKeyframe(time, true);
            }
            else
            {
                Target.DeleteFrame(time);
            }
        }
    }
}
