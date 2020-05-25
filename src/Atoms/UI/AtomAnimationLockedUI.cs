namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class AtomAnimationLockedUI : AtomAnimationBaseUI
    {
        public const string ScreenName = "Locked (Performance)";
        public override string Name => ScreenName;

        public AtomAnimationLockedUI(IAtomPlugin plugin)
            : base(plugin)
        {

        }
        public override void Init()
        {
            base.Init();
        }
    }
}

