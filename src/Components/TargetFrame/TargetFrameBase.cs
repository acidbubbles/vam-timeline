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

            var image = go.AddComponent<Image>();
            image.color = Style.BackgroundColor;
            image.raycastTarget = false;

            return go;
        }

        public void Bind(IAtomPlugin plugin, AtomAnimationClip clip, T target)
        {
            Plugin = plugin;
            Clip = clip;
            Target = target;
        }

        public void SetTime(float time)
        {
            var on = Target.GetLeadCurve().KeyframeBinarySearch(time);
        }

        public abstract void ToggleKeyframe(bool enable);
    }
}
