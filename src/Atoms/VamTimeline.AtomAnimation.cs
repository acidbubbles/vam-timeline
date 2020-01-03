using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class AtomAnimation : AnimationBase<AtomAnimationClip, FreeControllerV3AnimationTarget>, IAnimation<AtomAnimationClip, FreeControllerV3AnimationTarget>
    {
        private readonly Atom _atom;
        public readonly Animation Animation;
        private AnimationState _animState;

        public float Speed
        {
            get
            {
                return Current.Speed;
            }

            set
            {
                Current.Speed = value;
                if (_animState == null) return;
                _animState.speed = value;
                if (Current.AnimationPattern != null)
                    Current.AnimationPattern.SetFloatParamValue("speed", value);
            }
        }

        public override float Time
        {
            get
            {
                if (Current == null) return 0f;
                if (_animState == null) return 0f;
                return _animState.time % _animState.length;
            }
            set
            {
                if (Current == null) return;
                if (_animState == null) return;
                _animState.time = value;
                if (!_animState.enabled)
                {
                    _animState.enabled = true;
                    Animation.Sample();
                    _animState.enabled = false;
                }
            }
        }

        public AtomAnimation(Atom atom)
        {
            if (atom == null) throw new ArgumentNullException(nameof(atom));

            _atom = atom;
            Animation = _atom.gameObject.GetComponent<Animation>() ?? _atom.gameObject.AddComponent<Animation>();
            if (Animation == null) throw new NullReferenceException("Could not create an Animation");
        }

        protected override AtomAnimationClip CreateClip(string animatioName)
        {
            return new AtomAnimationClip("Anim1");
        }

        public FreeControllerV3AnimationTarget Add(FreeControllerV3 controller)
        {
            var added = Current.Add(controller);
            RebuildAnimation();
            return added;
        }

        public void Remove(FreeControllerV3 controller)
        {
            Current.Remove(controller);
            RebuildAnimation();
        }

        public void Play()
        {
            if (Current == null) return;
            if (_animState == null) return;
            _animState.time = 0;
            Animation.Play(Current.AnimationName);
            if (Current.AnimationPattern)
            {
                Current.AnimationPattern.SetBoolParamValue("loopOnce", false);
                Current.AnimationPattern.ResetAndPlay();
            }
        }

        public void Stop()
        {
            if (Current == null || _animState == null) return;
            Animation.Stop();
            Time = 0;
            foreach (var clip in Clips)
            {
                if (clip.AnimationPattern)
                {
                    clip.AnimationPattern.SetBoolParamValue("loopOnce", true);
                    clip.AnimationPattern.ResetAnimation();
                }
            }
        }

        public bool IsPlaying()
        {
            return Animation.IsPlaying(Current.AnimationName);
        }

        public override void RebuildAnimation()
        {
            if (Current == null) throw new NullReferenceException("No current animation set");
            var time = Time;
            foreach (var clip in Clips)
            {
                clip.RebuildAnimation();
                Animation.AddClip(clip.Clip, clip.AnimationName);
                var animState = Animation[clip.AnimationName];
                animState.speed = clip.Speed;
            }
            // This is a ugly hack, otherwise the scrubber won't work after modifying a frame
            Animation.Play(Current.AnimationName);
            Animation.Stop(Current.AnimationName);
            _animState = Animation[Current.AnimationName];
            _animState.time = time;
        }

        public void ChangeCurve(string curveType)
        {
            var time = Time;
            Current.ChangeCurve(time, curveType);
            RebuildAnimation();
        }

        public string AddAnimation()
        {
            // TODO: Let the user name the animation
            var lastAnimationName = Clips.Last().AnimationName;
            var lastAnimationIndex = lastAnimationName.Substring(4);
            var animationName = "Anim" + (int.Parse(lastAnimationIndex) + 1);
            var clip = CreateClip(animationName);
            clip.Speed = Speed;
            clip.AnimationLength = AnimationLength;
            foreach (var controller in Current.Targets.Select(c => c.Controller))
            {
                var animController = clip.Add(controller);
                animController.SetKeyframeToCurrentTransform(0f);
            }
            AddClip(clip);

            return animationName;
        }

        public void ChangeAnimation(string animationName)
        {
            var anim = Clips.FirstOrDefault(c => c.AnimationName == animationName);
            if (anim == null) return;
            var time = Time;
            var isPlaying = IsPlaying();
            if (isPlaying)
            {
                Animation.Blend(Current.AnimationName, 0f, BlendDuration);
                Animation.Blend(animationName, 1f, BlendDuration);
                if (Current.AnimationPattern != null)
                {
                    // Let the loop finish during the transition
                    Current.AnimationPattern.SetBoolParamValue("loopOnce", true);
                }
            }
            Current.SelectTargetByName("");
            Current = anim;
            if (!isPlaying)
            {
                Play();
                Stop();
                Time = time;
            }
            if (isPlaying && Current.AnimationPattern != null)
            {
                Current.AnimationPattern.SetBoolParamValue("loopOnce", false);
                Current.AnimationPattern.ResetAndPlay();
            }
        }

        public void SmoothAllFrames()
        {
            Current.SmoothAllFrames();
            RebuildAnimation();
        }

        public IClipboardEntry Copy()
        {
            var entries = new List<FreeControllerV3ClipboardEntry>();
            var time = Time;
            foreach (var controller in Current.GetAllOrSelectedTargets())
            {
                entries.Add(new FreeControllerV3ClipboardEntry
                {
                    Controller = controller.Controller,
                    Snapshot = controller.GetCurveSnapshot(time)
                });
            }
            return new AtomClipboardEntry { Entries = entries };
        }

        public void Paste(IClipboardEntry clipboard)
        {
            float time = Time;
            foreach (var entry in ((AtomClipboardEntry)clipboard).Entries)
            {
                var animController = Current.Targets.FirstOrDefault(c => c.Controller == entry.Controller);
                if (animController == null)
                    animController = Add(entry.Controller);
                animController.SetCurveSnapshot(time, entry.Snapshot);
            }
            RebuildAnimation();
        }
    }
}
