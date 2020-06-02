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
        public UnityEvent SelectedChanged { get; } = new UnityEvent();
        public UnityEvent AnimationKeyframesModified { get; } = new UnityEvent();

        private bool _selected;
        private bool _bulk;
        private bool _dirty = true;

        public bool Selected
        {
            get { return _selected; }
            set
            {
                if (_selected == value) return;
                _selected = value;
                SelectedChanged.Invoke();
            }
        }

        public bool Dirty
        {
            get
            {
                return _dirty;
            }
            set
            {
                _dirty = value;
                if (value && !_bulk)
                    AnimationKeyframesModified.Invoke();
            }
        }

        public void StartBulkUpdates()
        {
            _bulk = true;
        }
        public void EndBulkUpdates()
        {
            _bulk = false;
            if (Dirty) AnimationKeyframesModified.Invoke();
        }

        public void Dispose()
        {
            SelectedChanged.RemoveAllListeners();
        }
    }
}
