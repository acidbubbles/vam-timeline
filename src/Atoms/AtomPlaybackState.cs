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
        private float _playTime;
        public List<AtomClipPlaybackState> clips = new List<AtomClipPlaybackState>();
        public bool isPlaying;
        // This belong in AtomAnimation
        public float speed = 1f;
        // TODO: Move outside?
        public string originalAnimationName;

        public float playTime
        {
            get
            {
                return _playTime;
            }
            set
            {
                var delta = value - _playTime;
                _playTime = value;
                foreach (var clip in clips)
                {
                    if (!clip.enabled) continue;

                    clip.clipTime += delta;
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
            if (animationName == null) throw new ArgumentNullException(nameof(animationName));

            return clips.FirstOrDefault(c => c.clip.animationName == animationName);
        }

        public AtomClipPlaybackState Blend(AtomClipPlaybackState clip, float weight, float duration)
        {
            clip.enabled = true;
            clip.blendRate = (weight - clip.weight) / duration;
            return clip;
        }

        public void Reset(bool resetTime)
        {
            if (resetTime) _playTime = 0f;
            foreach (var clip in clips)
            {
                clip.Reset(resetTime);
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
        private float _clipTime;
        public float weight;
        public bool enabled;
        public bool mainInLayer;
        public float blendRate;
        public bool sequencing;
        public string nextAnimationName;
        public float nextTime;

        public float clipTime
        {
            get
            {
                return _clipTime;
            }

            set
            {
                _clipTime = Mathf.Abs(clip.loop ? value % clip.animationLength : Mathf.Min(value, clip.animationLength));
            }
        }

        public AtomClipPlaybackState(AtomAnimationClip clip)
        {
            this.clip = clip;
        }

        public void SetNext(string nextAnimationName, float nextTime)
        {
            this.nextAnimationName = nextAnimationName;
            this.nextTime = nextTime;
            sequencing = nextAnimationName != null;
        }

        public void Reset(bool resetTime)
        {
            enabled = false;
            weight = 0f;
            blendRate = 0f;
            sequencing = false;
            mainInLayer = false;
            SetNext(null, 0f);
            if (resetTime) clipTime = 0f;
            else clipTime = clipTime.Snap();
        }
    }
}
