using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace VamTimeline
{
    public abstract class ScreenBase : MonoBehaviour
    {
        public class ScreenChangeRequestEventArgs { public string screenName; public object screenArg; }
        public class ScreenChangeRequestedEvent : UnityEvent<ScreenChangeRequestEventArgs> { }

        protected static readonly Color navButtonColor = new Color(0.8f, 0.7f, 0.8f);
        private static readonly Font _font = (Font)Resources.GetBuiltinResource(typeof(Font), "Arial.ttf");

        public ScreenChangeRequestedEvent onScreenChangeRequested = new ScreenChangeRequestedEvent();
        public Transform popupParent;
        public abstract string screenId { get; }

        protected AtomAnimation animation => plugin.animation;
        protected OperationsFactory operations => new OperationsFactory(animation, current);

        protected IAtomPlugin plugin;
        protected VamPrefabFactory prefabFactory;
        protected AtomAnimationClip current;
        protected bool _disposing;

        protected ScreenBase()
        {
        }

        public virtual void Init(IAtomPlugin plugin, object arg)
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

        protected Text CreateHeader(string val, int level)
        {
            var headerUI = prefabFactory.CreateSpacer();
            headerUI.height = 40f;

            var text = headerUI.gameObject.AddComponent<Text>();
            text.text = val;
            text.font = _font;
            switch (level)
            {
                case 1:
                    text.fontSize = 30;
                    text.fontStyle = FontStyle.Bold;
                    text.color = new Color(0.95f, 0.9f, 0.92f);
                    break;
                case 2:
                    text.fontSize = 28;
                    text.fontStyle = FontStyle.Bold;
                    text.color = new Color(0.85f, 0.8f, 0.82f);
                    break;
            }

            return text;
        }

        protected UIDynamicButton CreateChangeScreenButton(string label, string screenName)
        {
            var ui = prefabFactory.CreateButton(label);
            ui.button.onClick.AddListener(() => ChangeScreen(screenName));
            ui.buttonColor = navButtonColor;
            return ui;
        }

        public void ChangeScreen(string screenName, object screenArg = null)
        {
            onScreenChangeRequested.Invoke(new ScreenChangeRequestEventArgs { screenName = screenName, screenArg = screenArg });
        }

        public virtual void OnDestroy()
        {
            _disposing = true;
            onScreenChangeRequested.RemoveAllListeners();
            plugin.animation.onCurrentAnimationChanged.RemoveListener(OnCurrentAnimationChanged);
        }
    }
}

