using System.Collections.Generic;
using UnityEngine;

namespace VamTimeline
{
    public class FreeControllerAnimationTarget : CurveAnimationTargetBase, ICurveAnimationTarget
    {
        public readonly FreeControllerV3 controller;
        public readonly AnimationCurve x = new AnimationCurve();
        public readonly AnimationCurve y = new AnimationCurve();
        public readonly AnimationCurve z = new AnimationCurve();
        public readonly AnimationCurve rotX = new AnimationCurve();
        public readonly AnimationCurve rotY = new AnimationCurve();
        public readonly AnimationCurve rotZ = new AnimationCurve();
        public readonly AnimationCurve rotW = new AnimationCurve();
        public readonly List<AnimationCurve> curves;

        public override string name => controller.name;

        public FreeControllerAnimationTarget(FreeControllerV3 controller)
        {
            curves = new List<AnimationCurve> {
                x, y, z, rotX, rotY, rotZ, rotW
            };
            this.controller = controller;
        }

        public string GetShortName()
        {
            if (name.EndsWith("Control"))
                return name.Substring(0, name.Length - "Control".Length);
            return name;
        }

        public void Sample(float clipTime, float weight)
        {
            var control = controller?.control;
            if (control == null) return;
            if (controller.possessed) return;

            if (controller.currentRotationState == FreeControllerV3.RotationState.On)
            {
                var targetRotation = EvaluateRotation(clipTime);
                if (controller.linkToRB != null)
                {
                    targetRotation = controller.linkToRB.rotation * targetRotation;
                    var rotation = Quaternion.Slerp(control.rotation, targetRotation, weight);
                    control.rotation = rotation;
                }
                else
                {
                    var localRotation = Quaternion.Slerp(control.localRotation, targetRotation, weight);
                    control.localRotation = localRotation;
                }
            }

            if (controller.currentPositionState == FreeControllerV3.PositionState.On)
            {
                var targetPosition = EvaluatePosition(clipTime);
                if (controller.linkToRB != null)
                {
                    targetPosition = controller.linkToRB.position + controller.linkToRB.transform.rotation * Vector3.Scale(targetPosition, control.transform.localScale);
                    var position = Vector3.Lerp(control.position, targetPosition, weight);
                    control.position = position;
                }
                else
                {
                    var localPosition = Vector3.Lerp(control.localPosition, targetPosition, weight);
                    control.localPosition = localPosition;
                }
            }
        }

        #region Control

        public override AnimationCurve GetLeadCurve()
        {
            return x;
        }

        public IEnumerable<AnimationCurve> GetCurves()
        {
            return curves;
        }

        public void Validate(float animationLength)
        {
            Validate(GetLeadCurve(), animationLength);
        }

        public void ReapplyCurveTypes(bool loop)
        {
            if (x.length < 2) return;

            foreach (var curve in curves)
            {
                ReapplyCurveTypes(curve, loop);
            }
        }

        #endregion

        #region Keyframes control

        public int SetKeyframeToCurrentTransform(float time)
        {
            if (controller.linkToRB != null)
            {
                return SetKeyframe(time, controller.linkToRB.transform.InverseTransformPoint(controller.transform.position), Quaternion.Inverse(controller.transform.rotation) * controller.linkToRB.rotation);
            }

            return SetKeyframe(time, controller.transform.localPosition, controller.transform.localRotation);
        }

        public int SetKeyframe(float time, Vector3 localPosition, Quaternion locationRotation)
        {
            var key = x.SetKeyframe(time, localPosition.x);
            y.SetKeyframe(time, localPosition.y);
            z.SetKeyframe(time, localPosition.z);
            rotX.SetKeyframe(time, locationRotation.x);
            rotY.SetKeyframe(time, locationRotation.y);
            rotZ.SetKeyframe(time, locationRotation.z);
            rotW.SetKeyframe(time, locationRotation.w);
            SetKeyframeSettings(time, CurveTypeValues.Smooth);
            dirty = true;
            return key;
        }

        public void DeleteFrame(float time)
        {
            var key = GetLeadCurve().KeyframeBinarySearch(time);
            if (key != -1) DeleteFrameByKey(key);
        }

        public void DeleteFrameByKey(int key)
        {
            foreach (var curve in curves)
            {
                curve.RemoveKey(key);
            }
            DeleteKeyframeSettings(GetLeadCurve()[key].time);
            dirty = true;
        }

        public void AddEdgeFramesIfMissing(float animationLength)
        {
            x.AddEdgeFramesIfMissing(animationLength);
            y.AddEdgeFramesIfMissing(animationLength);
            z.AddEdgeFramesIfMissing(animationLength);
            rotX.AddEdgeFramesIfMissing(animationLength);
            rotY.AddEdgeFramesIfMissing(animationLength);
            rotZ.AddEdgeFramesIfMissing(animationLength);
            rotW.AddEdgeFramesIfMissing(animationLength);
            AddEdgeKeyframeSettingsIfMissing(animationLength);
            dirty = true;
        }

        public float[] GetAllKeyframesTime()
        {
            var curve = x;
            var keyframes = new float[curve.length];
            for (var i = 0; i < curve.length; i++)
                keyframes[i] = curve[i].time;
            return keyframes;
        }

        public float GetTimeClosestTo(float time)
        {
            return x[x.KeyframeBinarySearch(time, true)].time;
        }

        public bool HasKeyframe(float time)
        {
            return x.KeyframeBinarySearch(time) != -1;
        }

        #endregion

        #region Evaluate

        public Vector3 EvaluatePosition(float time)
        {
            return new Vector3(
                x.Evaluate(time),
                y.Evaluate(time),
                z.Evaluate(time)
            );
        }

        public Quaternion EvaluateRotation(float time)
        {
            return new Quaternion(
                rotX.Evaluate(time),
                rotY.Evaluate(time),
                rotZ.Evaluate(time),
                rotW.Evaluate(time)
            );
        }

        #endregion

        #region Snapshots

        public FreeControllerV3Snapshot GetCurveSnapshot(float time)
        {
            var key = x.KeyframeBinarySearch(time);
            if (key == -1) return null;
            return new FreeControllerV3Snapshot
            {
                x = x[key],
                y = y[key],
                z = z[key],
                rotX = rotX[key],
                rotY = rotY[key],
                rotZ = rotZ[key],
                rotW = rotW[key],
                curveType = GetKeyframeSettings(time) ?? CurveTypeValues.LeaveAsIs
            };
        }

        public void SetCurveSnapshot(float time, FreeControllerV3Snapshot snapshot, bool dirty = true)
        {
            x.SetKeySnapshot(time, snapshot.x);
            y.SetKeySnapshot(time, snapshot.y);
            z.SetKeySnapshot(time, snapshot.z);
            rotX.SetKeySnapshot(time, snapshot.rotX);
            rotY.SetKeySnapshot(time, snapshot.rotY);
            rotZ.SetKeySnapshot(time, snapshot.rotZ);
            rotW.SetKeySnapshot(time, snapshot.rotW);
            UpdateSetting(time, snapshot.curveType, true);
            if (dirty) base.dirty = true;
        }

        #endregion

        #region Interpolation

        public bool Interpolate(float clipTime, float maxDistanceDelta, float maxRadiansDelta)
        {
            var targetLocalPosition = new Vector3
            {
                x = x.Evaluate(clipTime),
                y = y.Evaluate(clipTime),
                z = z.Evaluate(clipTime)
            };

            var targetLocalRotation = new Quaternion
            {
                x = rotX.Evaluate(clipTime),
                y = rotY.Evaluate(clipTime),
                z = rotZ.Evaluate(clipTime),
                w = rotW.Evaluate(clipTime)
            };

            controller.transform.localPosition = Vector3.MoveTowards(controller.transform.localPosition, targetLocalPosition, maxDistanceDelta);
            controller.transform.localRotation = Quaternion.RotateTowards(controller.transform.localRotation, targetLocalRotation, maxRadiansDelta);

            var posDistance = Vector3.Distance(controller.transform.localPosition, targetLocalPosition);
            // NOTE: We skip checking for rotation reached because in some cases we just never get even near the target rotation.
            // var rotDistance = Quaternion.Dot(Controller.transform.localRotation, targetLocalRotation);
            return posDistance < 0.01f;
        }

        #endregion

        public bool TargetsSameAs(IAtomAnimationTarget target)
        {
            var t = target as FreeControllerAnimationTarget;
            if (t == null) return false;
            return t.controller == controller;
        }

        public class Comparer : IComparer<FreeControllerAnimationTarget>
        {
            public int Compare(FreeControllerAnimationTarget t1, FreeControllerAnimationTarget t2)
            {
                return t1.controller.name.CompareTo(t2.controller.name);

            }
        }

        public void SmoothNeighbors(int key)
        {
            if (key == -1) return;
            x.SmoothTangents(key, 1f);
            if (key > 0) x.SmoothTangents(key - 1, 1f);
            if (key < x.length - 1) x.SmoothTangents(key + 1, 1f);

            y.SmoothTangents(key, 1f);
            if (key > 0) y.SmoothTangents(key - 1, 1f);
            if (key < y.length - 1) y.SmoothTangents(key + 1, 1f);

            z.SmoothTangents(key, 1f);
            if (key > 0) z.SmoothTangents(key - 1, 1f);
            if (key < z.length - 1) z.SmoothTangents(key + 1, 1f);
        }
    }
}
