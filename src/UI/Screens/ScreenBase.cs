using UnityEngine;
using UnityEngine.Events;

namespace VamTimeline
{
    public abstract class ScreenBase : MonoBehaviour
    {
        public class ScreenChangeRequestEventArgs { public string screenName; public object screenArg; }
        public class ScreenChangeRequestedEvent : UnityEvent<ScreenChangeRequestEventArgs> { }

        protected static readonly Color NavButtonColor = new Color(0.8f, 0.7f, 0.8f);

        public readonly ScreenChangeRequestedEvent onScreenChangeRequested = new ScreenChangeRequestedEvent();
        public Transform popupParent;
        public abstract string screenId { get; }

        protected AtomAnimation animation => plugin.animation;
        protected AtomAnimationEditContext animationEditContext => plugin.animationEditContext;
        protected AtomAnimationClip current => animationEditContext.current;
        protected OperationsFactory operations => plugin.operations;

        protected IAtomPlugin plugin;
        protected VamPrefabFactory prefabFactory;
        protected bool disposing;

        public virtual void Init(IAtomPlugin plugin, object arg)
        {
            this.plugin = plugin;
            prefabFactory = gameObject.AddComponent<VamPrefabFactory>();
            prefabFactory.plugin = plugin;
            plugin.animationEditContext.onCurrentAnimationChanged.AddListener(OnCurrentAnimationChanged);
        }

        protected virtual void OnCurrentAnimationChanged(AtomAnimationEditContext.CurrentAnimationChangedEventArgs args)
        {
        }

        protected UIDynamicButton CreateChangeScreenButton(string label, string screenName)
        {
            var ui = prefabFactory.CreateButton(label);
            ui.button.onClick.AddListener(() => ChangeScreen(screenName));
            ui.buttonColor = NavButtonColor;
            return ui;
        }

        public void ChangeScreen(string screenName, object screenArg = null)
        {
            onScreenChangeRequested.Invoke(new ScreenChangeRequestEventArgs { screenName = screenName, screenArg = screenArg });
        }

        public virtual void OnDestroy()
        {
            prefabFactory.ClearConfirm();
            disposing = true;
            onScreenChangeRequested.RemoveAllListeners();
            plugin.animationEditContext.onCurrentAnimationChanged.RemoveListener(OnCurrentAnimationChanged);
        }
    }
}

