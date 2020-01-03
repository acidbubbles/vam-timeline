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
    public abstract class AnimationBase<TClip> where TClip : class, IAnimationClip
    {
        public readonly List<TClip> Clips = new List<TClip>();
        public TClip Current;

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
                Clips.Add(CreateClip("Anim1"));
            if (Current == null)
                Current = Clips.First();
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

        public IEnumerable<IAnimationTarget> GetAllOrSelectedTargets()
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
        public abstract void RebuildAnimation();
    }
}
