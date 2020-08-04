using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class FreeControllerAnimationTarget : CurveAnimationTargetBase, ICurveAnimationTarget
    {
        public readonly FreeControllerV3 controller;
        public readonly BezierAnimationCurve x = new BezierAnimationCurve();
        public readonly BezierAnimationCurve y = new BezierAnimationCurve();
        public readonly BezierAnimationCurve z = new BezierAnimationCurve();
        public readonly BezierAnimationCurve rotX = new BezierAnimationCurve();
        public readonly BezierAnimationCurve rotY = new BezierAnimationCurve();
        public readonly BezierAnimationCurve rotZ = new BezierAnimationCurve();
        public readonly BezierAnimationCurve rotW = new BezierAnimationCurve();
        public readonly List<BezierAnimationCurve> curves;

        public override string name => controller.name;

        public bool playbackEnabled = true;

        private bool _parentAvailable;
        private int _lastParentAvailableCheck = 0;
        public string parentAtomId;
        public string parentRigidbodyId;
        public Rigidbody parentRigidbody;
        public void SetParent(string atomId, string rigidbodyId)
        {
            if (string.IsNullOrEmpty(rigidbodyId))
            {
                parentAtomId = null;
                parentRigidbodyId = null;
                parentRigidbody = null;
                _parentAvailable = true;
                return;
            }
            _parentAvailable = false;
            parentAtomId = atomId;
            parentRigidbodyId = rigidbodyId;
            parentRigidbody = null;
            TryBindParent(false);
        }

        public bool EnsureParentAvailable(bool silent = true)
        {
            if (parentRigidbodyId == null) return true;
            if (_parentAvailable)
            {
                if (parentRigidbody == null)
                {
                    _parentAvailable = false;
                    parentRigidbody = null;
                    return false;
                }
                return true;
            }
            if (Time.frameCount == _lastParentAvailableCheck) return false;
            if (TryBindParent(silent)) return true;
            _lastParentAvailableCheck = Time.frameCount;
            return false;
        }

        public bool TryBindParent(bool silent)
        {
            if (SuperController.singleton.isLoading) return false;
            if (parentRigidbodyId == null) return true;
            var atom = SuperController.singleton.GetAtomByUid(parentAtomId);
            if (atom == null)
            {
                if (!silent) SuperController.LogError($"Timeline: Atom '{parentAtomId}' defined as a parent of {controller.name} was not found in the scene. You can remove the parenting, but the animation will not show in the expected position.");
                return false;
            }
            var rigidbody = atom.linkableRigidbodies.FirstOrDefault(rb => rb.name == parentRigidbodyId);
            if (rigidbody == null)
            {
                if (!silent) SuperController.LogError($"Timeline: Atom '{parentAtomId}' does not have a rigidbody '{parentRigidbodyId}'.");
                return false;
            }

            parentRigidbody = rigidbody;
            _parentAvailable = true;
            return true;
        }

        private Rigidbody _lastLinkedRigidbody;
        public Rigidbody GetLinkedRigidbody()
        {
            if (parentRigidbody != null) return parentRigidbody;
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
            if (!EnsureParentAvailable()) return null;
            return GetLinkedRigidbody()?.transform ?? controller.transform.parent;
        }

        public FreeControllerAnimationTarget(FreeControllerV3 controller)
        {
            curves = new List<BezierAnimationCurve> {
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
            if (!EnsureParentAvailable()) return;
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

        public override BezierAnimationCurve GetLeadCurve()
        {
            return x;
        }

        public override IEnumerable<BezierAnimationCurve> GetCurves()
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
            if (!EnsureParentAvailable(false)) return -1;
            var rb = GetLinkedRigidbody();
            if (rb != null)
                return SetKeyframe(time, rb.transform.InverseTransformPoint(controller.transform.position), Quaternion.Inverse(rb.rotation) * controller.transform.rotation);

            return SetKeyframe(time, controller.transform.localPosition, controller.transform.localRotation);
        }

        public int SetKeyframe(float time, Vector3 localPosition, Quaternion locationRotation)
        {
            var keyframe = x.GetKeyframeAt(time);
            var curveType = keyframe?.curveType ?? CurveTypeValues.Smooth;
            var key = x.SetKeyframe(time, localPosition.x, curveType);
            y.SetKeyframe(time, localPosition.y, curveType);
            z.SetKeyframe(time, localPosition.z, curveType);
            rotX.SetKeyframe(time, locationRotation.x, curveType);
            rotY.SetKeyframe(time, locationRotation.y, curveType);
            rotZ.SetKeyframe(time, locationRotation.z, curveType);
            rotW.SetKeyframe(time, locationRotation.w, curveType);
            dirty = true;
            return key;
        }

        public int SetKeyframeByKey(int key, Vector3 localPosition, Quaternion locationRotation)
        {
            var curveType = x.GetKeyframe(key).curveType;
            x.SetKeyframeByKey(key, localPosition.x, curveType);
            y.SetKeyframeByKey(key, localPosition.y, curveType);
            z.SetKeyframeByKey(key, localPosition.z, curveType);
            rotX.SetKeyframeByKey(key, locationRotation.x, curveType);
            rotY.SetKeyframeByKey(key, locationRotation.y, curveType);
            rotZ.SetKeyframeByKey(key, locationRotation.z, curveType);
            rotW.SetKeyframeByKey(key, locationRotation.w, curveType);
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
