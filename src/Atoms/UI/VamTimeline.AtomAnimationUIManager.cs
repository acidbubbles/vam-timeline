using System;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class AtomAnimationUIManager
    {
        protected IAtomPlugin _plugin;
        private AtomAnimationBaseUI _current;

        public AtomAnimationUIManager(IAtomPlugin plugin)
        {
            _plugin = plugin;
        }

        public void InitCustomUI()
        {
            _current = new AtomAnimationSettingsUI(_plugin);
            _current.Init();
        }

        public void AnimationUpdated()
        {
            if (_current == null) return;
            _current.AnimationUpdated();
        }

        public void UIUpdated()
        {
            if (_current == null) return;
            _current.UIUpdated();
        }
    }
}

