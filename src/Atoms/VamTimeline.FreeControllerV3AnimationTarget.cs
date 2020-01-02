using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace VamTimeline
{
    public interface IAnimationTarget
    {
        string Name { get; }

        void ChangeCurve(float time, string curveType);
        FreeControllerV3Snapshot GetCurveSnapshot(float time);
        void ReapplyCurvesToClip(AnimationClip clip);
        void SetCurveSnapshot(float time, FreeControllerV3Snapshot snapshot);
        void SetKeyframe(float time, Vector3 position, Quaternion rotation);
        void SetKeyframeToCurrentTransform(float time);
        void SetKeyframeToTransform(float time, Vector3 localPosition, Quaternion localRotation);
        void SetLength(float length);
        void SmoothAllFrames();
        void RenderDebugInfo(StringBuilder display, float time);
        IEnumerable<float> GetAllKeyframesTime();
    }

    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class FreeControllerV3AnimationTarget : IAnimationTarget
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

        public string Name => Controller.name;

        public FreeControllerV3AnimationTarget(FreeControllerV3 controller, float animationLength)
        {
            Curves = new List<AnimationCurve> {
                X, Y, Z, RotX, RotY, RotZ, RotW
            };
            Controller = controller;
            _animationLength = animationLength;
        }

        #region Control

        public void SetLength(float length)
        {
            foreach (var curve in Curves)
            {
                curve.SetLength(length);
            }
            _animationLength = length;
        }


        public void ReapplyCurvesToClip(AnimationClip clip)
        {
            // Smooth loop
            foreach (var curve in Curves)
            {
                curve.SmoothLoop();
            }

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

        #endregion

        #region Keyframes control

        public void SetKeyframeToCurrentTransform(float time)
        {
            SetKeyframeToTransform(time, Controller.transform.localPosition, Controller.transform.localRotation);
        }

        public void SetKeyframeToTransform(float time, Vector3 localPosition, Quaternion localRotation)
        {
            if (time == 0f)
            {
                SetKeyframe(0f, localPosition, localRotation);
                SetKeyframe(_animationLength, localPosition, localRotation);
            }
            else
            {
                SetKeyframe(time, localPosition, localRotation);
            }
        }

        public void SetKeyframe(float time, Vector3 position, Quaternion rotation)
        {
            X.SetKeyframe(time, position.x);
            Y.SetKeyframe(time, position.y);
            Z.SetKeyframe(time, position.z);
            RotX.SetKeyframe(time, rotation.x);
            RotY.SetKeyframe(time, rotation.y);
            RotZ.SetKeyframe(time, rotation.z);
            RotW.SetKeyframe(time, rotation.w);
        }

        #endregion

        #region Curves

        public void ChangeCurve(float time, string curveType)
        {
            if (string.IsNullOrEmpty(curveType)) return;
            if (time == 0f || time == _animationLength) return;

            foreach (var curve in Curves)
            {
                curve.ChangeCurve(time, curveType);
            }
        }

        public void SmoothAllFrames()
        {
            foreach (var curve in Curves)
            {
                curve.SmoothAllFrames();
            }
        }

        #endregion

        #region Snapshots

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
            X.SetKeySnapshot(time, snapshot.X);
            Y.SetKeySnapshot(time, snapshot.Y);
            Z.SetKeySnapshot(time, snapshot.Z);
            RotX.SetKeySnapshot(time, snapshot.RotX);
            RotY.SetKeySnapshot(time, snapshot.RotY);
            RotZ.SetKeySnapshot(time, snapshot.RotZ);
            RotW.SetKeySnapshot(time, snapshot.RotW);
        }

        #endregion

        #region  Rendering

        public void RenderDebugInfo(StringBuilder display, float time)
        {
            display.AppendLine($"{Controller.containingAtom.name}:{Controller.name}");
            RenderStateController(time, display, "X", X);
            RenderStateController(time, display, "Y", Y);
            RenderStateController(time, display, "Z", Z);
            RenderStateController(time, display, "RotX", RotX);
            RenderStateController(time, display, "RotY", RotY);
            RenderStateController(time, display, "RotZ", RotZ);
            RenderStateController(time, display, "RotW", RotW);
        }

        private static void RenderStateController(float time, StringBuilder display, string name, AnimationCurve curve)
        {
            display.AppendLine($"{name}");
            foreach (var keyframe in curve.keys)
            {
                display.AppendLine($"  {(keyframe.time == time ? "+" : "-")} {keyframe.time:0.00}s: {keyframe.value:0.00}");
                display.AppendLine($"    Tngt in: {keyframe.inTangent:0.00} out: {keyframe.outTangent:0.00}");
                display.AppendLine($"    Wght in: {keyframe.inWeight:0.00} out: {keyframe.outWeight:0.00} {keyframe.weightedMode}");
            }
        }

        public IEnumerable<float> GetAllKeyframesTime()
        {
            return Curves.SelectMany(c => c.keys.Take(c.keys.Length - 1)).Select(k => k.time).Distinct();
        }

        #endregion
    }
}
