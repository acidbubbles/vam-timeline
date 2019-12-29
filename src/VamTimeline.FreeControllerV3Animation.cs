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
        private float _animationLength;
        public FreeControllerV3 Controller;
        public AnimationCurve X = new AnimationCurve();
        public AnimationCurve Y = new AnimationCurve();
        public AnimationCurve Z = new AnimationCurve();
        public AnimationCurve RotX = new AnimationCurve();
        public AnimationCurve RotY = new AnimationCurve();
        public AnimationCurve RotZ = new AnimationCurve();
        public AnimationCurve RotW = new AnimationCurve();
        public List<AnimationCurve> Curves;
        public List<AnimationCurve> PositionCurves;
        public List<AnimationCurve> RotationCurves;

        public FreeControllerV3Animation(FreeControllerV3 controller, float animationLength)
        {
            Curves = new List<AnimationCurve> {
                X, Y, Z, RotX, RotY, RotZ, RotW
            };
            PositionCurves = new List<AnimationCurve> {
                X, Y, Z
            };
            RotationCurves = new List<AnimationCurve> {
                RotX, RotY, RotZ, RotW
            };
            Controller = controller;
            _animationLength = animationLength;
            SetKey(0f, controller.transform.localPosition, controller.transform.localRotation);
            SetKey(_animationLength, controller.transform.localPosition, controller.transform.localRotation);
        }

        private void UpdateCurves(AnimationClip clip)
        {
            var path = GetRelativePath();
            clip.SetCurve(path, typeof(Transform), "localPosition.x", X);
            clip.SetCurve(path, typeof(Transform), "localPosition.y", Y);
            clip.SetCurve(path, typeof(Transform), "localPosition.z", Z);
            clip.SetCurve(path, typeof(Transform), "localRotation.x", RotX);
            clip.SetCurve(path, typeof(Transform), "localRotation.y", RotY);
            clip.SetCurve(path, typeof(Transform), "localRotation.z", RotZ);
            clip.SetCurve(path, typeof(Transform), "localRotation.w", RotW);
        }

        private string GetRelativePath()
        {
            var root = Controller.containingAtom.transform;
            var target = Controller.transform;
            var parts = new List<string>();
            Transform t = target;
            while (t != root && t != t.root)
            {
                parts.Add(t.name);
                t = t.parent;
            }
            parts.Reverse();
            return string.Join("/", parts.ToArray());
        }

        public void SetLength(float length)
        {
            foreach (var curve in Curves)
            {
                if (length > _animationLength)
                {
                    for (var i = 0; i < curve.keys.Length - 1; i++)
                    {
                        if (curve.keys[i].time < length) continue;
                        curve.RemoveKey(i);
                    }
                }

                var last = curve.keys[curve.keys.Length - 1];
                last.time = length;
                curve.MoveKey(curve.keys.Length - 1, last);
            }
            _animationLength = length;
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
        }

        public void RebuildAnimation(AnimationClip clip)
        {
            // Smooth loop
            foreach (var curve in Curves)
            {
                if (curve.keys.Length <= 2) continue;
                curve.SmoothTangents(0, 0f);
                curve.SmoothTangents(curve.keys.Length - 1, 0f);
                var first = curve.keys[0];
                var last = curve.keys[curve.keys.Length - 1];
                var tangent = (first.inTangent + last.outTangent) / 2f;
                first.inTangent = tangent;
                first.outTangent = tangent;
                last.inTangent = tangent;
                last.outTangent = tangent;
                curve.MoveKey(0, first);
                curve.MoveKey(curve.keys.Length - 1, last);
            }
            UpdateCurves(clip);
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
