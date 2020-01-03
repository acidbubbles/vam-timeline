using System.Collections.Generic;
using System.Linq;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public abstract class AnimationBase<TClip, TTarget>
        where TClip : class, IAnimationClip<TTarget>
        where TTarget : class, IAnimationTarget
    {
        public List<TClip> Clips { get; } = new List<TClip>();
        public TClip Current { get; set; }

        public abstract float Time { get; set; }

        public float AnimationLength
        {
            get
            {
                return Current.AnimationLength;
            }
            set
            {
                Current.AnimationLength = value;
                RebuildAnimation();
            }
        }

        public float BlendDuration { get; set; } = 1f;

        public void Initialize()
        {
            if (Clips.Count == 0)
                AddClip(CreateClip("Anim1"));
            if (Current == null)
                Current = Clips.First();
        }

        public void AddClip(TClip clip)
        {
            Clips.Add(clip);
        }

        public bool IsEmpty()
        {
            if (Clips.Count == 0) return true;
            if (Clips.Count == 1 && Clips[0].IsEmpty()) return true;
            return false;
        }

        public IEnumerable<string> GetAnimationNames()
        {
            return Clips.Select(c => c.AnimationName);
        }

        public void SelectTargetByName(string name)
        {
            Current.SelectTargetByName(name);
        }

        public IEnumerable<string> GetTargetsNames()
        {
            return Current.GetTargetsNames();
        }

        public IEnumerable<TTarget> GetAllOrSelectedTargets()
        {
            return Current.GetAllOrSelectedTargets();
        }

        public float GetNextFrame()
        {
            return Current.GetNextFrame(Time);
        }

        public float GetPreviousFrame()
        {
            return Current.GetPreviousFrame(Time);
        }

        public void DeleteFrame()
        {
            Current.DeleteFrame(Time);
            RebuildAnimation();
        }

        protected abstract TClip CreateClip(string animatioName);
        public virtual void RebuildAnimation() { }
    }
}
