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
    public class FloatParamTargetFrame : TargetFrameBase<FloatParamAnimationTarget>
    {
        private RectTransform _sliderFillRect;

        public FloatParamTargetFrame()
            : base()
        {
        }

        protected override void CreateCustom()
        {
            var slider = new GameObject();
            slider.transform.SetParent(transform, false);

            {
                var rect = slider.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.sizeDelta = new Vector2(-62f, -6f);
                rect.anchoredPosition += new Vector2(26f, 0f);

                var image = slider.AddComponent<Image>();
                image.color = new Color(0f, 0f, 0f, 0f);
                image.raycastTarget = true;
            }

            var sliderBackground = new GameObject();
            sliderBackground.transform.SetParent(slider.transform, false);

            {
                var rect = sliderBackground.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.sizeDelta = new Vector2(0f, 20f);
                rect.anchoredPosition += new Vector2(0f, 13f);

                var image = sliderBackground.AddComponent<GradientImage>();
                image.top = new Color(0.7f, 0.7f, 0.7f);
                image.bottom = new Color(0.8f, 0.8f, 0.8f);
                image.raycastTarget = false;
            }

            var sliderFill = new GameObject();
            sliderFill.transform.SetParent(sliderBackground.transform, false);

            {
                var rect = sliderFill.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.sizeDelta = Vector2.zero;
                _sliderFillRect = rect;

                var image = sliderFill.AddComponent<GradientImage>();
                image.top = new Color(1.0f, 1.0f, 1.0f);
                image.bottom = new Color(0.9f, 0.9f, 0.9f);
                image.raycastTarget = false;
            }

            var interactions = slider.AddComponent<SimpleSlider>();
            interactions.OnChange.AddListener((float val) =>
            {
                Target.FloatParam.val = Target.FloatParam.min + val * (Target.FloatParam.max - Target.FloatParam.min);
                Plugin.Animation.SetKeyframe(Target, Plugin.Animation.Time, Target.FloatParam.val);
                SetTime(Plugin.Animation.Time, true);
                ToggleKeyframe(true);
            });
        }

        public override void SetTime(float time, bool stopped)
        {
            base.SetTime(time, stopped);

            if (stopped)
            {
                ValueText.text = Target.FloatParam.val.ToString("0.00");
            }

            _sliderFillRect.anchorMax = new Vector2(Mathf.Clamp01((-Target.FloatParam.min + Target.FloatParam.val) / (Target.FloatParam.max - Target.FloatParam.min)), 1f);
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
                Plugin.Animation.SetKeyframe(Target, time, Target.FloatParam.val);
            }
            else
            {
                Target.DeleteFrame(time);
            }
        }
    }
}
