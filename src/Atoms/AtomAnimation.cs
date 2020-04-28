using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

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
        public const float PaddingBeforeLoopFrame = 0.001f;
        public const float InterpolationMaxDistanceDelta = 1.5f;
        public const float InterpolationMaxAngleDelta = 180.0f;
        public const string RandomizeAnimationName = "(Randomize)";
        public const string RandomizeGroupSuffix = "/*";
        private readonly Atom _atom;
        private readonly Animation _animation;
        private AnimationState _animState;
        private bool _isPlaying;
        private float _interpolateUntil = 0f;
        private bool _playQueuedAfterInterpolation;
        private float _playTime;
        private AtomAnimationClip _previousClip;
        private float _blendingTimeLeft;
        private float _blendingDuration;
        private string _nextAnimation;
        private float _nextAnimationTime;
        private float _speed = 1f;
        // TODO: If we can either get a global counter or infer this from the plugin number, it would be better.
        private readonly int _layer = Random.Range(0, int.MaxValue);

        public List<AtomAnimationClip> Clips { get; } = new List<AtomAnimationClip>();
        public AtomAnimationClip Current { get; set; }
        public string PlayedAnimation { get; private set; }
        public float InterpolationTimeout { get; set; } = 1f;

        public float Time
        {
            get
            {
                var time = _animState != null && _animState.enabled ? _animState.time : _playTime;
                if (Current.Loop) return time % Current.AnimationLength;
                return time;
            }
            set
            {
                _playTime = value;
                if (Current == null) return;
                SampleParamsAnimation();
                if (_animState != null)
                    _animState.time = value;
                if (!_isPlaying)
                    _interpolateUntil = UnityEngine.Time.time + InterpolationTimeout;
            }
        }

        public float Speed
        {
            get
            {
                return _speed;
            }

            set
            {
                _speed = value;
                foreach (var clip in Clips)
                {
                    var animState = _animation[clip.AnimationName];
                    if (animState != null)
                        animState.speed = _speed;
                    if (clip.AnimationPattern != null)
                        clip.AnimationPattern.SetFloatParamValue("speed", value);
                }
            }
        }

        public AtomAnimation(Atom atom)
        {
            if (atom == null) throw new ArgumentNullException(nameof(atom));
            _atom = atom;
            _animation = _atom.gameObject.GetComponent<Animation>() ?? _atom.gameObject.AddComponent<Animation>();
            if (_animation == null) throw new NullReferenceException($"Could not create an Animation component on {_atom.uid}");
        }

        public void Initialize()
        {
            if (Clips.Count == 0)
                AddClip(new AtomAnimationClip("Anim 1"));
            if (Current == null)
                Current = Clips.First();
        }

        public void AddClip(AtomAnimationClip clip)
        {
            Clips.Add(clip);
        }

        public bool IsEmpty()
        {
            if (Clips.Count == 0) return true;
            if (Clips.Count == 1 && Clips[0].IsEmpty()) return true;
            return false;
        }

        public IEnumerable<string> GetAnimationNames()
        {
            return Clips.Select(c => c.AnimationName);
        }

        protected string GetNewAnimationName()
        {
            for (var i = Clips.Count + 1; i < 999; i++)
            {
                var animationName = "Anim " + i;
                if (!Clips.Any(c => c.AnimationName == animationName)) return animationName;
            }
            return Guid.NewGuid().ToString();
        }

        public FreeControllerAnimationTarget Add(FreeControllerV3 controller)
        {
            var added = Current.Add(controller);
            if (added != null)
            {
                added.SetKeyframeToCurrentTransform(0f);
                added.SetKeyframeToCurrentTransform(Current.AnimationLength);
                if (!Current.Loop)
                    added.ChangeCurve(Current.AnimationLength, CurveTypeValues.CopyPrevious);
            }
            return added;
        }

        public FloatParamAnimationTarget Add(JSONStorable storable, JSONStorableFloat jsf)
        {
            var added = Current.Add(storable, jsf);
            if (added != null)
            {
                added.SetKeyframe(0f, jsf.val);
                added.SetKeyframe(Current.AnimationLength, jsf.val);
            }
            return added;
        }

        public void SetKeyframe(FloatParamAnimationTarget target, float time, float val)
        {
            time = time.Snap();
            if (time > Current.AnimationLength)
                time = Current.AnimationLength;
            target.SetKeyframe(time, val);
        }

        public void SetKeyframeToCurrentTransform(FreeControllerAnimationTarget target, float time)
        {
            time = time.Snap();
            if (time > Current.AnimationLength)
                time = Current.AnimationLength;
            target.SetKeyframeToCurrentTransform(time);
        }

        public void Play()
        {
            if (Current == null) return;
            if (Current == null)
            {
                SuperController.LogError($"VamTimeline: Cannot play animation, Timeline is still loading");
                return;
            }
            if (_interpolateUntil > 0)
            {
                _playQueuedAfterInterpolation = true;
                return;
            }
            PlayedAnimation = Current.AnimationName;
            _isPlaying = true;
            if (_animState != null)
                _animation.Play(Current.AnimationName);
            if (Current.AnimationPattern)
            {
                Current.AnimationPattern.SetBoolParamValue("loopOnce", false);
                Current.AnimationPattern.ResetAndPlay();
            }
            DetermineNextAnimation(_playTime);
        }

        private void DetermineNextAnimation(float time)
        {
            _nextAnimation = null;
            _nextAnimationTime = 0;

            if (Current.NextAnimationName == null) return;
            if (Clips.Count == 1) return;

            if (Current.NextAnimationTime > 0 + float.Epsilon)
                _nextAnimationTime = (time + Current.NextAnimationTime).Snap();
            else
                return;

            if (Current.NextAnimationName == RandomizeAnimationName)
            {
                var idx = Random.Range(0, Clips.Count - 1);
                if (idx >= Clips.IndexOf(Current)) idx += 1;
                _nextAnimation = Clips[idx].AnimationName;
            }
            else if (Current.NextAnimationName.EndsWith(RandomizeGroupSuffix))
            {
                var prefix = Current.NextAnimationName.Substring(0, Current.NextAnimationName.Length - RandomizeGroupSuffix.Length);
                var group = Clips
                    .Where(c => c.AnimationName != Current.AnimationName)
                    .Where(c => c.AnimationName.StartsWith(prefix))
                    .ToList();
                var idx = Random.Range(0, group.Count);
                _nextAnimation = group[idx].AnimationName;
            }
            else
            {
                _nextAnimation = Current.NextAnimationName;
            }
        }

        private void SampleParamsAnimation()
        {
            var time = Time;
            var weight = _blendingTimeLeft / _blendingDuration;
            foreach (var morph in Current.TargetFloatParams)
            {
                var val = morph.Value.Evaluate(time);
                if (_previousClip != null)
                {
                    var blendingTarget = _previousClip.TargetFloatParams.FirstOrDefault(t => t.FloatParam == morph.FloatParam);
                    if (blendingTarget != null)
                    {
                        morph.FloatParam.val = (blendingTarget.Value.Evaluate(_playTime) * weight) + (val * (1 - weight));
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

        public void Update()
        {
            if (_isPlaying)
            {
                /* Diagnostics
                if (_animState == null)
                {
                    SuperController.LogError("VamTimeline: Animation state is null");
                    _isPlaying = false;
                    return;
                }
                if (!_animState.enabled)
                {
                    SuperController.LogError("VamTimeline: Animation has stopped");
                    _isPlaying = false;
                    return;
                }
                if (_animState.weight == 0)
                {
                    SuperController.LogError("VamTimeline: Animation has a weight of 0");
                    _isPlaying = false;
                    return;
                }
                if (!_animation.IsPlaying(_animState.name))
                {
                    SuperController.LogError("VamTimeline: Animation state is enabled but animation is not playing");
                    _isPlaying = false;
                    return;
                }
                if (_animState.time == 0)
                {
                    SuperController.LogError("VamTimeline: Animation state time is 0");
                    return;
                }
                */

                _playTime += UnityEngine.Time.deltaTime * Speed;

                if (_previousClip != null)
                {
                    _blendingTimeLeft -= UnityEngine.Time.deltaTime;
                    if (_blendingTimeLeft <= 0)
                    {
                        _blendingTimeLeft = 0;
                        _blendingDuration = 0;
                        _previousClip = null;
                    }
                }

                SampleParamsAnimation();

                if (_nextAnimationTime > 0 + float.Epsilon && _playTime >= _nextAnimationTime)
                {
                    // TODO: Keep only the name or make a ChangeAnimation overload
                    var nextAnimation = _nextAnimation;
                    if (nextAnimation != null)
                    {
                        ChangeAnimation(nextAnimation);
                    }
                }
            }
            else if (_interpolateUntil > 0)
            {
                var allControllersReached = true;
                foreach (var target in Current.TargetControllers)
                {
                    var controllerReached = target.Interpolate(_playTime, InterpolationMaxDistanceDelta * UnityEngine.Time.deltaTime, InterpolationMaxAngleDelta * UnityEngine.Time.deltaTime);
                    if (!controllerReached) allControllersReached = false;
                }

                if (allControllersReached || UnityEngine.Time.time >= _interpolateUntil)
                {
                    _interpolateUntil = 0;
                    if (_playQueuedAfterInterpolation)
                    {
                        _playQueuedAfterInterpolation = false;
                        Play();
                    }
                    else
                    {
                        SampleParamsAnimation();
                        SampleControllers();
                    }
                }
            }
        }

        public void Sample()
        {
            if (_isPlaying) return;
            SampleParamsAnimation();
            SampleControllers();
        }

        private void SampleControllers()
        {
            if (_animState == null)
                return;

            _animState.enabled = true;
            _animState.weight = 1f;
            _animation.Sample();
            _animState.enabled = false;
        }

        public void Stop()
        {
            _playQueuedAfterInterpolation = false;
            _isPlaying = false;
            if (Current == null) return;
            _animation.Stop();
            foreach (var clip in Clips)
            {
                if (clip.AnimationPattern)
                {
                    clip.AnimationPattern.SetBoolParamValue("loopOnce", true);
                }
            }
            _blendingTimeLeft = 0;
            _blendingDuration = 0;
            _previousClip = null;
            _nextAnimation = null;
            _nextAnimationTime = 0;
            SampleParamsAnimation();
            if (PlayedAnimation != null && PlayedAnimation != Current.AnimationName)
            {
                if (Clips.Any(c => c.AnimationName == PlayedAnimation))
                    ChangeAnimation(PlayedAnimation);
                PlayedAnimation = null;
            }
        }

        public bool IsPlaying()
        {
            return _isPlaying || _playQueuedAfterInterpolation;
        }

        public bool IsInterpolating()
        {
            return _interpolateUntil > 0;
        }

        public void RebuildAnimation()
        {
            if (Current == null) throw new NullReferenceException("No current animation set");
            var time = Time.Snap();
            foreach (var clip in Clips)
            {
                if (clip.Transition)
                {
                    var previous = Clips.FirstOrDefault(c => c.NextAnimationName == clip.AnimationName);
                    if (previous != null)
                        clip.Paste(0f, previous.Copy(previous.AnimationLength, true));
                    var next = Clips.FirstOrDefault(c => c.AnimationName == clip.NextAnimationName);
                    if (next != null)
                        clip.Paste(clip.AnimationLength, next.Copy(0f, true));
                }
                clip.Validate();
                RebuildClipCurve(clip);
                _animation.AddClip(clip.Clip, clip.AnimationName);
                var animState = _animation[clip.AnimationName];
                if (animState != null)
                {
                    animState.layer = _layer;
                    animState.weight = 1;
                    animState.wrapMode = clip.Loop ? WrapMode.Loop : WrapMode.Once;
                    animState.speed = _speed;
                }
            }
            if (HasAnimatableControllers())
            {
                // This is a ugly hack, otherwise the scrubber won't work after modifying a frame
                _animation.Play(Current.AnimationName);
                if (!_isPlaying)
                    _animation.Stop(Current.AnimationName);
                _animState = _animation[Current.AnimationName];
                if (_animState != null)
                {
                    _animState.time = time;
                }
                else
                {
                    SuperController.LogError($"VamTimeline.{nameof(RebuildAnimation)}: Could not find animation {Current.AnimationName}");
                }
            }
            else
            {
                _animState = null;
            }
        }

        private void RebuildClipCurve(AtomAnimationClip clip)
        {
            clip.Clip.ClearCurves();

            foreach (var target in clip.TargetControllers)
            {
                if (target.Storables.Any(s => s.animationDirty))
                {
                    target.Validate();

                    if (clip.Loop)
                        target.SetCurveSnapshot(clip.AnimationLength, target.GetCurveSnapshot(0f));

                    target.ReapplyCurveTypes();

                    if (clip.Loop)
                        target.SmoothLoop();

                    if (clip.EnsureQuaternionContinuity)
                        clip.EnsureQuaternionContinuityAndRecalculateSlope();

                    foreach (var s in target.Storables)
                        s.animationDirty = false;
                }

                target.ReapplyCurvesToClip(clip.Clip);
            }

            foreach (var target in clip.TargetFloatParams)
            {
                if (target.StorableValue.animationDirty)
                {
                    if (clip.Loop)
                    {
                        target.SetKeyframe(clip.AnimationLength, target.Value[0].value);
                    }
                    target.Value.FlatAllFrames();

                    target.StorableValue.animationDirty = false;
                }
            }
        }

        private bool HasAnimatableControllers()
        {
            return Current.TargetControllers.Count > 0;
        }

        public AtomAnimationClip AddAnimation()
        {
            string animationName = GetNewAnimationName();
            var clip = new AtomAnimationClip(animationName);
            AddClip(clip);
            return clip;
        }

        public void ChangeAnimation(string animationName)
        {
            var clip = Clips.FirstOrDefault(c => c.AnimationName == animationName);
            if (clip == null) throw new NullReferenceException($"Could not find animation '{animationName}'. Found animations: '{string.Join("', '", Clips.Select(c => c.AnimationName).ToArray())}'.");
            var targetAnim = _animation[animationName];
            var time = Time;
            if (_isPlaying)
            {
                if (HasAnimatableControllers())
                {
                    targetAnim.time = 0f;
                    targetAnim.enabled = true;
                    targetAnim.weight = 0f;
                    _animation.Blend(Current.AnimationName, 0f, Current.BlendDuration);
                    _animation.Blend(animationName, 1f, Current.BlendDuration);
                }
                if (Current.AnimationPattern != null)
                {
                    // Let the loop finish during the transition
                    Current.AnimationPattern.SetBoolParamValue("loopOnce", true);
                }
                _previousClip = Current;
                _blendingTimeLeft = _blendingDuration = Current.BlendDuration;
            }

            Current = clip;
            _animState = targetAnim;

            if (_isPlaying)
            {
                DetermineNextAnimation(_playTime);
            }
            else
                Time = 0f;

            if (_isPlaying && Current.AnimationPattern != null)
            {
                Current.AnimationPattern.SetBoolParamValue("loopOnce", false);
                Current.AnimationPattern.ResetAndPlay();
            }
        }

        public Dictionary<string, AnimationCurve> GetCurvesDictionary()
        {
            // TODO: List of named curves instead of an actual dictionary (we don't need the hash tree)
            // TODO: Reuse the dictionary unless controllers are added or removed (list dirty flag)
            var dict = new Dictionary<string, AnimationCurve>();
            foreach (var target in Current.GetAllOrSelectedControllerTargets())
            {
                // TODO: Add rotation w value too?
                dict.Add($"{target.Name}.x", target.X);
                dict.Add($"{target.Name}.y", target.Y);
                dict.Add($"{target.Name}.z", target.Z);
            }
            foreach (var target in Current.GetAllOrSelectedFloatParamTargets())
            {
                dict.Add($"{target.Name}", target.Value);
            }
            return dict;
        }
    }
}