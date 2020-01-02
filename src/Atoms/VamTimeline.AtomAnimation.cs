using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class AtomAnimation
    {
        private readonly Atom _atom;
        public readonly Animation Animation;
        public readonly List<AtomAnimationClip> Clips = new List<AtomAnimationClip>();
        public AtomAnimationClip Current;
        public UnityEvent Updated = new UnityEvent();
        private float _blendDuration = 1f;
        private AnimationState _animState;

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

        public float BlendDuration
        {
            get
            {
                return _blendDuration;
            }
            set
            {
                _blendDuration = value;
                Updated.Invoke();
            }
        }

        public float Time
        {
            get
            {
                if (Current == null) return 0f;
                if (_animState == null) return 0f;
                return _animState.time % _animState.length;
            }
            set
            {
                if (Current == null) throw new InvalidOperationException("Cannot set time without a current animation");
                if (_animState == null) throw new InvalidOperationException("Cannot set time without a current animation state");
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
            _atom = atom;
            Animation = _atom.gameObject.GetComponent<Animation>() ?? _atom.gameObject.AddComponent<Animation>();
            if (Animation == null) throw new NullReferenceException("Could not create an Animation");
        }

        public void Initialize()
        {
            if (Clips.Count == 0)
                Clips.Add(new AtomAnimationClip("Anim1"));
            if (Current == null)
                Current = Clips.First();
        }

        public void AddClip(AtomAnimationClip clip)
        {
            Clips.Add(clip);
            Animation.AddClip(clip.Clip, clip.AnimationName);
        }

        public FreeControllerV3Animation Add(FreeControllerV3 controller)
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

        public void SelectControllerByName(string val)
        {
            Current.SelectControllerByName(val);
        }

        public List<string> GetControllersName()
        {
            return Current.GetControllersName();
        }

        public bool IsPlaying()
        {
            return Animation.IsPlaying(Current.AnimationName);
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

        public void RebuildAnimation()
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
            Updated.Invoke();
        }

        public IEnumerable<FreeControllerV3Animation> GetAllOrSelectedControllers()
        {
            return Current.GetAllOrSelectedControllers();
        }

        public void ChangeCurve(string val)
        {
            var time = Time;
            Current.ChangeCurve(time, val);
            RebuildAnimation();
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
            Current.SelectControllerByName("");
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
    }
}
