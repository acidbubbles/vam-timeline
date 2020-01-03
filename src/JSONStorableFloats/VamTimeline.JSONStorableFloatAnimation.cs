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
    public class JSONStorableFloatAnimation : AnimationBase<JSONStorableFloatAnimationClip>, IAnimation<JSONStorableFloatAnimationClip>
    {
        private float _time;
        private bool _isPlaying;

        public override float Time
        {
            get
            {
                return _time;
            }
            set
            {
                _time = value;
                SampleAnimation();
            }
        }

        private void SampleAnimation()
        {
            foreach (var morph in Current.Storables)
            {
                morph.Storable.val = morph.Value.Evaluate(_time);
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
            _time = 0;
            _isPlaying = true;
        }

        public bool IsPlaying()
        {
            return _isPlaying;
        }

        public void Stop()
        {
            _time = 0;
            _isPlaying = false;
            SampleAnimation();
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

        public void Update()
        {
            if (_isPlaying)
            {
                _time = (_time + UnityEngine.Time.deltaTime) % AnimationLength;
                SampleAnimation();
            }
        }
    }
}
