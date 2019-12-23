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
    public class FreeControllerV3Animation
    {
        private readonly float _animationLength;
        private string _animationName;
        public FreeControllerV3 Controller;
        public readonly Animation Animation;
        public readonly AnimationClip Clip;
        public AnimationCurve X = new AnimationCurve();
        public AnimationCurve Y = new AnimationCurve();
        public AnimationCurve Z = new AnimationCurve();
        public AnimationCurve RotX = new AnimationCurve();
        public AnimationCurve RotY = new AnimationCurve();
        public AnimationCurve RotZ = new AnimationCurve();
        public AnimationCurve RotW = new AnimationCurve();
        public List<AnimationCurve> Curves;

        // TODO: Cache this, but if we do detect renames!
        public string Name => $"{Controller.containingAtom.name}/{Controller.name}";

        public FreeControllerV3Animation(FreeControllerV3 controller, string animationName, float animationLength)
        {
            Curves = new List<AnimationCurve> {
                X, Y, Z, RotX, RotY, RotZ, RotW
            };
            Controller = controller;
            _animationName = animationName;
            _animationLength = animationLength;
            // TODO: These should not be set internally, but rather by the initializer
            SetKey(0f, controller.transform.localPosition, controller.transform.localRotation);
            SetKey(_animationLength, controller.transform.localPosition, controller.transform.localRotation);

            Clip = new AnimationClip();
            // TODO: Make that an option in the UI
            Clip.wrapMode = WrapMode.Loop;
            Clip.legacy = true;
            UpdateCurves();

            Animation = controller.gameObject.GetComponent<Animation>() ?? controller.gameObject.AddComponent<Animation>();
            Animation.AddClip(Clip, _animationName);

            Animation.Play(_animationName);
            Animation.Stop(_animationName);
        }

        private void UpdateCurves()
        {
            Clip.ClearCurves();
            Clip.SetCurve("", typeof(Transform), "localPosition.x", X);
            Clip.SetCurve("", typeof(Transform), "localPosition.y", Y);
            Clip.SetCurve("", typeof(Transform), "localPosition.z", Z);
            Clip.SetCurve("", typeof(Transform), "localRotation.x", RotX);
            Clip.SetCurve("", typeof(Transform), "localRotation.y", RotY);
            Clip.SetCurve("", typeof(Transform), "localRotation.z", RotZ);
            Clip.SetCurve("", typeof(Transform), "localRotation.w", RotW);
            Clip.EnsureQuaternionContinuity();
        }

        public void SetKeyToCurrentPositionAndUpdate(float time)
        {
            if (time == 0f)
            {
                // TODO: Here we should also set the tangents
                SetKey(0f, Controller.transform.localPosition, Controller.transform.localRotation, (AnimationCurve c, ref Keyframe k) =>
                {
                    Keyframe last = c.keys.Last();
                    k.inTangent = last.inTangent;
                    k.outTangent = c.keys.Last().outTangent;
                });
                SetKey(_animationLength, Controller.transform.localPosition, Controller.transform.localRotation, (AnimationCurve c, ref Keyframe k) =>
                {
                    Keyframe first = c.keys.First();
                    k.inTangent = first.inTangent;
                    k.outTangent = c.keys.First().outTangent;
                });
            }
            else
            {
                SetKey(time, Controller.transform.localPosition, Controller.transform.localRotation);
            }
            // TODO: If the time is zero, also update the last frame!
            RebuildAnimation();
        }

        public void RebuildAnimation()
        {
            UpdateCurves();
            Animation.AddClip(Clip, _animationName);
        }

        public void SetKey(float time, Vector3 position, Quaternion rotation, KeyframeModify fn = null)
        {
            AddKey(X, time, position.x, fn);
            AddKey(Y, time, position.y, fn);
            AddKey(Z, time, position.z, fn);
            AddKey(RotX, time, rotation.x, fn);
            AddKey(RotY, time, rotation.y, fn);
            AddKey(RotZ, time, rotation.z, fn);
            AddKey(RotW, time, rotation.w, fn);
        }

        private static void AddKey(AnimationCurve curve, float time, float value, KeyframeModify fn = null)
        {
            var key = curve.AddKey(time, value);
            Keyframe keyframe;
            if (key == -1)
            {
                // TODO: If this returns -1, it means the key was not added. Maybe use MoveKey?
                key = Array.FindIndex(curve.keys, k => k.time == time);
                if (key == -1) throw new InvalidOperationException($"Cannot AddKey at time {time}, but no keys exist at this position");
                keyframe = curve.keys[key];
                keyframe.value = value;
                curve.MoveKey(key, keyframe);
            }
            if (fn != null)
            {
                keyframe = curve.keys[key];
                fn(curve, ref keyframe);
                curve.MoveKey(key, keyframe);
            }
        }
    }
}
