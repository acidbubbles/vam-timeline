using System;
using UnityEngine;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class TriggersTargetFrame : TargetFrameBase<TriggersAnimationTarget>
    {
        public TriggersTargetFrame()
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
                valueText.text = target.keyframes.Contains(time.ToMilliseconds()) ? "Trigger" : "-";
            }
        }

        public override void ToggleKeyframe(bool enable)
        {
            if (plugin.animation.isPlaying) return;
            var time = plugin.animation.clipTime.Snap();
            if (time.IsSameFrame(0f) || time.IsSameFrame(clip.animationLength))
            {
                if (!enable)
                    SetToggle(true);
                return;
            }
            if (enable)
            {
                target.SetKeyframe(time, true);
            }
            else
            {
                target.DeleteFrame(time);
            }
        }

        protected override void CreateExpandPanel(RectTransform container)
        {
            // TODO: Make the expand panel contain the triggers UI
            throw new NotImplementedException();
        }
    }
}
