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
        public UnityEvent AnimationCurveModified { get; } = new UnityEvent();

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
        // TODO: Instead of MarkDirty, just use a setter??
        public bool Dirty
        {
            get
            {
                return _dirty;
            }
            set
            {
                if(value == true && stuff) throw new Exception("Hey");
                _dirty = value;
                if (value && !_bulk)
                    AnimationCurveModified.Invoke();
            }
        }
        public static bool stuff = false;

        public void StartBulkUpdates()
        {
            _bulk = true;
        }
        public void EndBulkUpdates()
        {
            _bulk = false;
            if (Dirty) AnimationCurveModified.Invoke();
        }

        public void Dispose()
        {
            SelectedChanged.RemoveAllListeners();
        }
    }
}
