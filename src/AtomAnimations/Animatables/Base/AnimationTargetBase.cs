using System;
using UnityEngine.Events;

namespace VamTimeline
{
    public abstract class AnimationTargetBase<TAnimatableRef> : IDisposable where TAnimatableRef : AnimatableRefBase
    {
        public AnimatableRefBase animatableRefBase => animatableRef;
        public TAnimatableRef animatableRef { get; }
        public UnityEvent onAnimationKeyframesDirty { get; } = new UnityEvent();
        public UnityEvent onAnimationKeyframesRebuilt { get; } = new UnityEvent();

        public IAtomAnimationClip clip { get; set; }

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

        public bool selected
        {
            get
            {
                return animatableRef.selected;
            }
            set
            {
                animatableRef.selected = value;
            }
        }

        public bool collapsed
        {
            get
            {
                return animatableRef.collapsed;
            }
            set
            {
                animatableRef.collapsed = value;
            }
        }

        public string group { get; set; }

        public string name => animatableRef.name;
        public string GetShortName() => animatableRef.GetShortName();
        public string GetFullName() => animatableRef.GetFullName();

        protected AnimationTargetBase(TAnimatableRef animatableRef)
        {
            this.animatableRef = animatableRef;
        }

        public void StartBulkUpdates()
        {
            _bulk++;
        }
        public void EndBulkUpdates()
        {
            if (_bulk == 0)
                throw new InvalidOperationException("There is no bulk update in progress");
            _bulk--;
            if (_bulk == 0 && dirty)
                dirty = true;
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
