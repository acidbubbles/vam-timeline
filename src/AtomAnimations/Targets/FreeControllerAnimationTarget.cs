using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class FreeControllerAnimationTarget : TransformAnimationTargetBase, ICurveAnimationTarget
    {
        public readonly FreeControllerV3 controller;

        public override string name => controller.name;

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
            : base()
        {
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
            weight *= _scaledWeight;
            if (weight == 0) return;

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

		public void SelectInVam()
		{
            SuperController.singleton.SelectController(controller);
		}

        #region Keyframes control

        public int SetKeyframeToCurrentTransform(float time)
        {
            if (!EnsureParentAvailable(false)) return -1;
            var rb = GetLinkedRigidbody();
            if (rb != null)
                return SetKeyframe(time, rb.transform.InverseTransformPoint(controller.transform.position), Quaternion.Inverse(rb.rotation) * controller.transform.rotation);

            return SetKeyframe(time, controller.transform.localPosition, controller.transform.localRotation);
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
    }
}
