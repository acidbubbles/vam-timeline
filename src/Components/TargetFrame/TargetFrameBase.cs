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
    public abstract class TargetFrameBase<T> : MonoBehaviour, ITargetFrame
        where T : IAnimationTargetWithCurves
    {
        protected readonly StyleBase Style = new StyleBase();
        protected IAtomPlugin Plugin;
        protected AtomAnimationClip Clip;
        protected T Target;
        protected UIDynamicToggle Toggle;
        protected Text ValueText;


        public bool interactable { get; set; }
        public UIDynamic Container => gameObject.GetComponent<UIDynamic>();

        public TargetFrameBase()
        {
        }

        public void Bind(IAtomPlugin plugin, AtomAnimationClip clip, T target)
        {
            Plugin = plugin;
            Clip = clip;
            Target = target;

            CreateToggle(plugin);
            Toggle.label = target.Name;

            CreateCustom();

            ValueText = CreateValueText();
        }

        private void CreateToggle(IAtomPlugin plugin)
        {
            if (Toggle != null) return;

            var ui = Instantiate(plugin.Manager.configurableTogglePrefab.transform);
            ui.SetParent(transform, false);

            Toggle = ui.GetComponent<UIDynamicToggle>();

            var rect = ui.gameObject.GetComponent<RectTransform>();
            rect.StretchParent();

            Toggle.backgroundImage.raycastTarget = false;

            var label = Toggle.labelText;
            label.fontSize = 26;
            label.alignment = TextAnchor.UpperLeft;
            label.raycastTarget = false;
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.offsetMin += new Vector2(-3f, 0f);
            labelRect.offsetMax += new Vector2(0f, -5f);

            var checkbox = Toggle.toggle.image.gameObject;
            var toggleRect = checkbox.GetComponent<RectTransform>();
            toggleRect.anchoredPosition += new Vector2(3f, 0f);

            ui.gameObject.SetActive(true);

            Toggle.toggle.onValueChanged.AddListener(ToggleKeyframe);
        }

        protected Text CreateValueText()
        {
            var go = new GameObject();
            go.transform.SetParent(transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1, 0);
            rect.anchorMax = new Vector2(1, 0);
            rect.sizeDelta = new Vector2(300f, 40f);
            rect.anchoredPosition = new Vector2(-150f - 5f, 20f + 3f);

            var text = go.AddComponent<Text>();
            text.alignment = TextAnchor.LowerRight;
            text.fontSize = 20;
            text.font = Style.Font;
            text.color = Style.FontColor;
            text.raycastTarget = false;

            return text;
        }

        public virtual void SetTime(float time, bool stopped)
        {
            if (stopped)
            {
                Toggle.toggle.interactable = true;
                Toggle.toggle.isOn = Target.GetLeadCurve().KeyframeBinarySearch(time) != -1;
            }
            else
            {
                if (Toggle.toggle.interactable)
                    Toggle.toggle.interactable = false;
                if (ValueText.text != "")
                    ValueText.text = "";
            }
        }

        protected abstract void CreateCustom();
        public abstract void ToggleKeyframe(bool enable);
    }
}
