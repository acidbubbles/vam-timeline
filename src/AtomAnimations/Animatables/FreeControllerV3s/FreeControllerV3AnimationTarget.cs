using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class FreeControllerV3AnimationTarget : TransformAnimationTargetBase<FreeControllerV3Ref>, ICurveAnimationTarget
    {
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
                if (!silent) SuperController.LogError($"Timeline: Atom '{parentAtomId}' defined as a parent of {animatableRef.name} was not found in the scene. You can remove the parenting, but the animation will not show in the expected position.");
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
             var currentPositionState = animatableRef.controller.currentPositionState;
             var linkToRB = animatableRef.controller.linkToRB;
             if (currentPositionState == FreeControllerV3.PositionState.ParentLink || currentPositionState == FreeControllerV3.PositionState.PhysicsLink)
             {
                 if (ReferenceEquals(linkToRB, null)) return _previousLinkedParentRB = null;
                 if (_previousLinkedParentRB == linkToRB) return _previousLinkedParentRB;
                 if (ReferenceEquals(linkToRB.GetComponent<FreeControllerV3>(), null)) return _previousLinkedParentRB;
                 return _previousLinkedParentRB = linkToRB;
             }
             return null;
        }

        public Rigidbody GetRotationParentRB()
        {
             if (!ReferenceEquals(parentRigidbody, null)) return parentRigidbody;
             var currentPositionState = animatableRef.controller.currentPositionState;
             var linkToRB = animatableRef.controller.linkToRB;
             if (currentPositionState == FreeControllerV3.PositionState.ParentLink || currentPositionState == FreeControllerV3.PositionState.PhysicsLink)
             {
                 if (ReferenceEquals(linkToRB, null)) return _previousLinkedParentRB = null;
                 if (_previousLinkedParentRB == linkToRB) return _previousLinkedParentRB;
                 if (ReferenceEquals(linkToRB.GetComponent<FreeControllerV3>(), null)) return _previousLinkedParentRB;
                 return _previousLinkedParentRB = linkToRB;
             }
             return null;
        }

        public FreeControllerV3AnimationTarget(FreeControllerV3Ref animatableRef)
            : base(animatableRef)
        {
        }

        public override void SelectInVam()
        {
            base.SelectInVam();
            if (SuperController.singleton.GetSelectedController() == animatableRef.controller)
            {
                var selector = animatableRef.controller.containingAtom.gameObject.GetComponentInChildren<UITabSelector>();
                if (selector != null)
                    selector.SetActiveTab(selector.startingTabName);
            }
            else
            {
                SuperController.singleton.SelectController(animatableRef.controller);
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
            var controllerTransform = animatableRef.controller.transform;

            return SetKeyframe(
                time,
                hasPosParent ? posParent.transform.InverseTransformPoint(controllerTransform.position) : controllerTransform.localPosition,
                hasRotParent ? Quaternion.Inverse(rotParent.rotation) * controllerTransform.rotation : controllerTransform.localRotation,
                -1,
                makeDirty
            );
        }

        public ICurveAnimationTarget Clone(bool copyKeyframes)
        {
            var clone = new FreeControllerV3AnimationTarget(animatableRef);
            if (copyKeyframes)
            {
                clone.x.keys.AddRange(x.keys);
                clone.y.keys.AddRange(y.keys);
                clone.z.keys.AddRange(z.keys);
                clone.rotX.keys.AddRange(rotX.keys);
                clone.rotY.keys.AddRange(rotY.keys);
                clone.rotZ.keys.AddRange(rotZ.keys);
                clone.rotW.keys.AddRange(rotW.keys);
            }
            else
            {
                clone.SetKeyframe(0f, GetKeyframePosition(0), GetKeyframeRotation(0), CurveTypeValues.SmoothLocal);
                clone.SetKeyframe(GetKeyframeTime(x.length - 1), GetKeyframePosition(x.length - 1), GetKeyframeRotation(x.length - 1), CurveTypeValues.SmoothLocal);
                clone.ComputeCurves();
            }
            return clone;
        }

        public void RestoreFrom(ICurveAnimationTarget backup)
        {
            var target = backup as FreeControllerV3AnimationTarget;
            if (target == null) return;
            var maxTime = x.GetLastFrame().time;
            x.keys.Clear();
            x.keys.AddRange(target.x.keys.Where(k => k.time < maxTime + 0.0001f));
            y.keys.Clear();
            y.keys.AddRange(target.y.keys.Where(k => k.time < maxTime + 0.0001f));
            z.keys.Clear();
            z.keys.AddRange(target.z.keys.Where(k => k.time < maxTime + 0.0001f));
            rotX.keys.Clear();
            rotX.keys.AddRange(target.rotX.keys.Where(k => k.time < maxTime + 0.0001f));
            rotY.keys.Clear();
            rotY.keys.AddRange(target.rotY.keys.Where(k => k.time < maxTime + 0.0001f));
            rotZ.keys.Clear();
            rotZ.keys.AddRange(target.rotZ.keys.Where(k => k.time < maxTime + 0.0001f));
            rotW.keys.Clear();
            rotW.keys.AddRange(target.rotW.keys.Where(k => k.time < maxTime + 0.0001f));
            AddEdgeFramesIfMissing(maxTime);
            dirty = true;
        }

        #endregion

        public bool TargetsSameAs(IAtomAnimationTarget target)
        {
            var t = target as FreeControllerV3AnimationTarget;
            if (t == null) return false;
            return t.animatableRef == animatableRef;
        }

        public override string ToString()
        {
            return $"[FreeControllerV3 Target: {name}]";
        }

        public class Comparer : IComparer<FreeControllerV3AnimationTarget>
        {
            public int Compare(FreeControllerV3AnimationTarget t1, FreeControllerV3AnimationTarget t2)
            {
                return string.Compare(t1.animatableRef.name, t2.animatableRef.name, StringComparison.Ordinal);
            }
        }
    }
}
