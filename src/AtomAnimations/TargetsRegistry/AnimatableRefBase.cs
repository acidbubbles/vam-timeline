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

        public abstract string name { get; }
        public abstract string GetShortName();
    }
}
