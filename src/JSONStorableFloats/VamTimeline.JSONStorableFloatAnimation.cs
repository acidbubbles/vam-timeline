using System;
using System.Linq;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class JSONStorableFloatAnimation : AnimationBase<JSONStorableFloatAnimationClip, JSONStorableFloatAnimationTarget>, IAnimation<JSONStorableFloatAnimationClip, JSONStorableFloatAnimationTarget>
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
            foreach (var morph in Current.Targets)
            {
                morph.Storable.val = morph.Value.Evaluate(_time);
            }
        }

        public float Speed { get; set; } = 1f;

        public string AddAnimation()
        {
            string animationName = GetNewAnimationName();
            var clip = CreateClip(animationName);
            CopyCurrentValues(clip);
            AddClip(clip);
            return animationName;
        }

        private void CopyCurrentValues(JSONStorableFloatAnimationClip clip)
        {
            clip.Speed = Speed;
            clip.AnimationLength = AnimationLength;
            foreach (var storable in Current.Targets.Select(c => c.Storable))
            {
                var animController = clip.Add(storable);
                animController.SetKeyframe(0f, storable.val);
            }
        }

        public void ChangeAnimation(string animationName)
        {
            Current.SelectTargetByName("");
            Current = Clips.FirstOrDefault(c => c.AnimationName == animationName);
            if (!_isPlaying)
            {
                Time = 0f;
                SampleAnimation();
            }
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
                _time = (_time + UnityEngine.Time.deltaTime * Speed) % AnimationLength;
                SampleAnimation();
            }
        }
    }
}
