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

        public bool interactable { get; set; }
        public UIDynamic Container => gameObject.GetComponent<UIDynamic>();

        public TargetFrameBase()
        {
            gameObject.AddComponent<Canvas>();
            gameObject.AddComponent<GraphicRaycaster>();

            CreateBackground();
        }

        private GameObject CreateBackground()
        {
            var go = new GameObject();
            go.transform.SetParent(transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.StretchParent();

            return go;
        }

        public void Bind(IAtomPlugin plugin, AtomAnimationClip clip, T target)
        {
            Plugin = plugin;
            Clip = clip;
            Target = target;

            var ui = Instantiate(plugin.Manager.configurableTogglePrefab.transform);
            ui.SetParent(transform, false);
            Toggle = ui.GetComponent<UIDynamicToggle>();
            Toggle.label = target.Name;
            var rect = ui.gameObject.GetComponent<RectTransform>();
            rect.StretchParent();
            ui.gameObject.SetActive(true);

            Toggle.toggle.onValueChanged.AddListener(ToggleKeyframe);
        }

        public void SetTime(float time, bool stopped)
        {
            if (stopped)
            {
                Toggle.toggle.interactable = true;
                Toggle.toggle.isOn = Target.GetLeadCurve().KeyframeBinarySearch(time) != -1;
            }
            else
            {
                Toggle.toggle.interactable = false;
            }
        }

        public abstract void ToggleKeyframe(bool enable);
    }
}
