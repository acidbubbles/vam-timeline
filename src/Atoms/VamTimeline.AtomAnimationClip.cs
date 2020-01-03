using System;
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
    public class AtomAnimationClip : AnimationClipBase<FreeControllerV3AnimationTarget>, IAnimationClip<FreeControllerV3AnimationTarget>
    {
        public readonly AnimationClip Clip;
        public AnimationPattern AnimationPattern;

        public AtomAnimationClip(string animationName)
            : base(animationName)
        {
            Clip = new AnimationClip
            {
                wrapMode = WrapMode.Loop,
                legacy = true
            };
        }

        public FreeControllerV3AnimationTarget Add(FreeControllerV3 controller)
        {
            if (Targets.Any(c => c.Controller == controller)) return null;
            FreeControllerV3AnimationTarget controllerState = new FreeControllerV3AnimationTarget(controller, AnimationLength);
            controllerState.SetKeyframeToCurrentTransform(0f);
            Targets.Add(controllerState);
            return controllerState;
        }

        public void Remove(FreeControllerV3 controller)
        {
            var existing = Targets.FirstOrDefault(c => c.Controller == controller);
            if (existing == null) return;
            Targets.Remove(existing);
        }

        public void RebuildAnimation()
        {
            Clip.ClearCurves();
            foreach (var controller in Targets)
            {
                controller.ReapplyCurvesToClip(Clip);
            }
            Clip.EnsureQuaternionContinuity();
        }

        public void ChangeCurve(float time, string curveType)
        {
            if (time == 0 || time == AnimationLength) return;

            foreach (var controller in GetAllOrSelectedTargets())
            {
                controller.ChangeCurve(time, curveType);
            }
        }

        public void SmoothAllFrames()
        {
            foreach (var controller in Targets)
            {
                controller.SmoothAllFrames();
            }
        }
    }
}
