using System;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public abstract class AtomAnimationBaseUI
    {
        protected IAtomPlugin _plugin;

        protected AtomAnimationBaseUI(IAtomPlugin plugin)
        {
            _plugin = plugin;
        }

        public abstract void Init();

        public virtual void AnimationUpdated()
        {
            UIUpdated();
        }

        public virtual void UIUpdated()
        {
        }
    }
}

