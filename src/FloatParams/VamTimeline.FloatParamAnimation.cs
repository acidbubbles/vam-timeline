using System;
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
    public class FloatParamAnimation : AnimationBase<FloatParamAnimationClip, FloatParamAnimationTarget>, IAnimation<FloatParamAnimationClip, FloatParamAnimationTarget>
    {
        private FloatParamAnimationClip _blendingAnimation;
        private float _blendingTimeLeft;
        private float _blendingDuration;

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
                var val = morph.Value.Evaluate(_time);
                if (_blendingAnimation != null)
                {
                    var blendingTarget = _blendingAnimation.Targets.FirstOrDefault(t => t.FloatParam == morph.FloatParam);
                    if (blendingTarget != null)
                    {
                        var weight = _blendingTimeLeft / _blendingDuration;
                        morph.FloatParam.val = (blendingTarget.Value.Evaluate(_time) * (weight)) + (val * (1 - weight));
                    }
                    else
                    {
                        morph.FloatParam.val = val;
                    }
                }
                else
                {
                    morph.FloatParam.val = val;
                }
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

        private void CopyCurrentValues(FloatParamAnimationClip clip)
        {
            clip.Speed = Speed;
            clip.AnimationLength = AnimationLength;
            foreach (var target in Current.Targets)
            {
                var animController = clip.Add(target.Storable, target.FloatParam);
                animController.SetKeyframe(0f, target.FloatParam.val);
            }
        }

        public void ChangeAnimation(string animationName)
        {
            if (_isPlaying)
            {
                _blendingAnimation = Current;
                _blendingTimeLeft = _blendingDuration = BlendDuration;
            }
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
            _blendingTimeLeft = 0;
            _blendingDuration = 0;
            _blendingAnimation = null;
            SampleAnimation();
        }

        protected override FloatParamAnimationClip CreateClip(string animatioName)
        {
            return new FloatParamAnimationClip(animatioName);
        }

        public IClipboardEntry Copy()
        {
            var entries = new List<FloatParamValClipboardEntry>();
            var time = Time;
            foreach (var target in Current.GetAllOrSelectedTargets())
            {
                if (!target.Value.keys.Any(k => k.time == time)) continue;
                entries.Add(new FloatParamValClipboardEntry
                {
                    Storable = target.Storable,
                    FloatParam = target.FloatParam,
                    Snapshot = target.Value.keys.FirstOrDefault(k => k.time == time)
                });
            }
            return new FloatParamClipboardEntry { Entries = entries };
        }

        public void Paste(IClipboardEntry clipboard)
        {
            float time = Time;
            foreach (var entry in ((FloatParamClipboardEntry)clipboard).Entries)
            {
                var animController = Current.Targets.FirstOrDefault(c => c.FloatParam == entry.FloatParam);
                if (animController == null)
                    animController = Current.Add(entry.Storable, entry.FloatParam);
                animController.SetKeyframe(time, entry.Snapshot.value);
            }
            RebuildAnimation();
        }

        public void Update()
        {
            if (_isPlaying)
            {
                _time = (_time + UnityEngine.Time.deltaTime * Speed) % AnimationLength;

                if (_blendingAnimation != null)
                {
                    _blendingTimeLeft -= UnityEngine.Time.deltaTime;
                    if (_blendingTimeLeft <= 0)
                    {
                        _blendingTimeLeft = 0;
                        _blendingDuration = 0;
                        _blendingAnimation = null;
                    }
                }

                SampleAnimation();
            }
        }
    }
}
