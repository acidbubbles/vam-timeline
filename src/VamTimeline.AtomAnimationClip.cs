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
    public class AtomAnimationClip
    {
        public readonly AnimationClip Clip;
        private float _animationLength = 5f;
        public readonly List<FreeControllerV3Animation> Controllers = new List<FreeControllerV3Animation>();
        public AnimationPattern AnimationPattern;
        private FreeControllerV3Animation _selected;

        public string AnimationName { get; }
        public float Speed { get; set; } = 1f;

        public AtomAnimationClip(string animationName)
        {
            AnimationName = animationName;
            Clip = new AnimationClip
            {
                // TODO: Make that an option in the UI
                wrapMode = WrapMode.Loop,
                legacy = true
            };
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
                controller.ReapplyCurvesToClip(Clip);
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
            if (time == 0 || time == AnimationLength) return;

            foreach (var controller in GetAllOrSelectedControllers())
            {
                controller.ChangeCurve(time, val);
            }
        }

        public void SmoothAllFrames()
        {
            foreach (var controller in Controllers)
            {
                controller.SmoothAllFrames();
            }
        }
    }
}
