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
    public class AtomPlaybackState
    {
        private float _time;
        public List<AtomClipPlaybackState> clips = new List<AtomClipPlaybackState>();
        public bool isPlaying;
        public float speed = 1f;
        // TODO: Private?
        public AtomClipPlaybackState next;
        public float nextTime;
        // TODO: Move outside?
        public string originalAnimationName;

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
                    if (!clip.enabled) continue;

                    clip.time += delta;
                    if (clip.blendRate != 0)
                    {
                        // TODO: Smooth Lerp
                        clip.weight += clip.blendRate * delta;
                        if (clip.weight >= 1f)
                        {
                            clip.blendRate = 0f;
                            clip.weight = 1f;
                        }
                        else if (clip.weight <= 0f)
                        {
                            clip.blendRate = 0f;
                            clip.weight = 0f;
                            clip.enabled = false;
                        }
                    }
                }
            }
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

        public void Blend(string animationName, float weight, float duration)
        {
            var clip = GetClip(animationName);
            clip.enabled = true;
            clip.blendRate = (weight - clip.weight) / duration;
        }

        public void Reset(string animationName, bool resetTime)
        {
            var current = GetClip(animationName);
            if (resetTime) _time = 0f;
            foreach (var clip in clips)
            {
                if (clip == current)
                {
                    clip.enabled = true;
                    clip.weight = 1f;
                }
                else
                {
                    clip.enabled = false;
                    clip.weight = 0f;
                }
                clip.blendRate = 0f;
                if (resetTime) clip.time = 0f;
            }
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
        private float _time;
        public float weight;
        public bool enabled;
        public float blendRate;
        public float time
        {
            get
            {
                return _time;
            }

            set
            {
                _time = Mathf.Abs(clip.loop ? value % clip.animationLength : Mathf.Max(value, clip.animationLength));
            }
        }

        public AtomClipPlaybackState(AtomAnimationClip clip)
        {
            this.clip = clip;
        }
    }
}
