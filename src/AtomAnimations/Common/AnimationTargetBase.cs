using System;
using UnityEngine.Events;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public abstract class AnimationTargetBase : IDisposable
    {
        public UnityEvent onSelectedChanged { get; } = new UnityEvent();
        public UnityEvent onAnimationKeyframesModified { get; } = new UnityEvent();

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
                    onAnimationKeyframesModified.Invoke();
            }
        }

        public void StartBulkUpdates()
        {
            _bulk = true;
        }
        public void EndBulkUpdates()
        {
            _bulk = false;
            if (dirty) onAnimationKeyframesModified.Invoke();
        }

        public void Dispose()
        {
            onSelectedChanged.RemoveAllListeners();
        }
    }
}
