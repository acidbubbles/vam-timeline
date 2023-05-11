using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Events;

namespace VamTimeline
{
    public partial class AtomAnimation
    {
        public class AtomAnimationClipEvent : UnityEvent<AtomAnimationClip> { }

        private static readonly Regex _lastDigitsRegex = new Regex(@"[0-9]+$");

        public readonly UnityEvent onAnimationSettingsChanged = new UnityEvent();
        public readonly UnityEvent onSpeedChanged = new UnityEvent();
        public readonly UnityEvent onWeightChanged = new UnityEvent();
        public readonly UnityEvent onClipsListChanged = new UnityEvent();
        public readonly UnityEvent onAnimationRebuilt = new UnityEvent();
        public readonly UnityEvent onPausedChanged = new UnityEvent();
        public readonly AtomAnimationClipEvent onIsPlayingChanged = new AtomAnimationClipEvent();
        public readonly AtomAnimationClipEvent onClipIsPlayingChanged = new AtomAnimationClipEvent();
        public readonly UnityEvent onSegmentChanged = new UnityEvent();
        public readonly AtomAnimationClipEvent onSegmentPlayed = new AtomAnimationClipEvent();

        public Logger logger;

        public bool recording;
        public bool focusOnLayer;
        public AtomPose lastAppliedPose;

        public IFadeManager fadeManager;
        private float _scheduleFadeIn = float.MaxValue;

        public List<AtomAnimationClip> clips { get; } = new List<AtomAnimationClip>();
        public bool isPlaying { get; private set; }
        private string _playingAnimationSegment = null;
        public int playingAnimationSegmentId { get; private set; } = AtomAnimationClip.NoneAnimationSegmentId;

        public bool IsPlayingAnimationSegment(string animationSegment)
        {
            return IsPlayingAnimationSegment(animationSegment.ToId());
        }

        public bool IsPlayingAnimationSegment(int animationSegmentId)
        {
            if (animationSegmentId == playingAnimationSegmentId) return true;
            if (animationSegmentId == AtomAnimationClip.SharedAnimationSegmentId) return true;
            return false;
        }

        public string playingAnimationSegment
        {
            get { return _playingAnimationSegment; }
            set
            {
                var id = value.ToId();
                if (id == playingAnimationSegmentId) return;
                if (id == AtomAnimationClip.SharedAnimationSegmentId) return;
                _playingAnimationSegment = value;
                playingAnimationSegmentId = id;
                onSegmentChanged.Invoke();
            }
        }
        public float autoStop;
        private bool _paused;
        public bool paused
        {
            get
            {
                return _paused;
            }
            set
            {
                var dispatch = value != _paused;
                _paused = value;
                if (dispatch)
                    onPausedChanged.Invoke();
            }
        }
        private bool allowAnimationProcessing => isPlaying && !SuperController.singleton.freezeAnimation;

        public int timeMode { get; set; } = TimeModes.RealTime;

        public bool liveParenting { get; set; } = true;

        public bool master { get; set; }

        public bool simulationFrozen;

        public float playTime { get; private set; }

        private float _globalSpeed = 1f;
        public float globalSpeed
        {
            get
            {
                return _globalSpeed;
            }

            set
            {
                _globalSpeed = value;
                for (var i = 0; i < clips.Count; i++)
                {
                    var clip = clips[i];
                    if (clip.animationPattern != null)
                        clip.animationPattern.SetFloatParamValue("speed", value);
                }

                onSpeedChanged.Invoke();
            }
        }

        private float _globalWeight = 1f;
        private float _globalScaledWeight = 1f;
        public float globalWeight
        {
            get
            {
                return _globalWeight;
            }

            set
            {
                _globalWeight = Mathf.Clamp01(value);
                _globalScaledWeight = value.ExponentialScale(0.1f, 1f);
                onWeightChanged.Invoke();
            }
        }

        public bool sequencing { get; private set; }

        private bool _animationRebuildRequestPending;
        private bool _animationRebuildInProgress;
        private bool _allowPlayingTermination;

        public AtomAnimationsClipsIndex index { get; }
        public AnimatablesRegistry animatables { get; }

        public bool syncSubsceneOnly { get; set; }
        public bool syncWithPeers { get; set; } = true;
        public bool forceBlendTime { get; set; }

        public bool ignoreSequencing { get; set; }

        public AtomAnimation()
        {
            index = new AtomAnimationsClipsIndex(clips);
            animatables = new AnimatablesRegistry();
        }

    }
}
