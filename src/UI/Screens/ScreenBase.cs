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
    public abstract class ScreenBase : MonoBehaviour
    {
        public class ScreenChangeRequestedEvent : UnityEvent<string> { }

        private static readonly Font _font = (Font)Resources.GetBuiltinResource(typeof(Font), "Arial.ttf");

        public ScreenChangeRequestedEvent onScreenChangeRequested = new ScreenChangeRequestedEvent();
        public abstract string screenId { get; }

        protected AtomAnimation animation => plugin.animation;

        protected IAtomPlugin plugin;
        protected VamPrefabFactory prefabFactory;
        protected AtomAnimationClip current;
        protected bool _disposing;

        protected ScreenBase()
        {
        }

        public virtual void Init(IAtomPlugin plugin)
        {
            this.plugin = plugin;
            prefabFactory = gameObject.AddComponent<VamPrefabFactory>();
            prefabFactory.plugin = plugin;
            plugin.animation.onCurrentAnimationChanged.AddListener(OnCurrentAnimationChanged);
            current = plugin.animation?.current;
        }

        protected virtual void OnCurrentAnimationChanged(AtomAnimation.CurrentAnimationChangedEventArgs args)
        {
            current = plugin.animation?.current;
        }

        protected Text CreateHeader(string val)
        {
            var layerUI = prefabFactory.CreateSpacer();
            layerUI.height = 40f;

            var text = layerUI.gameObject.AddComponent<Text>();
            text.text = val;
            text.font = _font;
            text.fontSize = 28;
            text.color = Color.black;

            return text;
        }

        protected UIDynamicButton CreateChangeScreenButton(string label, string screenName)
        {
            var ui = prefabFactory.CreateButton(label);
            ui.button.onClick.AddListener(() => onScreenChangeRequested.Invoke(screenName));
            return ui;
        }

        public virtual void OnDestroy()
        {
            _disposing = true;
            onScreenChangeRequested.RemoveAllListeners();
            plugin.animation.onCurrentAnimationChanged.RemoveListener(OnCurrentAnimationChanged);
        }
    }
}

