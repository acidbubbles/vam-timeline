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
        private readonly Atom _atom;
        private readonly Animation _animation;
        private AnimationState _animState;
        private AtomAnimationClip _blendingClip;
        private float _blendingTimeLeft;
        private float _blendingDuration;
        public List<AtomAnimationClip> Clips { get; } = new List<AtomAnimationClip>();
        public AtomAnimationClip Current { get; set; }

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
                if (Current == null) return;
                if (_animState == null) return;
                _animState.time = value;
                if (!_animState.enabled)
                {
                    _animState.enabled = true;
                    _animation.Sample();
                    _animState.enabled = false;
                }
                SampleAnimation();
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

        public float BlendDuration { get; set; } = 1f;

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
            _animation.Play(Current.AnimationName);
            if (Current.AnimationPattern)
            {
                Current.AnimationPattern.SetBoolParamValue("loopOnce", false);
                Current.AnimationPattern.ResetAndPlay();
            }
        }

        private void SampleAnimation()
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
            if (IsPlaying())
            {
                // _time = (_time + UnityEngine.Time.deltaTime * Speed) % AnimationLength;

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

                SampleAnimation();
            }
        }

        public void Stop()
        {
            if (Current == null || _animState == null) return;
            _animation.Stop();
            Time = 0;
            foreach (var clip in Clips)
            {
                if (clip.AnimationPattern)
                {
                    clip.AnimationPattern.SetBoolParamValue("loopOnce", true);
                    clip.AnimationPattern.ResetAnimation();
                }
            }
            _blendingTimeLeft = 0;
            _blendingDuration = 0;
            _blendingClip = null;
            SampleAnimation();
        }

        public bool IsPlaying()
        {
            return _animation.IsPlaying(Current.AnimationName);
        }

        public void RebuildAnimation()
        {
            if (Current == null) throw new NullReferenceException("No current animation set");
            var time = Time;
            foreach (var clip in Clips)
            {
                clip.RebuildAnimation();
                _animation.AddClip(clip.Clip, clip.AnimationName);
                var animState = _animation[clip.AnimationName];
                animState.speed = clip.Speed;
            }
            // This is a ugly hack, otherwise the scrubber won't work after modifying a frame
            _animation.Play(Current.AnimationName);
            _animation.Stop(Current.AnimationName);
            _animState = _animation[Current.AnimationName];
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
            string animationName = GetNewAnimationName();
            var clip = new AtomAnimationClip(animationName);
            CopyCurrentValues(clip);
            AddClip(clip);
            return animationName;
        }

        private void CopyCurrentValues(AtomAnimationClip clip)
        {
            clip.Speed = Speed;
            clip.AnimationLength = AnimationLength;
            foreach (var controller in Current.TargetControllers.Select(c => c.Controller))
            {
                var animController = clip.Add(controller);
                animController.SetKeyframeToCurrentTransform(0f);
            }
            foreach (var target in Current.TargetFloatParams)
            {
                var animController = clip.Add(target.Storable, target.FloatParam);
                animController.SetKeyframe(0f, target.FloatParam.val);
            }
        }

        public void ChangeAnimation(string animationName)
        {
            var anim = Clips.FirstOrDefault(c => c.AnimationName == animationName);
            if (anim == null) return;
            var time = Time;
            var isPlaying = IsPlaying();
            if (isPlaying)
            {
                _animation.Blend(Current.AnimationName, 0f, BlendDuration);
                _animation.Blend(animationName, 1f, BlendDuration);
                if (Current.AnimationPattern != null)
                {
                    // Let the loop finish during the transition
                    Current.AnimationPattern.SetBoolParamValue("loopOnce", true);
                }
                _blendingClip = Current;
                _blendingTimeLeft = _blendingDuration = BlendDuration;
            }
            Current.SelectTargetByName("");
            Current = anim;
            if (!isPlaying)
            {
                Play();
                Stop();
                Time = 0f;
                SampleAnimation();
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

        public AtomClipboardEntry Copy()
        {
            var time = Time;

            var controllers = new List<FreeControllerV3ClipboardEntry>();
            foreach (var controller in Current.GetAllOrSelectedTargetsOfType<FreeControllerAnimationTarget>())
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
            foreach (var target in Current.GetAllOrSelectedTargetsOfType<FloatParamAnimationTarget>())
            {
                if (!target.Value.keys.Any(k => k.time == time)) continue;
                floatParams.Add(new FloatParamValClipboardEntry
                {
                    Storable = target.Storable,
                    FloatParam = target.FloatParam,
                    Snapshot = target.Value.keys.FirstOrDefault(k => k.time == time)
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
            foreach (var entry in clipboard.Controllers)
            {
                var animController = Current.TargetControllers.FirstOrDefault(c => c.Controller == entry.Controller);
                if (animController == null)
                    animController = Add(entry.Controller);
                animController.SetCurveSnapshot(time, entry.Snapshot);
            }
            foreach (var entry in clipboard.FloatParams)
            {
                var animController = Current.TargetFloatParams.FirstOrDefault(c => c.FloatParam == entry.FloatParam);
                if (animController == null)
                    animController = Current.Add(entry.Storable, entry.FloatParam);
                animController.SetKeyframe(time, entry.Snapshot.value);
            }
            RebuildAnimation();
        }
    }
}
