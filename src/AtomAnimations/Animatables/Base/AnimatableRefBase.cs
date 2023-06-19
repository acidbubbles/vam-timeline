using UnityEngine.Events;

namespace VamTimeline
{
    public interface IAnimatableRefWithTransform
    {
        bool selectedPosition { get; set; }
        bool selectedRotation { get; set; }
    }

    public abstract class AnimatableRefBase
    {
        public bool selected { get; set; }

        public readonly UnityEvent onSelectedChanged = new UnityEvent();

        public bool collapsed { get; set; }

        public abstract string name { get; }
        public abstract object groupKey { get; }
        public abstract string groupLabel { get; }
        public abstract string GetShortName();
        public abstract string GetFullName();
    }
}
