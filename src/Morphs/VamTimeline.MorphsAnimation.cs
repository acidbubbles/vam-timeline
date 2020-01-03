using System;
using System.Collections.Generic;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class MorphsAnimation : IAnimation
    {
        public float Time { get; set; }
        public float AnimationLength { get; set; }
        public float Speed { get; set; }
        public float BlendDuration { get; set; }

        public string AddAnimation()
        {
            throw new NotImplementedException();
        }

        public void ChangeAnimation(string animationName)
        {
            throw new NotImplementedException();
        }

        public IClipboardEntry Copy()
        {
            throw new NotImplementedException();
        }

        public void DeleteFrame()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<IAnimationTarget> GetAllOrSelectedTargets()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<string> GetAnimationNames()
        {
            throw new NotImplementedException();
        }

        public float GetNextFrame()
        {
            throw new NotImplementedException();
        }

        public float GetPreviousFrame()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<string> GetTargetsNames()
        {
            throw new NotImplementedException();
        }

        public void Initialize()
        {
            throw new NotImplementedException();
        }

        public bool IsEmpty()
        {
            throw new NotImplementedException();
        }

        public bool IsPlaying()
        {
            throw new NotImplementedException();
        }

        public void Paste(IClipboardEntry clipboard)
        {
            throw new NotImplementedException();
        }

        public void Play()
        {
            throw new NotImplementedException();
        }

        public void SelectTargetByName(string name)
        {
            throw new NotImplementedException();
        }

        public void Stop()
        {
            throw new NotImplementedException();
        }
    }
}
