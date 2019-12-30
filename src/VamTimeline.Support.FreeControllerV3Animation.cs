using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AcidBubbles.VamTimeline
{
    /// <summary>
    /// VaM Timeline
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

        public void SetKeyToCurrentControllerTransform(float time)
        {
            SetKeyToTransform(time, Controller.transform.localPosition, Controller.transform.localRotation);
        }

        public void SetKeyToTransform(float time, Vector3 localPosition, Quaternion localRotation)
        {
            if (time == 0f)
            {
                SetKey(0f, localPosition, localRotation);
                SetKey(_animationLength, localPosition, localRotation);
            }
            else
            {
                SetKey(time, localPosition, localRotation);
            }
        }

        public void RebuildAnimation(AnimationClip clip)
        {
            // Smooth loop
            foreach (var curve in Curves)
            {
                if (curve.keys.Length <= 2) continue;
                var keyframe = curve.keys[0];
                var inTangent = CalculateLinearTangent(curve.keys[curve.keys.Length - 2].value, keyframe.value, curve.keys[curve.keys.Length - 2].time, curve.keys[curve.keys.Length - 1].time);
                var outTangent = CalculateLinearTangent(keyframe, curve.keys[1]);
                var tangent = inTangent + outTangent / 2f;
                keyframe.inTangent = tangent;
                keyframe.outTangent = tangent;
                keyframe.inWeight = 0.33f;
                keyframe.outWeight = 0.33f;
                curve.MoveKey(0, keyframe);

                keyframe.time = curve.keys[curve.keys.Length - 1].time;
                curve.MoveKey(curve.keys.Length - 1, keyframe);
            }
            UpdateCurves(clip);
        }

        public void SetKey(float time, Vector3 position, Quaternion rotation)
        {
            AddKey(X, time, position.x);
            AddKey(Y, time, position.y);
            AddKey(Z, time, position.z);
            AddKey(RotX, time, rotation.x);
            AddKey(RotY, time, rotation.y);
            AddKey(RotZ, time, rotation.z);
            AddKey(RotW, time, rotation.w);
        }

        private static void AddKey(AnimationCurve curve, float time, float value)
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
        }

        public FreeControllerV3Snapshot GetCurveSnapshot(float time)
        {
            return new FreeControllerV3Snapshot
            {
                X = X.keys.First(k => k.time == time),
                Y = Y.keys.First(k => k.time == time),
                Z = Z.keys.First(k => k.time == time),
                RotX = RotX.keys.First(k => k.time == time),
                RotY = RotY.keys.First(k => k.time == time),
                RotZ = RotZ.keys.First(k => k.time == time),
                RotW = RotW.keys.First(k => k.time == time),
            };
        }

        public void SetCurveSnapshot(float time, FreeControllerV3Snapshot snapshot)
        {
            SetKeySnapshot(time, X, snapshot.X);
            SetKeySnapshot(time, Y, snapshot.Y);
            SetKeySnapshot(time, Z, snapshot.Z);
            SetKeySnapshot(time, RotX, snapshot.RotX);
            SetKeySnapshot(time, RotY, snapshot.RotY);
            SetKeySnapshot(time, RotZ, snapshot.RotZ);
            SetKeySnapshot(time, RotW, snapshot.RotW);
        }

        private void SetKeySnapshot(float time, AnimationCurve curve, Keyframe keyframe)
        {
            var index = Array.FindIndex(curve.keys, k => k.time == time);
            if (index == -1)
                index = curve.AddKey(time, keyframe.value);
            keyframe.time = time;
            curve.MoveKey(index, keyframe);

            if (time == 0f)
            {
                keyframe.time = curve.keys[curve.keys.Length - 1].time;
                curve.MoveKey(curve.keys.Length - 1, keyframe);
            }
        }

        public void ChangeCurve(float time, string val)
        {
            if (string.IsNullOrEmpty(val)) return;
            foreach (var curve in Curves)
            {
                var key = Array.FindIndex(curve.keys, k => k.time == time);
                if (key == -1) return;
                var keyframe = curve.keys[key];
                var before = curve.keys[key - 1];
                var next = curve.keys[key + 1];

                switch (val)
                {
                    case null:
                    case "":
                        return;
                    case CurveTypeValues.Flat:
                        keyframe.inTangent = 0f;
                        keyframe.outTangent = 0f;
                        curve.MoveKey(key, keyframe);
                        break;
                    case CurveTypeValues.Linear:
                        keyframe.inTangent = CalculateLinearTangent(before, keyframe);
                        keyframe.outTangent = CalculateLinearTangent(keyframe, next);
                        curve.MoveKey(key, keyframe);
                        break;
                    case CurveTypeValues.Bounce:
                        keyframe.inTangent = CalculateTangent(before, keyframe);
                        keyframe.outTangent = CalculateTangent(keyframe, next);
                        curve.MoveKey(key, keyframe);
                        break;
                    case CurveTypeValues.Smooth:
                        curve.SmoothTangents(key, 0f);
                        break;
                    case CurveTypeValues.LinearFlat:
                        keyframe.inTangent = CalculateTangent(before, keyframe);
                        keyframe.outTangent = 0f;
                        break;
                    case CurveTypeValues.FlatLinear:
                        keyframe.inTangent = 0f;
                        keyframe.outTangent = CalculateTangent(keyframe, next);
                        break;
                    default:
                        throw new NotSupportedException($"Curve type {val} is not supported");
                }
            }
        }

        public void SmoothAllFrames()
        {
            foreach (var curve in Curves)
            {
                if (curve.keys.Length == 2)
                {
                    curve.keys[0].inTangent = 0f;
                    curve.keys[0].outTangent = 0f;
                    curve.keys[1].inTangent = 0f;
                    curve.keys[1].outTangent = 0f;
                    continue;
                }
                // First and last frame will be recalculated in loop smoothing
                for (int k = 1; k < curve.keys.Length - 1; k++)
                {
                    var keyframe = curve.keys[k];
                    var inTangent = CalculateLinearTangent(curve.keys[k - 1], keyframe);
                    var outTangent = CalculateLinearTangent(keyframe, curve.keys[k + 1]);
                    var tangent = inTangent + outTangent / 2f;
                    keyframe.inTangent = tangent;
                    keyframe.outTangent = tangent;
                    keyframe.inWeight = 0.33f;
                    keyframe.outWeight = 0.33f;
                    curve.MoveKey(k, keyframe);
                }

                var cloneFirstToLastKeyframe = curve.keys[0];
                cloneFirstToLastKeyframe.time = curve.keys[curve.keys.Length - 1].time;
                curve.MoveKey(curve.keys.Length - 1, cloneFirstToLastKeyframe);
            }
        }

        private static float CalculateTangent(Keyframe from, Keyframe to, float strength = 0.8f)
        {
            var tangent = CalculateLinearTangent(from, to);
            if (tangent > 0)
                return strength;
            else if (tangent < 0)
                return -strength;
            else
                return 0;
        }

        private static float CalculateLinearTangent(Keyframe from, Keyframe to)
        {
            return (float)((from.value - (double)to.value) / (from.time - (double)to.time));
        }

        private static float CalculateLinearTangent(float fromValue, float toValue, float fromTime, float toTime)
        {
            return (float)((fromValue - (double)toValue) / (fromTime - (double)toTime));
        }
    }
}
