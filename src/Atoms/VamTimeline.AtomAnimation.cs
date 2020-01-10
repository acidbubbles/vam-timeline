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
    public class AtomAnimation
    {
        public const float PaddingBeforeLoopFrame = 0.001f;

        private readonly Atom _atom;
        private readonly Animation _animation;
        private AnimationState _animState;
        private bool _isPlaying;
        private float _playTime;
        private AtomAnimationClip _blendingClip;
        private float _blendingTimeLeft;
        private float _blendingDuration;
        private string _nextAnimation;
        private float _nextAnimationTime;

        public List<AtomAnimationClip> Clips { get; } = new List<AtomAnimationClip>();
        public AtomAnimationClip Current { get; set; }
        public string PlayedAnimation { get; private set; }

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
                {
                    if (!_isPlaying)
                    {
                        _animState.enabled = true;
                        _animState.weight = 1f;
                    }
                    _animState.time = value;
                    if (!_isPlaying)
                    {
                        _animation.Sample();
                        _animState.enabled = false;
                    }
                }
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
                AddClip(new AtomAnimationClip("Anim1"));
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
            var lastAnimationName = Clips.Last().AnimationName;
            var lastAnimationIndex = lastAnimationName.Substring(4);
            var animationName = "Anim" + (int.Parse(lastAnimationIndex) + 1);
            return animationName;
        }

        public void DeleteFrame()
        {
            Current.DeleteFrame(Time);
            RebuildAnimation();
        }

        public FreeControllerAnimationTarget Add(FreeControllerV3 controller)
        {
            var added = Current.Add(controller);
            if (added != null)
            {
                added.SetKeyframeToCurrentTransform(0f);
                added.SetKeyframeToCurrentTransform(Current.AnimationLength);
                RebuildAnimation();
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
                RebuildAnimation();
            }
            return added;
        }

        public void SetKeyframe(FloatParamAnimationTarget target, float time, float val)
        {
            if (time > Current.AnimationLength)
                time = Current.AnimationLength;
            target.SetKeyframe(time, val);
            RebuildAnimation();
        }

        public void SetKeyframeToCurrentTransform(FreeControllerAnimationTarget target, float time)
        {
            if (time > Current.AnimationLength)
                time = Current.AnimationLength;
            target.SetKeyframeToCurrentTransform(time);
            RebuildAnimation();
        }

        public void Remove(FreeControllerV3 controller)
        {
            Current.Remove(controller);
            RebuildAnimation();
        }

        public void Play()
        {
            if (Current == null) return;
            PlayedAnimation = Current.AnimationName;
            _isPlaying = true;
            _playTime = 0;
            if (_animState != null)
            {
                _animState.time = 0;
                _animation.Play(Current.AnimationName);
            }
            if (Current.AnimationPattern)
            {
                Current.AnimationPattern.SetBoolParamValue("loopOnce", false);
                Current.AnimationPattern.ResetAndPlay();
            }
            DetermineNextAnimation(0f);
        }

        private void DetermineNextAnimation(float time)
        {
            _nextAnimation = null;
            _nextAnimationTime = 0;

            if (Current.NextAnimationName == null) return;

            if (Current.NextAnimationTime > 0)
                _nextAnimationTime = time + Current.NextAnimationTime;
            else
                return;

            _nextAnimation = Current.NextAnimationName;
        }

        private void SampleParamsAnimation()
        {
            var time = Time;
            foreach (var morph in Current.TargetFloatParams)
            {
                var val = morph.Value.Evaluate(time);
                if (_blendingClip != null)
                {
                    var blendingTarget = _blendingClip.TargetFloatParams.FirstOrDefault(t => t.FloatParam == morph.FloatParam);
                    if (blendingTarget != null)
                    {
                        var weight = _blendingTimeLeft / _blendingDuration;
                        morph.FloatParam.val = (blendingTarget.Value.Evaluate(time) * (weight)) + (val * (1 - weight));
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
                _playTime += UnityEngine.Time.deltaTime * Speed;

                if (_blendingClip != null)
                {
                    _blendingTimeLeft -= UnityEngine.Time.deltaTime;
                    if (_blendingTimeLeft <= 0)
                    {
                        _blendingTimeLeft = 0;
                        _blendingDuration = 0;
                        _blendingClip = null;
                    }
                }

                SampleParamsAnimation();
            }

            if (_nextAnimationTime > 0 && _playTime >= _nextAnimationTime)
            {
                // TODO: Keep only the name or make a ChangeAnimation overload
                var nextAnimation = _nextAnimation;
                if (nextAnimation != null)
                {
                    ChangeAnimation(nextAnimation);
                }
            }
        }

        public void Stop()
        {
            if (Current == null) return;
            _isPlaying = false;
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
            _blendingClip = null;
            _nextAnimation = null;
            _nextAnimationTime = 0;
            SampleParamsAnimation();
            if (PlayedAnimation != null && PlayedAnimation != Current.AnimationName)
            {
                ChangeAnimation(PlayedAnimation);
                PlayedAnimation = null;
            }
        }

        public bool IsPlaying()
        {
            return _isPlaying;
        }

        public void RebuildAnimation()
        {
            if (Current == null) throw new NullReferenceException("No current animation set");
            var time = Time;
            foreach (var clip in Clips)
            {
                RebuildClipCurve(clip);
                _animation.AddClip(clip.Clip, clip.AnimationName);
                var animState = _animation[clip.AnimationName];
                if (animState != null)
                {
                    animState.wrapMode = clip.Loop ? WrapMode.Loop : WrapMode.Once;
                    animState.speed = clip.Speed;
                }
            }
            if (HasAnimatableControllers())
            {
                // This is a ugly hack, otherwise the scrubber won't work after modifying a frame
                _animation.Play(Current.AnimationName);
                _animation.Stop(Current.AnimationName);
                _animState = _animation[Current.AnimationName];
                if (_animState != null)
                {
                    _animState.time = time;
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
                if (clip.Loop)
                {
                    // TODO: Extract this since we may need to smooth between two animations too
                    target.SetCurveSnapshot(clip.AnimationLength, target.GetCurveSnapshot(0f));
                    target.SmoothLoop();
                }
                target.ReapplyCurvesToClip(clip.Clip);
            }
            if (clip.EnsureQuaternionContinuity)
                clip.Clip.EnsureQuaternionContinuity();

            foreach (var target in clip.TargetFloatParams)
            {
                if (clip.Loop)
                {
                    target.SetKeyframe(clip.AnimationLength, target.Value.keys[0].value);
                }
                target.Value.FlatAllFrames();
            }
        }

        private bool HasAnimatableControllers()
        {
            return Current.TargetControllers.Count > 0;
        }

        public void ChangeCurve(string curveType)
        {
            var time = Time;
            Current.ChangeCurve(time, curveType);
            RebuildAnimation();
        }

        public string AddAnimation()
        {
            string animationName = GetNewAnimationName();
            var clip = new AtomAnimationClip(animationName);
            CopyCurrentClipStateTo(clip);
            AddClip(clip);
            return animationName;
        }

        private void CopyCurrentClipStateTo(AtomAnimationClip clip)
        {
            clip.Speed = Current.Speed;
            clip.CropOrExtendLength(Current.AnimationLength);
            foreach (var origTarget in Current.TargetControllers)
            {
                var newTarget = clip.Add(origTarget.Controller);
                newTarget.SetKeyframeToCurrentTransform(0f);
                newTarget.SetKeyframeToCurrentTransform(clip.AnimationLength);
            }
            foreach (var origTarget in Current.TargetFloatParams)
            {
                var newTarget = clip.Add(origTarget.Storable, origTarget.FloatParam);
                newTarget.SetKeyframe(0f, origTarget.FloatParam.val);
                newTarget.SetKeyframe(clip.AnimationLength, origTarget.FloatParam.val);
            }
        }

        public void ChangeAnimation(string animationName)
        {
            var clip = Clips.FirstOrDefault(c => c.AnimationName == animationName);
            if (clip == null) throw new NullReferenceException($"Could not find animation '{animationName}'");
            var time = Time;
            if (_isPlaying)
            {
                if (HasAnimatableControllers())
                {
                    var targetAnim = _animation[animationName];
                    targetAnim.time = 0f;
                    _animation.Blend(Current.AnimationName, 0f, Current.BlendDuration);
                    _animation.Blend(animationName, 1f, Current.BlendDuration);
                }
                if (Current.AnimationPattern != null)
                {
                    // Let the loop finish during the transition
                    Current.AnimationPattern.SetBoolParamValue("loopOnce", true);
                }
                if (_blendingClip != null)
                {
                    // TODO: Fade multiple blending clips
                    // For morphs that won't be continued, immediately apply the last value
                    foreach (var morph in Current.TargetFloatParams.Where(t => !clip.TargetFloatParams.Any(ct => t.FloatParam == ct.FloatParam)))
                        morph.FloatParam.val = morph.Value.Evaluate(morph.Value.keys[morph.Value.keys.Length - 1].time);
                }
                _blendingClip = Current;
                _blendingTimeLeft = _blendingDuration = Current.BlendDuration;
            }

            Current = clip;
            _animState = _animation[Current.AnimationName];

            if (_isPlaying)
            {
                _animState.enabled = true;
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

        public void SmoothAllFrames()
        {
            Current.SmoothAllFrames();
            RebuildAnimation();
        }

        public AtomClipboardEntry Copy()
        {
            var time = Time;

            var controllers = new List<FreeControllerV3ClipboardEntry>();
            foreach (var controller in Current.GetAllOrSelectedControllerTargets())
            {
                var snapshot = controller.GetCurveSnapshot(time);
                if (snapshot == null) continue;
                controllers.Add(new FreeControllerV3ClipboardEntry
                {
                    Controller = controller.Controller,
                    Snapshot = snapshot
                });
            }
            var floatParams = new List<FloatParamValClipboardEntry>();
            foreach (var target in Current.GetAllOrSelectedFloatParamTargets())
            {
                if (!target.Value.keys.Any(k => k.time == time)) continue;
                floatParams.Add(new FloatParamValClipboardEntry
                {
                    Storable = target.Storable,
                    FloatParam = target.FloatParam,
                    Snapshot = target.Value.keys.First(k => k.time == time)
                });
            }
            return new AtomClipboardEntry
            {
                Controllers = controllers,
                FloatParams = floatParams
            };
        }

        public void Paste(AtomClipboardEntry clipboard)
        {
            float time = Time;
            if (Current.Loop && time >= Current.AnimationLength)
                time = 0f;
            foreach (var entry in clipboard.Controllers)
            {
                var animController = Current.TargetControllers.FirstOrDefault(c => c.Controller == entry.Controller);
                if (animController == null)
                    animController = Add(entry.Controller);
                animController.SetCurveSnapshot(time, entry.Snapshot);
                if (time == 0f && Current.Loop)
                    animController.SetCurveSnapshot(Current.AnimationLength, entry.Snapshot);
            }
            foreach (var entry in clipboard.FloatParams)
            {
                var animController = Current.TargetFloatParams.FirstOrDefault(c => c.FloatParam == entry.FloatParam);
                if (animController == null)
                    animController = Current.Add(entry.Storable, entry.FloatParam);
                animController.SetKeyframe(time, entry.Snapshot.value);
                if (time == 0f && Current.Loop)
                    animController.SetKeyframe(Current.AnimationLength, entry.Snapshot.value);
            }
            RebuildAnimation();
        }
    }
}
