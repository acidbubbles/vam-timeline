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

        public AtomAnimation(Atom atom)
        {
            _atom = atom;
            Animation = _atom.gameObject.GetComponent<Animation>() ?? _atom.gameObject.AddComponent<Animation>();
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

        public void Play()
        {
            AnimationState animState = Animation[Current.AnimationName];
            animState.time = 0;
            Animation.Play(Current.AnimationName);
        }

        internal void Stop()
        {
            Animation.Stop(Current.AnimationName);
            Time = 0;
        }

        public float Speed
        {
            get
            {
                AnimationState animState = Animation[Current.AnimationName];
                return animState.speed;
            }

            set
            {
                AnimationState animState = Animation[Current.AnimationName];
                animState.speed = value;
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

        public void PauseToggle()
        {
            var animState = Animation[Current.AnimationName];
            animState.enabled = !animState.enabled;
        }

        public bool IsPlaying()
        {
            return Animation.IsPlaying(Current.AnimationName);
        }

        public float Time
        {
            get
            {
                var animState = Animation[Current.AnimationName];
                if (animState == null) return 0f;
                return animState.time % animState.length;
            }
            set
            {
                var animState = Animation[Current.AnimationName];
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

        public void NextFrame()
        {
            Time = Current.GetNextFrame(Time);
        }

        public void PreviousFrame()
        {
            Time = Current.GetPreviousFrame(Time);
        }

        public void DeleteFrame()
        {
            Current.DeleteFrame(Time);
            RebuildAnimation();
        }

        public void RebuildAnimation()
        {
            var time = Animation[Current.AnimationName].time;
            Current.RebuildAnimation();
            Animation.AddClip(Current.Clip, Current.AnimationName);
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
    }
}
