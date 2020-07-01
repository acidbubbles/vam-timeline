using System;
using UnityEngine.Events;

namespace VamTimeline
{
    public abstract class AnimationTargetBase : IDisposable
    {
        public UnityEvent onSelectedChanged { get; } = new UnityEvent();
        public UnityEvent onAnimationKeyframesDirty { get; } = new UnityEvent();
        public UnityEvent onAnimationKeyframesRebuilt { get; } = new UnityEvent();

        private bool _selected;
        private bool _bulk;
        private bool _dirty = true;

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

        public bool dirty
        {
            get
            {
                return _dirty;
            }
            set
            {
                _dirty = value;
                if (value && !_bulk)
                    onAnimationKeyframesDirty.Invoke();
            }
        }

        public void StartBulkUpdates()
        {
            _bulk = true;
        }
        public void EndBulkUpdates()
        {
            _bulk = false;
            if (dirty) onAnimationKeyframesDirty.Invoke();
        }

        public virtual void Dispose()
        {
            onSelectedChanged.RemoveAllListeners();
            onAnimationKeyframesDirty.RemoveAllListeners();
            onAnimationKeyframesRebuilt.RemoveAllListeners();
        }
    }
}
