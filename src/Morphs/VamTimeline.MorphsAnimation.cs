using System;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class MorphsAnimation : AnimationBase<MorphsAnimationClip>, IAnimation
    {
        public override float Time { get; set; }
        public float Speed { get; set; }

        public string AddAnimation()
        {
            throw new NotImplementedException();
        }

        public void ChangeAnimation(string animationName)
        {
            throw new NotImplementedException();
        }

        public void Play()
        {
            throw new NotImplementedException();
        }

        public bool IsPlaying()
        {
            throw new NotImplementedException();
        }

        public void Stop()
        {
            throw new NotImplementedException();
        }

        protected override MorphsAnimationClip CreateClip(string animatioName)
        {
            return new MorphsAnimationClip(animatioName);
        }

        public IClipboardEntry Copy()
        {
            throw new NotImplementedException();
        }

        public void Paste(IClipboardEntry clipboard)
        {
            throw new NotImplementedException();
        }

        public override void RebuildAnimation()
        {
            if (Current == null) throw new NullReferenceException("No current animation set");
            foreach (var clip in Clips)
            {
                clip.RebuildAnimation();
            }
        }
    }
}
