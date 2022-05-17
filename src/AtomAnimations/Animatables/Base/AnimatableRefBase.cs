using UnityEngine.Events;

namespace VamTimeline
{
    public abstract class AnimatableRefBase
    {
        private bool _selected;
        public bool selected
        {
            get { return _selected; }
            set
            {
                if (_selected == value) return;
                _selected = value;
                onSelectedChanged.Invoke();
            }
        }
        public readonly UnityEvent onSelectedChanged = new UnityEvent();

        public bool collapsed { get; set; }

        public abstract string name { get; }
        public abstract object groupKey { get; }
        public abstract string groupLabel { get; }
        public abstract string GetShortName();
        public abstract string GetFullName();
    }
}
