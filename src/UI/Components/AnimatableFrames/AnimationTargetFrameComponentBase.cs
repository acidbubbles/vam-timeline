using System.Threading;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace VamTimeline
{
    public abstract class AnimationTargetFrameComponentBase<T> : MonoBehaviour, IAnimationTargetFrameComponent
        where T : IAtomAnimationTarget
    {
        protected virtual float expandSize => 70f;
        protected abstract bool enableValueText { get; }
        protected abstract bool enableLabel { get; }
        protected readonly StyleBase style = new StyleBase();
        protected IAtomPlugin plugin;
        protected AtomAnimationClip clip;
        protected T target;
        protected UIDynamicToggle toggle;
        protected Text valueText;
        protected bool expanded => _expanded != null;
        private GameObject _expandButton;
        private int _ignoreNextToggleEvent;
        private RectTransform _expanded;

        public virtual void Bind(IAtomPlugin plugin, AtomAnimationClip clip, T target)
        {
            this.plugin = plugin;
            this.clip = clip;
            this.target = target;

            CreateToggle(plugin);
            toggle.label = enableLabel ? Crop(target.GetFullName()) : "";

            CreateCustom();

            if (enableValueText)
            {
                valueText = CreateValueText();
            }

            _expandButton = CreateExpandButton();
            var expandListener = _expandButton.AddComponent<Clickable>();
            expandListener.onClick.AddListener(pointerEvent => ToggleExpanded());

            this.plugin.animationEditContext.onTimeChanged.AddListener(OnTimeChanged);
            OnTimeChanged(this.plugin.animationEditContext.timeArgs);

            target.onAnimationKeyframesRebuilt.AddListener(OnAnimationKeyframesRebuilt);

            OnAnimationKeyframesRebuilt();
        }

        private static string Crop(string value)
        {
            if (value.Length > 30)
                return value.Substring(0, 10) + "..." + value.Substring(value.Length - 20);
            return value;
        }

        public void ToggleExpanded()
        {
            var ui = GetComponent<UIDynamic>();
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

        private void OnAnimationKeyframesRebuilt()
        {
            Invoke(nameof(OnAnimationKeyframesRebuiltLate), 0);
        }

        private void OnAnimationKeyframesRebuiltLate()
        {
            SetTime(plugin.animationEditContext.clipTime, !plugin.animation.isPlaying);
        }

        private void CreateToggle(IAtomPlugin plugin)
        {
            if (toggle != null) return;

            var ui = Instantiate(plugin.manager.configurableTogglePrefab.transform, transform, false);

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

            toggle.toggle.onValueChanged.AddListener(ToggleKeyframe);
        }

        public void ToggleKeyframe(bool on)
        {
            if (_ignoreNextToggleEvent > 0) return;
            if (!plugin.animationEditContext.CanEdit()) return;
            var time = plugin.animationEditContext.clipTime.Snap();
            var mustBeOn = time.IsSameFrame(0f) || time.IsSameFrame(clip.animationLength);
            if (mustBeOn && !on)
                SetToggle(true);
            ToggleKeyframeImpl(time, mustBeOn || on, mustBeOn);
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
            if (UIPerformance.ShouldSkip(UIPerformance.HighFrequency)) return;
            if (!plugin.animation.isPlaying) return;

            SetTime(plugin.animationEditContext.clipTime, false);
        }

        private void OnTimeChanged(AtomAnimationEditContext.TimeChangedEventArgs args)
        {
            SetTime(args.currentClipTime, true);
        }

        public virtual void SetTime(float time, bool stopped)
        {
            if (stopped)
            {
                toggle.toggle.interactable = !(clip.loop && !clip.loopPreserveLastFrame) || time < clip.animationLength;
                SetToggle(target.HasKeyframe(time.Snap()));
            }
            else
            {
                toggle.toggle.interactable = false;
                if (enableValueText)
                {
                    if (valueText.text != "")
                        valueText.text = "";
                }
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

        protected UIDynamicToggle CreateExpandToggle(Transform parent, JSONStorableBool jsb)
        {
            var ui = Instantiate(plugin.manager.configurableTogglePrefab, transform, false);
            ui.transform.SetParent(parent, false);

            var uiToggle = ui.GetComponent<UIDynamicToggle>();
            jsb.toggle = uiToggle.toggle;

            var label = uiToggle.labelText;
            label.fontSize = 24;
            label.alignment = TextAnchor.MiddleLeft;
            label.raycastTarget = false;
            label.text = jsb.name;

            var layout = uiToggle.GetComponent<LayoutElement>();
            layout.minHeight = 52f;
            layout.preferredHeight = 52f;

            return uiToggle;
        }

        protected UIDynamicTextField CreateExpandTextInput(Transform parent, JSONStorableString jss)
        {
            var ui = VamPrefabFactory.CreateTextInput(jss, plugin.manager.configurableTextFieldPrefab, parent);
            ui.transform.SetParent(parent, false);

            var layout = ui.GetComponent<LayoutElement>();
            layout.minHeight = 52f;
            layout.preferredHeight = 52f;

            return ui;
        }

        protected abstract void CreateCustom();
        protected abstract void CreateExpandPanel(RectTransform container);
        protected abstract void ToggleKeyframeImpl(float time, bool on, bool mustBeOn);

        public virtual void OnDestroy()
        {
            plugin.animationEditContext.onTimeChanged.RemoveListener(OnTimeChanged);
            target.onAnimationKeyframesRebuilt.RemoveListener(OnAnimationKeyframesRebuilt);
        }
    }
}
