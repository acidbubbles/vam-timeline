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
    public class AtomAnimationClip
    {

        public readonly AnimationClip Clip;
        private float _animationLength = 5f;
        public readonly List<FreeControllerV3Animation> Controllers = new List<FreeControllerV3Animation>();
        // TODO: Replace this by a parameter so we can do the same from external tools
        private FreeControllerV3Animation _selected;

        public string AnimationName { get; }
        public float Speed { get; set; } = 1f;

        public AtomAnimationClip(string animationName)
        {
            AnimationName = animationName;
            Clip = new AnimationClip();
            // TODO: Make that an option in the UI
            Clip.wrapMode = WrapMode.Loop;
            Clip.legacy = true;
        }

        public FreeControllerV3Animation Add(FreeControllerV3 controller)
        {
            if (Controllers.Any(c => c.Controller == controller)) return null;
            FreeControllerV3Animation controllerState = new FreeControllerV3Animation(controller, AnimationLength);
            Controllers.Add(controllerState);
            return controllerState;
        }

        public void Remove(FreeControllerV3 controller)
        {
            var existing = Controllers.FirstOrDefault(c => c.Controller == controller);
            if (existing == null) return;
            Controllers.Remove(existing);
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

        public float GetNextFrame(float time)
        {
            var nextTime = AnimationLength;
            foreach (var controller in GetAllOrSelectedControllers())
            {
                var controllerNextTime = controller.X.keys.FirstOrDefault(k => k.time > time).time;
                if (controllerNextTime != 0 && controllerNextTime < nextTime) nextTime = controllerNextTime;
            }
            if (nextTime == AnimationLength)
                return 0f;
            else
                return nextTime;
        }

        public float GetPreviousFrame(float time)
        {
            if (time == 0f)
                return GetAllOrSelectedControllers().SelectMany(c => c.Curves).SelectMany(c => c.keys).Select(c => c.time).Where(t => t != AnimationLength).Max();
            var previousTime = 0f;
            foreach (var controller in GetAllOrSelectedControllers())
            {
                var controllerNextTime = controller.X.keys.LastOrDefault(k => k.time < time).time;
                if (controllerNextTime != 0 && controllerNextTime > previousTime) previousTime = controllerNextTime;
            }
            return previousTime;
        }

        public void DeleteFrame(float time)
        {
            foreach (var controller in GetAllOrSelectedControllers())
            {
                foreach (var curve in controller.Curves)
                {
                    var key = Array.FindIndex(curve.keys, k => k.time == time);
                    if (key != -1) curve.RemoveKey(key);
                }
            }
        }

        public void RebuildAnimation()
        {
            Clip.ClearCurves();
            foreach (var controller in Controllers)
            {
                controller.RebuildAnimation(Clip);
            }
            Clip.EnsureQuaternionContinuity();
        }

        public IEnumerable<FreeControllerV3Animation> GetAllOrSelectedControllers()
        {
            if (_selected != null) return new[] { _selected };
            return Controllers;
        }

        public void ChangeCurve(float time, string val)
        {
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
        }

        private static float CalculateLinearTangent(Keyframe from, Keyframe to)
        {
            return (float)((from.value - (double)to.value) / (from.time - (double)to.time));
        }
    }
}
