using System.Threading;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public abstract class TargetFrameBase<T> : MonoBehaviour, ITargetFrame
        where T : IAnimationTargetWithCurves
    {
        protected readonly StyleBase style = new StyleBase();
        protected IAtomPlugin plugin;
        protected AtomAnimationClip clip;
        protected T target;
        protected UIDynamicToggle toggle;
        protected Text valueText;
        private GameObject _expandButton;
        private int _ignoreNextToggleEvent;
        private RectTransform _expanded;

        public UIDynamic Container => gameObject.GetComponent<UIDynamic>();

        public TargetFrameBase()
        {
        }

        public void Bind(IAtomPlugin plugin, AtomAnimationClip clip, T target)
        {
            this.plugin = plugin;
            this.clip = clip;
            this.target = target;

            CreateToggle(plugin);
            toggle.label = target.name;

            CreateCustom();

            valueText = CreateValueText();

            _expandButton = CreateExpandButton();
            var expandListener = _expandButton.AddComponent<Clickable>();
            expandListener.onClick.AddListener(pointerEvent => ToggleExpanded());

            this.plugin.animation.onTimeChanged.AddListener(this.OnTimeChanged);
            OnTimeChanged(this.plugin.animation.time);

            target.onAnimationKeyframesModified.AddListener(OnAnimationKeyframesModified);

            OnAnimationKeyframesModified();
        }

        private void ToggleExpanded()
        {
            var ui = GetComponent<UIDynamic>();
            var expandSize = 70f;
            if (_expanded == null)
            {
                ui.height += expandSize;
                _expanded = CreateExpandContainer();
                CreateExpandPanel(_expanded);
                _expandButton.GetComponent<Text>().text = "\u02C5";
            }
            else
            {
                ui.height -= expandSize;
                Destroy(_expanded.gameObject);
                _expanded = null;
                _expandButton.GetComponent<Text>().text = "\u02C3";
            }
        }

        private RectTransform CreateExpandContainer()
        {
            var go = new GameObject();
            go.transform.SetParent(transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.StretchParent();
            rect.offsetMin = new Vector2(8f, 8f);
            rect.offsetMax = new Vector2(-8f, -60f);

            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            image.color = new Color(0.75f, 0.70f, 0.82f);

            return rect;
        }

        private void OnAnimationKeyframesModified()
        {
            SetTime(plugin.animation.time, true);
        }

        private void CreateToggle(IAtomPlugin plugin)
        {
            if (toggle != null) return;

            var ui = Instantiate(plugin.manager.configurableTogglePrefab.transform);
            ui.SetParent(transform, false);

            toggle = ui.GetComponent<UIDynamicToggle>();

            var rect = ui.gameObject.GetComponent<RectTransform>();
            rect.StretchParent();

            toggle.backgroundImage.raycastTarget = false;

            var label = toggle.labelText;
            label.fontSize = 26;
            label.alignment = TextAnchor.UpperLeft;
            label.raycastTarget = false;
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.offsetMin += new Vector2(-3f, 0f);
            labelRect.offsetMax += new Vector2(0f, -5f);

            var checkbox = toggle.toggle.image.gameObject;
            var toggleRect = checkbox.GetComponent<RectTransform>();
            toggleRect.anchorMin = new Vector2(0, 1);
            toggleRect.anchorMax = new Vector2(0, 1);
            toggleRect.anchoredPosition = new Vector2(29f, -30f);

            ui.gameObject.SetActive(true);

            toggle.toggle.onValueChanged.AddListener(ToggleKeyframeInternal);
        }

        private void ToggleKeyframeInternal(bool on)
        {
            if (_ignoreNextToggleEvent > 0) return;
            ToggleKeyframe(on);
        }

        protected Text CreateValueText()
        {
            var go = new GameObject();
            go.transform.SetParent(transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(300f, 40f);
            rect.anchoredPosition = new Vector2(-150f - 48f, 20f - 55f);

            var text = go.AddComponent<Text>();
            text.alignment = TextAnchor.LowerRight;
            text.fontSize = 20;
            text.font = style.Font;
            text.color = style.FontColor;
            text.raycastTarget = false;

            return text;
        }

        private GameObject CreateExpandButton()
        {
            var go = new GameObject();
            go.transform.SetParent(transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(30f, 52f);
            rect.anchoredPosition += new Vector2(-22f, -30f);

            var text = go.AddComponent<Text>();
            text.font = style.Font;
            text.color = new Color(0.6f, 0.6f, 0.7f);
            text.text = "\u02C3";
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 40;

            return go;
        }

        public void Update()
        {
            if (UIPerformance.ShouldSkip()) return;
            if (!plugin.animation.IsPlaying()) return;

            SetTime(plugin.animation.time, false);
        }

        private void OnTimeChanged(float time)
        {
            SetTime(time, true);
        }

        public virtual void SetTime(float time, bool stopped)
        {
            if (stopped)
            {
                toggle.toggle.interactable = time > 0 && time < clip.animationLength;
                SetToggle(target.GetLeadCurve().KeyframeBinarySearch(time) != -1);
            }
            else
            {
                toggle.toggle.interactable = false;
                if (valueText.text != "")
                    valueText.text = "";
            }
        }

        protected void SetToggle(bool on)
        {
            if (toggle.toggle.isOn == on) return;
            Interlocked.Increment(ref _ignoreNextToggleEvent);
            try
            {
                toggle.toggle.isOn = on;
            }
            finally
            {
                Interlocked.Decrement(ref _ignoreNextToggleEvent);
            }
        }

        protected UIDynamicButton CreateExpandButton(Transform parent, string label, UnityAction callback)
        {
            var btn = Instantiate(plugin.manager.configurableButtonPrefab).GetComponent<UIDynamicButton>();
            btn.gameObject.transform.SetParent(parent, false);

            btn.label = label;
            btn.button.onClick.AddListener(callback);
            btn.buttonText.fontSize = 24;

            var layout = btn.GetComponent<LayoutElement>();
            layout.minHeight = 20f;
            layout.preferredHeight = 20f;

            return btn;
        }

        protected abstract void CreateCustom();
        protected abstract void CreateExpandPanel(RectTransform container);
        public abstract void ToggleKeyframe(bool enable);

        public void OnDestroy()
        {
            plugin.animation.onTimeChanged.RemoveListener(OnTimeChanged);
            target.onAnimationKeyframesModified.RemoveListener(OnAnimationKeyframesModified);
        }
    }
}
