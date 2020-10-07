using System;
using UnityEngine.Events;

namespace VamTimeline
{
    public abstract class AnimationTargetBase : IDisposable
    {
        public UnityEvent onAnimationKeyframesDirty { get; } = new UnityEvent();
        public UnityEvent onAnimationKeyframesRebuilt { get; } = new UnityEvent();

        public AtomAnimationClip clip;

        private int _bulk;
        private bool _dirty = true;

        public bool dirty
        {
            get
            {
                return _dirty;
            }
            set
            {
                _dirty = value;
                if (value && _bulk == 0)
                    onAnimationKeyframesDirty.Invoke();
            }
        }

        public void StartBulkUpdates()
        {
            _bulk++;
        }
        public void EndBulkUpdates()
        {
            if (_bulk == 0) throw new InvalidOperationException("There is no bulk update in progress");
            _bulk--;
            if (_bulk == 0 && dirty) onAnimationKeyframesDirty.Invoke();
        }

        public virtual void SelectInVam()
        {
        }

        public virtual void Dispose()
        {
            onAnimationKeyframesDirty.RemoveAllListeners();
            onAnimationKeyframesRebuilt.RemoveAllListeners();
        }
    }
}
