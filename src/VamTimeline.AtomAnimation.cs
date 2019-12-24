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
        private static class CurveTypeValues
        {
            public const string Flat = "Flat";
            public const string Linear = "Linear";
            public const string Smooth = "Smooth";
            public const string Bounce = "Bounce";
        }

        private readonly Atom _atom;
        public readonly Animation Animation;
        public readonly AnimationClip Clip;
        public readonly string AnimationName = "Anim1";
        private float _animationLength = 5f;
        public readonly List<FreeControllerV3Animation> Controllers = new List<FreeControllerV3Animation>();
        private FreeControllerV3Animation _selected;

        public readonly List<string> CurveTypes = new List<string> { CurveTypeValues.Flat, CurveTypeValues.Linear, CurveTypeValues.Smooth, CurveTypeValues.Bounce };

        public AtomAnimation(Atom atom)
        {
            _atom = atom;
            Animation = _atom.gameObject.GetComponent<Animation>() ?? _atom.gameObject.AddComponent<Animation>();
            Clip = new AnimationClip();
            // TODO: Make that an option in the UI
            Clip.wrapMode = WrapMode.Loop;
            Clip.legacy = true;
            Animation.AddClip(Clip, AnimationName);
        }

        public FreeControllerV3Animation Add(FreeControllerV3 controller)
        {
            if (Controllers.Any(c => c.Controller == controller)) return null;
            FreeControllerV3Animation controllerState = new FreeControllerV3Animation(controller, AnimationLength);
            Controllers.Add(controllerState);
            RebuildAnimation();
            return controllerState;
        }

        public void Remove(FreeControllerV3 controller)
        {
            var existing = Controllers.FirstOrDefault(c => c.Controller == controller);
            if (existing == null) return;
            Controllers.Remove(existing);
            RebuildAnimation();
        }

        public float AnimationLength
        {
            get
            {
                return _animationLength;
            }
            set
            {
                if (value == _animationLength)
                    return;
                _animationLength = value;
                foreach (var controller in Controllers)
                {
                    controller.SetLength(value);
                }
                RebuildAnimation();
            }
        }

        public void Play()
        {
            AnimationState animState = Animation[AnimationName];
            animState.time = 0;
            Animation.Play(AnimationName);
        }

        internal void Stop()
        {
            Animation.Stop(AnimationName);
            SetTime(0);
        }

        public float Speed
        {
            get
            {
                AnimationState animState = Animation[AnimationName];
                return animState.speed;
            }

            set
            {
                AnimationState animState = Animation[AnimationName];
                animState.speed = value;
            }
        }

        public void SetTime(float time)
        {
            var animState = Animation[AnimationName];
            animState.time = time;
            if (!animState.enabled)
            {
                // TODO: Can we set this once?
                animState.enabled = true;
                Animation.Sample();
                animState.enabled = false;
            }
        }

        public void SelectControllerByName(string val)
        {
            _selected = string.IsNullOrEmpty(val)
                ? null
                : Controllers.FirstOrDefault(c => c.Controller.name == val);
        }

        public List<string> GetControllersName()
        {
            return Controllers.Select(c => c.Controller.name).ToList();
        }

        public void PauseToggle()
        {
            var animState = Animation[AnimationName];
            animState.enabled = !animState.enabled;
        }

        public bool IsPlaying()
        {
            return Animation.IsPlaying(AnimationName);
        }

        public float GetTime()
        {
            var animState = Animation[AnimationName];
            if (animState == null) return 0f;
            return animState.time % animState.length;
        }

        public void NextFrame()
        {
            var time = GetTime();
            // TODO: Hardcoded loop length
            var nextTime = AnimationLength;
            foreach (var controller in GetAllOrSelectedControllers())
            {
                var controllerNextTime = controller.X.keys.FirstOrDefault(k => k.time > time).time;
                if (controllerNextTime != 0 && controllerNextTime < nextTime) nextTime = controllerNextTime;
            }
            if (nextTime == AnimationLength)
                SetTime(0f);
            else
                SetTime(nextTime);
        }

        public void PreviousFrame()
        {
            var time = GetTime();
            var previousTime = 0f;
            foreach (var controller in GetAllOrSelectedControllers())
            {
                var controllerNextTime = controller.X.keys.LastOrDefault(k => k.time < time).time;
                if (controllerNextTime != 0 && controllerNextTime > previousTime) previousTime = controllerNextTime;
            }
            if (previousTime == 0f)
                // TODO: Instead, move to the last frame
                SetTime(0f);
            else
                SetTime(previousTime);
        }

        public void DeleteFrame()
        {
            var time = GetTime();
            foreach (var controller in GetAllOrSelectedControllers())
            {
                foreach (var curve in controller.Curves)
                {
                    var key = Array.FindIndex(curve.keys, k => k.time == time);
                    if (key != -1) curve.RemoveKey(key);
                }
            }
            RebuildAnimation();
        }

        public void RebuildAnimation()
        {
            var time = Animation[AnimationName].time;
            Clip.ClearCurves();
            foreach (var controller in Controllers)
            {
                controller.RebuildAnimation(Clip);
            }
            Animation.AddClip(Clip, AnimationName);
            Clip.EnsureQuaternionContinuity();
            // TODO: This is a ugly hack, otherwise the scrubber won't work after modifying a frame
            Animation.Play(AnimationName);
            Animation.Stop(AnimationName);
            Animation[AnimationName].time = time;
        }

        public IEnumerable<FreeControllerV3Animation> GetAllOrSelectedControllers()
        {
            if (_selected != null) return new[] { _selected };
            return Controllers;
        }

        public void ChangeCurve(string val)
        {
            var time = GetTime();
            if (_selected == null) return;
            if (time == 0 || time == AnimationLength) return;

            switch (val)
            {
                case null:
                case "":
                    return;
                case CurveTypeValues.Flat:
                    foreach (var curve in _selected.Curves)
                    {
                        var key = Array.FindIndex(curve.keys, k => k.time == time);
                        if (key == -1) return;
                        var keyframe = curve.keys[key];
                        keyframe.inTangent = 0f;
                        keyframe.outTangent = 0f;
                        curve.MoveKey(key, keyframe);
                    }
                    break;
                case CurveTypeValues.Linear:
                    foreach (var curve in _selected.Curves)
                    {
                        var key = Array.FindIndex(curve.keys, k => k.time == time);
                        if (key == -1) return;
                        var before = curve.keys[key - 1];
                        var keyframe = curve.keys[key];
                        var next = curve.keys[key + 1];
                        keyframe.inTangent = CalculateLinearTangent(before, keyframe);
                        keyframe.outTangent = CalculateLinearTangent(keyframe, next);
                        curve.MoveKey(key, keyframe);
                    }
                    break;
                case CurveTypeValues.Bounce:
                    foreach (var curve in _selected.Curves)
                    {
                        var key = Array.FindIndex(curve.keys, k => k.time == time);
                        if (key == -1) return;
                        var before = curve.keys[key - 1];
                        var keyframe = curve.keys[key];
                        var next = curve.keys[key + 1];
                        keyframe.inTangent = CalculateLinearTangent(before, keyframe);
                        if (keyframe.inTangent > 0)
                            keyframe.inTangent = 0.8f;
                        else if (keyframe.inTangent < 0)
                            keyframe.inTangent = -0.8f;
                        else
                            keyframe.inTangent = 0;
                        keyframe.outTangent = CalculateLinearTangent(keyframe, next);
                        if (keyframe.outTangent > 0)
                            keyframe.outTangent = 0.8f;
                        else if (keyframe.outTangent < 0)
                            keyframe.outTangent = -0.8f;
                        else
                            keyframe.outTangent = 0;
                        curve.MoveKey(key, keyframe);
                    }
                    break;
                case CurveTypeValues.Smooth:
                    foreach (var curve in _selected.Curves)
                    {
                        var key = Array.FindIndex(curve.keys, k => k.time == time);
                        if (key == -1) return;
                        curve.SmoothTangents(key, 0f);
                    };
                    break;
                default:
                    throw new NotSupportedException($"Curve type {val} is not supported");
            }
            RebuildAnimation();
        }

        private static float CalculateLinearTangent(Keyframe from, Keyframe to)
        {
            return (float)((from.value - (double)to.value) / (from.time - (double)to.time));
        }
    }
}
