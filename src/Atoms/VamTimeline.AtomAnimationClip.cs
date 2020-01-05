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
        private float _animationLength = 5f;
        private IAnimationTarget _selected;
        public AnimationClip Clip { get; }
        public AnimationPattern AnimationPattern { get; set; }
        public readonly List<FloatParamAnimationTarget> TargetFloatParams = new List<FloatParamAnimationTarget>();
        public readonly List<FreeControllerAnimationTarget> TargetControllers = new List<FreeControllerAnimationTarget>();
        public IEnumerable<IAnimationTarget> AllTargets => TargetControllers.Cast<IAnimationTarget>().Concat(TargetFloatParams);
        public string AnimationName { get; }
        public float Speed { get; set; } = 1f;
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
                foreach (var target in AllTargets)
                {
                    target.SetLength(value);
                }
            }
        }

        public AtomAnimationClip(string animationName)
        {
            AnimationName = animationName;
            Clip = new AnimationClip
            {
                wrapMode = WrapMode.Loop,
                legacy = true
            };
        }

        public bool IsEmpty()
        {
            return AllTargets.Count() == 0;
        }

        public void SelectTargetByName(string val)
        {
            _selected = string.IsNullOrEmpty(val)
                ? null
                : AllTargets.FirstOrDefault(c => c.Name == val);
        }

        public IEnumerable<string> GetTargetsNames()
        {
            return AllTargets.Select(c => c.Name).ToList();
        }

        public FreeControllerAnimationTarget Add(FreeControllerV3 controller)
        {
            if (TargetControllers.Any(c => c.Controller == controller)) return null;
            FreeControllerAnimationTarget controllerState = new FreeControllerAnimationTarget(controller, AnimationLength);
            controllerState.SetKeyframeToCurrentTransform(0f);
            TargetControllers.Add(controllerState);
            return controllerState;
        }
        
        public FloatParamAnimationTarget Add(JSONStorable storable, JSONStorableFloat jsf)
        {
            if (TargetFloatParams.Any(s => s.Name == jsf.name)) return null;
            var target = new FloatParamAnimationTarget(storable, jsf, AnimationLength);
            Add(target);
            return target;
        }

        public void Add(FloatParamAnimationTarget target)
        {
            TargetFloatParams.Add(target);
        }

        public void Remove(FreeControllerV3 controller)
        {
            var existing = TargetControllers.FirstOrDefault(c => c.Controller == controller);
            if (existing == null) return;
            TargetControllers.Remove(existing);
        }

        public void RebuildAnimation()
        {
            Clip.ClearCurves();
            foreach (var controller in TargetControllers)
            {
                controller.ReapplyCurvesToClip(Clip);
            }
            // NOTE: This allows smoother rotation but cause weird looping issues in some cases. Better with than without though.
            Clip.EnsureQuaternionContinuity();
        }

        public void ChangeCurve(float time, string curveType)
        {
            if (time == 0 || time == AnimationLength) return;

            foreach (var controller in GetAllOrSelectedTargetsOfType<FreeControllerAnimationTarget>())
            {
                controller.ChangeCurve(time, curveType);
            }
        }

        public void SmoothAllFrames()
        {
            foreach (var controller in TargetControllers)
            {
                controller.SmoothAllFrames();
            }
        }

        public float GetNextFrame(float time)
        {
            var nextTime = AnimationLength;
            foreach (var controller in GetAllOrSelectedTargets())
            {
                var targetNextTime = controller.GetCurves().First().keys.FirstOrDefault(k => k.time > time).time;
                if (targetNextTime != 0 && targetNextTime < nextTime) nextTime = targetNextTime;
            }
            if (nextTime == AnimationLength)
                return 0f;
            else
                return nextTime;
        }

        public float GetPreviousFrame(float time)
        {
            if (time == 0f)
                return GetAllOrSelectedTargets().SelectMany(c => c.GetCurves()).SelectMany(c => c.keys).Select(c => c.time).Where(t => t != AnimationLength).Max();
            var previousTime = 0f;
            foreach (var controller in GetAllOrSelectedTargets())
            {
                var targetPreviousTime = controller.GetCurves().First().keys.LastOrDefault(k => k.time < time).time;
                if (targetPreviousTime != 0 && targetPreviousTime > previousTime) previousTime = targetPreviousTime;
            }
            return previousTime;
        }

        public void DeleteFrame(float time)
        {
            foreach (var target in GetAllOrSelectedTargets())
            {
                foreach (var curve in target.GetCurves())
                {
                    var key = Array.FindIndex(curve.keys, k => k.time == time);
                    if (key != -1) curve.RemoveKey(key);
                }
            }
        }

        public IEnumerable<IAnimationTarget> GetAllOrSelectedTargets()
        {
            if (_selected != null) return new IAnimationTarget[] { _selected };
            return AllTargets.Cast<IAnimationTarget>();
        }

        public IEnumerable<T> GetAllOrSelectedTargetsOfType<T>()
            where T : class, IAnimationTarget
        {
            if (_selected != null) return new T[] { _selected as T };
            return AllTargets.OfType<T>();
        }
    }
}
