using UnityEngine;
using UnityEngine.EventSystems;
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
            var sliderBackground = new GameObject();
            sliderBackground.transform.SetParent(transform, false);

            {
                var rect = sliderBackground.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.sizeDelta = new Vector2(-64f, 20f);
                rect.anchoredPosition += new Vector2(26f, 16f);

                var image = sliderBackground.AddComponent<Image>();
                image.color = new Color(0.7f, 0.7f, 0.7f);
            }

            var sliderFill = new GameObject();
            sliderFill.transform.SetParent(sliderBackground.transform, false);

            {
                var rect = sliderFill.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.sizeDelta = Vector2.zero;
                _sliderFillRect = rect;

                var image = sliderFill.AddComponent<Image>();
                image.color = Color.white;
            }

            var slider = sliderBackground.AddComponent<SimpleSlider>();
            slider.OnChange.AddListener((float val) =>
            {
                Target.FloatParam.val = Target.FloatParam.min + val * (Target.FloatParam.max - Target.FloatParam.min);
                SetTime(-1, true);
            });
        }

        public override void SetTime(float time, bool stopped)
        {
            base.SetTime(time, stopped);

            if (stopped)
            {
                ValueText.text = Target.FloatParam.val.ToString("0.00");
            }

            _sliderFillRect.anchorMax = new Vector2(Mathf.Clamp01((Target.FloatParam.min + Target.FloatParam.val) / (Target.FloatParam.max - Target.FloatParam.min)), 1f);
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

        public void OnPointerDown(PointerEventData eventData)
        {
            eventData.useDragThreshold = false;
            UpdateScrubberFromView(eventData);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            UpdateScrubberFromView(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            UpdateScrubberFromView(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            UpdateScrubberFromView(eventData);
        }

        private void UpdateScrubberFromView(PointerEventData eventData)
        {
            Vector2 localPosition;
            var rect = GetComponent<RectTransform>();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out localPosition))
                return;
            var ratio = Mathf.Clamp01((localPosition.x + rect.sizeDelta.x / 2f) / rect.sizeDelta.x);
        }
    }
}
