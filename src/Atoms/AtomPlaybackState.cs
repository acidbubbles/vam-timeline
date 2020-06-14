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
    public class AtomPlaybackState
    {
        private float _time;
        public List<AtomClipPlaybackState> clips = new List<AtomClipPlaybackState>();
        public bool isPlaying;
        public float speed;
        // TODO: Private?
        public AtomClipPlaybackState next;
        public float nextTime;
        // TODO: Move outside?
        public string originalAnimationName;
        // TODO: Remove
        public float blendingTimeLeft;
        public float blendingDuration;
        public AtomClipPlaybackState previous;
        // TODO: The goal is to get rid of this
        public AtomClipPlaybackState current;

        public float time
        {
            get
            {
                return _time;
            }
            set
            {
                var delta = value - _time;
                _time = value;
                foreach (var clip in clips)
                {
                    clip.time += delta;
                }
            }
        }

        public void Play(AtomAnimationClip current)
        {
            this.current = GetClip(current.animationName);
            originalAnimationName = current.animationName;
            isPlaying = true;
        }

        public AtomClipPlaybackState GetClip(string animationName)
        {
            return clips.FirstOrDefault(c => c.clip.animationName == animationName);
        }

        public void SetNext(string animationName, float time)
        {
            next = GetClip(animationName);
            nextTime = time;
        }
    }

    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class AtomClipPlaybackState
    {
        public readonly AtomAnimationClip clip;
        public float time;
        public float weight;
        public bool enabled;

        public AtomClipPlaybackState(AtomAnimationClip clip)
        {
            this.clip = clip;
        }
    }
}
