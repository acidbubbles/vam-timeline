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

        protected override void CreateExpandPanel(RectTransform container)
        {
            var group = container.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.spacing = 4f;
            group.padding = new RectOffset(8, 8, 8, 8);
            group.childAlignment = TextAnchor.MiddleCenter;

            CreateExpandButton(group.transform, "Select", () => SuperController.singleton.SelectController(target.controller));
        }

        public override void SetTime(float time, bool stopped)
        {
            base.SetTime(time, stopped);

            if (stopped)
            {
                var pos = target.controller.transform.position;
                valueText.text = $"x: {pos.x:0.000} y: {pos.y:0.000} z: {pos.z:0.000}";
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
                if (plugin.autoKeyframeAllControllersJSON.val)
                {
                    foreach (var target1 in clip.targetControllers)
                        SetControllerKeyframe(time, target1);
                }
                else
                {
                    SetControllerKeyframe(time, target);
                }
            }
            else
            {
                if (plugin.autoKeyframeAllControllersJSON.val)
                {
                    foreach (var target1 in clip.targetControllers)
                        target1.DeleteFrame(time);
                }
                else
                {
                    target.DeleteFrame(time);
                }
            }
        }

        private void SetControllerKeyframe(float time, FreeControllerAnimationTarget target)
        {
            plugin.animation.SetKeyframeToCurrentTransform(target, time);
            if (target.settings[time.ToMilliseconds()]?.curveType == CurveTypeValues.CopyPrevious)
                clip.ChangeCurve(time, CurveTypeValues.Smooth);
        }
    }
}
