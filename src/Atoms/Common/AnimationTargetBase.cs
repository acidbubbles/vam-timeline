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
        public bool Dirty { get; set; } = true;

        public void Dispose()
        {
            SelectedChanged.RemoveAllListeners();
        }
    }
}
