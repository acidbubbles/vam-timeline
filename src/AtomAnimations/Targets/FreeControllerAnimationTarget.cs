using System.Collections.Generic;
using UnityEngine;

namespace VamTimeline
{
    public class FreeControllerAnimationTarget : CurveAnimationTargetBase, ICurveAnimationTarget
    {
        public readonly FreeControllerV3 controller;
        public readonly VamAnimationCurve x = new VamAnimationCurve();
        public readonly VamAnimationCurve y = new VamAnimationCurve();
        public readonly VamAnimationCurve z = new VamAnimationCurve();
        public readonly VamAnimationCurve rotX = new VamAnimationCurve();
        public readonly VamAnimationCurve rotY = new VamAnimationCurve();
        public readonly VamAnimationCurve rotZ = new VamAnimationCurve();
        public readonly VamAnimationCurve rotW = new VamAnimationCurve();
        public readonly List<VamAnimationCurve> curves;

        public override string name => controller.name;

        public bool playbackEnabled = true;

        private Rigidbody _lastLinkedRigidbody;
        public Rigidbody GetLinkedRigidbody()
        {
            var rb = controller.linkToRB;
            if (rb == _lastLinkedRigidbody) return _lastLinkedRigidbody;
            if (rb == null)
            {
                _lastLinkedRigidbody = null;
                return null;
            }
            var tfc = rb.GetComponent<FreeControllerV3>();
            if (tfc == null && _lastLinkedRigidbody != null)
                return _lastLinkedRigidbody;
            if (tfc != null)
                return _lastLinkedRigidbody = controller.linkToRB;
            controller.RestorePreLinkState();
            return _lastLinkedRigidbody = controller.linkToRB;
        }

        public Transform GetParent()
        {
            return GetLinkedRigidbody()?.transform ?? controller.transform.parent;
        }

        public FreeControllerAnimationTarget(FreeControllerV3 controller)
        {
            curves = new List<VamAnimationCurve> {
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
            if (!playbackEnabled) return;

            var control = controller?.control;
            if (control == null) return;
            if (controller.possessed) return;
            Rigidbody link = GetLinkedRigidbody();

            if (controller.currentRotationState != FreeControllerV3.RotationState.Off)
            {
                var targetRotation = EvaluateRotation(clipTime);
                if (link != null)
                {
                    targetRotation = link.rotation * targetRotation;
                    var rotation = Quaternion.Slerp(control.rotation, targetRotation, weight);
                    control.rotation = rotation;
                }
                else
                {
                    var localRotation = Quaternion.Slerp(control.localRotation, targetRotation, weight);
                    control.localRotation = localRotation;
                }
            }

            if (controller.currentPositionState != FreeControllerV3.PositionState.Off)
            {
                var targetPosition = EvaluatePosition(clipTime);
                if (link != null)
                {
                    targetPosition = link.position + link.transform.rotation * Vector3.Scale(targetPosition, control.transform.localScale);
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

        public override VamAnimationCurve GetLeadCurve()
        {
            return x;
        }

        public override IEnumerable<VamAnimationCurve> GetCurves()
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
            var rb = GetLinkedRigidbody();
            if (rb != null)
                return SetKeyframe(time, rb.transform.InverseTransformPoint(controller.transform.position), Quaternion.Inverse(rb.rotation) * controller.transform.rotation);

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
            EnsureKeyframeSettings(time, CurveTypeValues.Smooth);
            dirty = true;
            return key;
        }

        public int SetKeyframeByKey(int key, Vector3 localPosition, Quaternion locationRotation)
        {
            x.SetKeyframeByKey(key, localPosition.x);
            y.SetKeyframeByKey(key, localPosition.y);
            z.SetKeyframeByKey(key, localPosition.z);
            rotX.SetKeyframeByKey(key, locationRotation.x);
            rotY.SetKeyframeByKey(key, locationRotation.y);
            rotZ.SetKeyframeByKey(key, locationRotation.z);
            rotW.SetKeyframeByKey(key, locationRotation.w);
            dirty = true;
            return key;
        }

        public void DeleteFrame(float time)
        {
            var key = GetLeadCurve().KeyframeBinarySearch(time);
            if (key == -1) return;
            foreach (var curve in curves)
            {
                curve.RemoveKey(key);
            }
            settings.Remove(time.ToMilliseconds());
            dirty = true;
        }

        public void AddEdgeFramesIfMissing(float animationLength)
        {
            var before = x.length;
            x.AddEdgeFramesIfMissing(animationLength);
            y.AddEdgeFramesIfMissing(animationLength);
            z.AddEdgeFramesIfMissing(animationLength);
            rotX.AddEdgeFramesIfMissing(animationLength);
            rotY.AddEdgeFramesIfMissing(animationLength);
            rotZ.AddEdgeFramesIfMissing(animationLength);
            rotW.AddEdgeFramesIfMissing(animationLength);
            AddEdgeKeyframeSettingsIfMissing(animationLength);
            if (x.length != before) dirty = true;
        }

        public float[] GetAllKeyframesTime()
        {
            var curve = x;
            var keyframes = new float[curve.length];
            for (var i = 0; i < curve.length; i++)
                keyframes[i] = curve.GetKeyframe(i).time;
            return keyframes;
        }

        public int[] GetAllKeyframesKeys()
        {
            var curve = x;
            var keyframes = new int[curve.length];
            for (var i = 0; i < curve.length; i++)
                keyframes[i] = i;
            return keyframes;
        }

        public float GetTimeClosestTo(float time)
        {
            return x.GetKeyframe(x.KeyframeBinarySearch(time, true)).time;
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

        public float GetKeyframeTime(int key)
        {
            return x.GetKeyframe(key).time;
        }

        public Vector3 GetKeyframePosition(int key)
        {
            return new Vector3(
                x.GetKeyframe(key).value,
                y.GetKeyframe(key).value,
                z.GetKeyframe(key).value
            );
        }

        public Quaternion GetKeyframeRotation(int key)
        {
            return new Quaternion(
                rotX.GetKeyframe(key).value,
                rotY.GetKeyframe(key).value,
                rotZ.GetKeyframe(key).value,
                rotW.GetKeyframe(key).value
            );
        }

        #endregion

        #region Snapshots

        ISnapshot IAtomAnimationTarget.GetSnapshot(float time)
        {
            return GetCurveSnapshot(time);
        }
        void IAtomAnimationTarget.SetSnapshot(float time, ISnapshot snapshot)
        {
            SetCurveSnapshot(time, (FreeControllerV3Snapshot)snapshot);
        }

        public FreeControllerV3Snapshot GetCurveSnapshot(float time)
        {
            var key = x.KeyframeBinarySearch(time);
            if (key == -1) return null;
            return new FreeControllerV3Snapshot
            {
                x = x.GetKeyframe(key).Clone(),
                y = y.GetKeyframe(key).Clone(),
                z = z.GetKeyframe(key).Clone(),
                rotX = rotX.GetKeyframe(key).Clone(),
                rotY = rotY.GetKeyframe(key).Clone(),
                rotZ = rotZ.GetKeyframe(key).Clone(),
                rotW = rotW.GetKeyframe(key).Clone(),
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

        public override string ToString()
        {
            return $"[FreeControllerV3 Target: {name}]";
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
            x.SmoothNeighbors(key);
            y.SmoothNeighbors(key);
            z.SmoothNeighbors(key);
            rotX.SmoothNeighbors(key);
            rotY.SmoothNeighbors(key);
            rotZ.SmoothNeighbors(key);
            rotW.SmoothNeighbors(key);
        }
    }
}
