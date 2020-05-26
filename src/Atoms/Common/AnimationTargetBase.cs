using System;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public abstract class AnimationTargetBase
    {
        private bool _selected;
        public bool Selected
        {
            get { return _selected; }
            set
            {
                if (_selected == value) return;
                _selected = value;
                SelectedChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler SelectedChanged;
        public bool Dirty { get; set; } = true;
    }
}
