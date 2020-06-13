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
            var slider = CreateSlider();
            var sliderBackground = CreateSliderBackground(slider);
            CreateSliderFill(sliderBackground);

            var interactions = slider.AddComponent<SimpleSlider>();
            interactions.OnChange.AddListener((float val) =>
            {
                SetValue(target.floatParam.min + val * (target.floatParam.max - target.floatParam.min));
            });
        }

        private GameObject CreateSlider()
        {
            var go = new GameObject();
            go.transform.SetParent(transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(-100f, -6f);
            rect.anchoredPosition += new Vector2(8f, 0f);

            var image = go.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = true;

            return go;
        }

        private static GameObject CreateSliderBackground(GameObject slider)
        {
            var go = new GameObject();
            go.transform.SetParent(slider.transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(0f, 18f);
            rect.anchoredPosition += new Vector2(0f, -40f);

            var image = go.AddComponent<GradientImage>();
            image.top = new Color(0.7f, 0.7f, 0.7f);
            image.bottom = new Color(0.8f, 0.8f, 0.8f);
            image.raycastTarget = false;

            return go;
        }

        private void CreateSliderFill(GameObject sliderBackground)
        {
            var go = new GameObject();
            go.transform.SetParent(sliderBackground.transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.sizeDelta = Vector2.zero;
            _sliderFillRect = rect;

            var image = go.AddComponent<GradientImage>();
            image.top = new Color(1.0f, 1.0f, 1.0f);
            image.bottom = new Color(0.9f, 0.9f, 0.9f);
            image.raycastTarget = false;
        }

        protected override void CreateExpandPanel(RectTransform container)
        {
            var group = container.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.spacing = 4f;
            group.padding = new RectOffset(8, 8, 8, 8);
            group.childAlignment = TextAnchor.MiddleCenter;

            var morph = (target.storable as DAZCharacterSelector)?.morphsControlUI?.GetMorphByUid(target.floatParam.name);

            CreateExpandButton(group.transform, "Default", () => SetValue(target.floatParam.defaultVal));

            if (morph == null)
            {
                CreateExpandButton(group.transform, "Range .1X", () =>
                {
                    target.floatParam.min *= 0.1f;
                    target.floatParam.max *= 0.1f;
                    SetTime(plugin.animation.time, true);
                }).button.interactable = !target.floatParam.constrained;

                CreateExpandButton(group.transform, "Range 10X", () =>
                {
                    target.floatParam.min *= 10f;
                    target.floatParam.max *= 10f;
                    SetTime(plugin.animation.time, true);
                }).button.interactable = !target.floatParam.constrained;
            }
            else
            {
                CreateExpandButton(group.transform, "Reset Range", () =>
                {
                    morph.ResetRange();
                    if (target.floatParam.val < target.floatParam.min) SetValue(target.floatParam.min);
                    if (target.floatParam.val > target.floatParam.max) SetValue(target.floatParam.max);
                    SetTime(plugin.animation.time, true);
                });

                CreateExpandButton(group.transform, "+ Range", () =>
                {
                    morph.IncreaseRange();
                    SetTime(plugin.animation.time, true);
                });
            }
        }

        private void SetValue(float val)
        {
            target.floatParam.val = val;
            plugin.animation.SetKeyframe(target, plugin.animation.time, target.floatParam.val);
            SetTime(plugin.animation.time, true);
            ToggleKeyframe(true);
        }

        public override void SetTime(float time, bool stopped)
        {
            base.SetTime(time, stopped);

            if (stopped)
            {
                valueText.text = target.floatParam.val.ToString("0.00");
            }

            _sliderFillRect.anchorMax = new Vector2(Mathf.Clamp01((-target.floatParam.min + target.floatParam.val) / (target.floatParam.max - target.floatParam.min)), 1f);
        }

        public override void ToggleKeyframe(bool enable)
        {
            if (plugin.animation.IsPlaying()) return;
            var time = plugin.animation.time.Snap();
            if (time.IsSameFrame(0f) || time.IsSameFrame(clip.animationLength))
            {
                if (!enable)
                    SetToggle(true);
                return;
            }
            if (enable)
            {
                plugin.animation.SetKeyframe(target, time, target.floatParam.val);
            }
            else
            {
                target.DeleteFrame(time);
            }
        }
    }
}
