using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class FreeControllerAnimationTarget : TransformAnimationTargetBase, ICurveAnimationTarget
    {
        public readonly FreeControllerV3 controller;

        public override string name => controller.name;

        public bool controlPosition = true;
        public bool controlRotation = true;
        public bool recording;
        private bool _parentAvailable;
        private int _lastParentAvailableCheck;
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

        private Rigidbody _previousLinkedParentRB;

        public Rigidbody GetPositionParentRB()
        {
             if (!ReferenceEquals(parentRigidbody, null)) return parentRigidbody;
             if (controller.currentPositionState == FreeControllerV3.PositionState.ParentLink || controller.currentPositionState == FreeControllerV3.PositionState.PhysicsLink)
             {
                 if (ReferenceEquals(controller.linkToRB, null)) return _previousLinkedParentRB = null;
                 if (_previousLinkedParentRB == controller.linkToRB) return _previousLinkedParentRB;
                 if (ReferenceEquals(controller.linkToRB.GetComponent<FreeControllerV3>(), null)) return _previousLinkedParentRB;
                 return _previousLinkedParentRB = controller.linkToRB;
             }
             return null;
        }

        public Rigidbody GetRotationParentRB()
        {
             if (!ReferenceEquals(parentRigidbody, null)) return parentRigidbody;
             if (controller.currentPositionState == FreeControllerV3.PositionState.ParentLink || controller.currentPositionState == FreeControllerV3.PositionState.PhysicsLink)
             {
                 if (ReferenceEquals(controller.linkToRB, null)) return _previousLinkedParentRB = null;
                 if (_previousLinkedParentRB == controller.linkToRB) return _previousLinkedParentRB;
                 if (ReferenceEquals(controller.linkToRB.GetComponent<FreeControllerV3>(), null)) return _previousLinkedParentRB;
                 return _previousLinkedParentRB = controller.linkToRB;
             }
             return null;
        }

        public FreeControllerAnimationTarget(FreeControllerV3 controller)
        {
            this.controller = controller;
        }

        public string GetShortName()
        {
            if (name.EndsWith("Control"))
                return name.Substring(0, name.Length - "Control".Length);
            return name;
        }

        public override void SelectInVam()
        {
            base.SelectInVam();
            if (SuperController.singleton.GetSelectedController() == controller)
            {
                var selector = controller.containingAtom.gameObject.GetComponentInChildren<UITabSelector>();
                if (selector != null)
                    selector.SetActiveTab(selector.startingTabName);
            }
            else
            {
                SuperController.singleton.SelectController(controller);
            }
        }

        #region Keyframes control

        public int SetKeyframeToCurrentTransform(float time, bool makeDirty = true)
        {
            // TODO: Fix this while possessing
            if (!EnsureParentAvailable(false)) return -1;
            var posParent = GetPositionParentRB();
            var hasPosParent = !ReferenceEquals(posParent, null);
            var rotParent = GetRotationParentRB();
            var hasRotParent = !ReferenceEquals(rotParent, null);
            var controllerTransform = controller.transform;

            return SetKeyframe(
                time,
                hasPosParent ? posParent.transform.InverseTransformPoint(controllerTransform.position) : controllerTransform.localPosition,
                hasRotParent ? Quaternion.Inverse(rotParent.rotation) * controllerTransform.rotation : controllerTransform.localRotation,
                -1,
                false
            );
        }

        public ICurveAnimationTarget Clone()
        {
            var clone = new FreeControllerAnimationTarget(controller);
            clone.x.keys.AddRange(x.keys);
            clone.y.keys.AddRange(y.keys);
            clone.z.keys.AddRange(z.keys);
            clone.rotX.keys.AddRange(rotX.keys);
            clone.rotY.keys.AddRange(rotY.keys);
            clone.rotZ.keys.AddRange(rotZ.keys);
            clone.rotW.keys.AddRange(rotW.keys);
            return clone;
        }

        public void RestoreFrom(ICurveAnimationTarget backup)
        {
            var target = backup as FreeControllerAnimationTarget;
            if (target == null) return;
            if (Math.Abs(target.x.GetLastFrame().time - x.GetLastFrame().time) > 0.0001) return;
            x.keys.Clear();
            x.keys.AddRange(target.x.keys);
            y.keys.Clear();
            y.keys.AddRange(target.y.keys);
            z.keys.Clear();
            z.keys.AddRange(target.z.keys);
            rotX.keys.Clear();
            rotX.keys.AddRange(target.rotX.keys);
            rotY.keys.Clear();
            rotY.keys.AddRange(target.rotY.keys);
            rotZ.keys.Clear();
            rotZ.keys.AddRange(target.rotZ.keys);
            rotW.keys.Clear();
            rotW.keys.AddRange(target.rotW.keys);
            dirty = true;
        }

        #endregion

        #region Interpolation

        public bool Interpolate(float clipTime, float maxDistanceDelta, float maxRadiansDelta)
        {
            var targetLocalPosition = EvaluatePosition(clipTime);
            var targetLocalRotation = EvaluateRotation(clipTime);

            var controllerTransform = controller.transform;
            controllerTransform.localPosition = Vector3.MoveTowards(controllerTransform.localPosition, targetLocalPosition, maxDistanceDelta);
            controllerTransform.localRotation = Quaternion.RotateTowards(controllerTransform.localRotation, targetLocalRotation, maxRadiansDelta);

            var posDistance = Vector3.Distance(controllerTransform.localPosition, targetLocalPosition);
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
                return string.Compare(t1.controller.name, t2.controller.name, StringComparison.Ordinal);
            }
        }
    }
}
