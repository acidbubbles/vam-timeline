using System;
using UnityEngine;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class JSONStorableFloatAnimation : AnimationBase<JSONStorableFloatAnimationClip>, IAnimation
    {
        private float _time;

        public override float Time
        {
            get
            {
                return _time;
            }
            set
            {
                _time = value;
                foreach (var morph in Current.Morphs)
                {
                    morph.Storable.val = morph.Value.Evaluate(_time);
                }
            }
        }
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

        protected override JSONStorableFloatAnimationClip CreateClip(string animatioName)
        {
            return new JSONStorableFloatAnimationClip(animatioName);
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
        }
    }
}
