namespace VamTimeline
{
    public class GlobalTriggersScreen : ScreenBase
    {
        public const string ScreenName = "Global Triggers";

        public override string screenId => ScreenName;

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            CreateChangeScreenButton("<b><</b> <i>Back</i>", MoreScreen.ScreenName);

            prefabFactory.CreateButton("On Clips Changed").button.onClick.AddListener(()=>
            {
                animation.clipListChangedTrigger.trigger.triggerActionsParent = popupParent;
                animation.clipListChangedTrigger.trigger.OpenTriggerActionsPanel();
            });
            prefabFactory.CreateButton("On Is Playing Changed").button.onClick.AddListener(()=>
            {
                animation.isPlayingChangedTrigger.trigger.triggerActionsParent = popupParent;
                animation.isPlayingChangedTrigger.trigger.OpenTriggerActionsPanel();
            });
        }
    }
}

