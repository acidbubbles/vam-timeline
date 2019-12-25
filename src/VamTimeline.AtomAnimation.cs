using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AcidBubbles.VamTimeline
{
    /// <summary>
    /// VaM Timeline Controller
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
        public float BlendDuration { get; set; } = 1f;

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
                RebuildAnimation();
            }
        }

        public float Time
        {
            get
            {
                if (Current == null) return 0f;
                var animState = Animation[Current.AnimationName];
                if (animState == null) return 0f;
                return animState.time % animState.length;
            }
            set
            {
                if (Current == null) throw new InvalidOperationException("Cannot set time without a current animation");
                var animState = Animation[Current.AnimationName];
                if (animState == null) throw new InvalidOperationException("Cannot set time without a current animation state");
                animState.time = value;
                if (!animState.enabled)
                {
                    // TODO: Can we set this once?
                    animState.enabled = true;
                    Animation.Sample();
                    animState.enabled = false;
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
            AnimationState animState = Animation[Current.AnimationName];
            if (animState == null) return;
            animState.time = 0;
            Animation.Play(Current.AnimationName);
        }

        public void Stop()
        {
            if (Current == null || Animation[Current.AnimationName] == null) return;
            Animation.Stop(Current.AnimationName);
            Time = 0;
        }

        public void SelectControllerByName(string val)
        {
            Current.SelectControllerByName(val);
        }

        public List<string> GetControllersName()
        {
            return Current.GetControllersName();
        }

        public void PauseToggle()
        {
            var animState = Animation[Current.AnimationName];
            animState.enabled = !animState.enabled;
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
                var state = Animation[clip.AnimationName];
                state.speed = clip.Speed;
            }
            // TODO: This is a ugly hack, otherwise the scrubber won't work after modifying a frame
            Animation.Play(Current.AnimationName);
            Animation.Stop(Current.AnimationName);
            Animation[Current.AnimationName].time = time;
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
            }
            Current = anim;
            if (!isPlaying)
            {
                Time = time;
            }
        }
    }
}
