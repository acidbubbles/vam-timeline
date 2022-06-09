﻿using System;
using System.Collections.Generic;
using System.Linq;
using Leap.Unity;
using UnityEngine;

namespace VamTimeline
{
    public class OffsetOperations
    {
        public const string ChangePivotMode = "Change pivot";
        public const string OffsetMode = "Offset";
        public const string RepositionMode = "Move relative to root";

        private readonly AtomAnimationClip _clip;

        public OffsetOperations(AtomAnimationClip clip)
        {
            _clip = clip;
        }

        public void Apply(Snapshot offsetSnapshot, float from, float to, string offsetMode)
        {
            var useRepositionMode = offsetMode == RepositionMode;
            var pivot = Vector3.zero;
            var positionDelta = Vector3.zero;
            var rotationDelta = Quaternion.identity;

            if (useRepositionMode)
            {
                pivot = offsetSnapshot.previousReferencePosition;
                positionDelta = offsetSnapshot.referenceController.control.position - offsetSnapshot.previousReferencePosition;
                rotationDelta = Quaternion.Inverse(offsetSnapshot.previousReferenceRotation) * offsetSnapshot.referenceController.control.rotation;

                offsetSnapshot.referenceController.control.SetPositionAndRotation(offsetSnapshot.previousReferencePosition, offsetSnapshot.previousReferenceRotation);
            }

            foreach (var snap in offsetSnapshot.clipboard.controllers)
            {
                // SuperController.LogMessage($"{snap.controllerRef.name} parent {snap.controllerRef.controller.control.parent.parent}");
                var target = _clip.targetControllers.First(t => t.animatableRef == snap.animatableRef);
                if (!target.EnsureParentAvailable(false)) continue;
                var posLink = target.GetPositionParentRB();
                var hasPosLink = !ReferenceEquals(posLink, null);
                var rotLink = target.GetRotationParentRB();
                var hasRotLink = !ReferenceEquals(rotLink, null);
                var control = snap.animatableRef.controller.control;
                var controlParent = control.parent;

                if(!useRepositionMode)
                {
                    var positionBefore = new Vector3(snap.snapshot.x.value, snap.snapshot.y.value, snap.snapshot.z.value);
                    var rotationBefore = new Quaternion(snap.snapshot.rotX.value, snap.snapshot.rotY.value, snap.snapshot.rotZ.value, snap.snapshot.rotW.value);

                    var positionAfter = hasPosLink ? posLink.transform.InverseTransformPoint(snap.animatableRef.controller.transform.position) : snap.animatableRef.controller.control.localPosition;
                    var rotationAfter = hasRotLink ? Quaternion.Inverse(rotLink.rotation) * snap.animatableRef.controller.transform.rotation : snap.animatableRef.controller.control.localRotation;

                    pivot = positionBefore;
                    positionDelta = positionAfter - positionBefore;
                    rotationDelta = Quaternion.Inverse(rotationBefore) * rotationAfter;
                }

                target.StartBulkUpdates();
                try
                {
                    foreach (var key in target.GetAllKeyframesKeys())
                    {
                        if (!useRepositionMode)
                        {
                            // Do not double-apply already moved keyframe
                            var time = target.GetKeyframeTime(key);
                            if (time < from - 0.0001f || time > to + 0.001f) continue;
                            if (Math.Abs(time - offsetSnapshot.clipboard.time) < 0.0001) continue;
                        }

                        var positionBefore = target.GetKeyframePosition(key);
                        var rotationBefore = target.GetKeyframeRotation(key);

                        switch (offsetMode)
                        {
                            case ChangePivotMode:
                            {
                                var positionAfter = rotationDelta * (positionBefore - pivot) + pivot + positionDelta;
                                target.SetKeyframeByKey(key, positionAfter, rotationBefore * rotationDelta);
                                break;
                            }
                            case OffsetMode:
                                target.SetKeyframeByKey(key, positionBefore + positionDelta, rotationBefore * rotationDelta);
                                break;
                            case RepositionMode:
                            {
                                positionBefore = controlParent.TransformPoint(positionBefore);
                                var positionAfter = rotationDelta * (positionBefore - pivot) + pivot + positionDelta;
                                rotationBefore = controlParent.TransformRotation(rotationBefore);
                                var rotationAfter = rotationDelta * rotationBefore;
                                target.SetKeyframeByKey(key,
                                    controlParent.InverseTransformPoint(positionAfter),
                                    controlParent.InverseTransformRotation(rotationAfter)
                                );
                                break;
                            }
                            default:
                                throw new NotImplementedException($"Offset mode '{offsetMode}' is not implemented");
                        }
                    }
                }
                finally
                {
                    target.EndBulkUpdates();
                }

            }
        }

        public Snapshot Start(float clipTime, IEnumerable<FreeControllerV3AnimationTarget> targets, FreeControllerV3 referenceController, string offsetMode)
        {
            if (offsetMode == RepositionMode)
            {
                if (ReferenceEquals(referenceController, null)) throw new NullReferenceException($"{nameof(referenceController)} cannot be null with {nameof(offsetMode)} {nameof(RepositionMode)}");
                referenceController.canGrabPosition = true;
                referenceController.canGrabRotation = true;
                referenceController.currentPositionState = FreeControllerV3.PositionState.On;
                referenceController.currentRotationState = FreeControllerV3.RotationState.On;
            }

            var clipboard = AtomAnimationClip.Copy(clipTime, targets.Cast<IAtomAnimationTarget>().ToList());
            if (clipboard.controllers.Count == 0)
            {
                SuperController.LogError($"Timeline: Cannot offset, no keyframes were found at time {clipTime}.");
                return null;
            }
            if (clipboard.controllers.Select(c => _clip.targetControllers.First(t => t.animatableRef == c.animatableRef)).Any(t => !t.EnsureParentAvailable(false)))
            {
                return null;
            }

            if (ReferenceEquals(referenceController, null))
                return new Snapshot { clipboard = clipboard };

            return new Snapshot
            {
                referenceController = referenceController,
                previousReferencePosition = referenceController.control.position,
                previousReferenceRotation = referenceController.control.rotation,
                clipboard = clipboard
            };
;
        }

        public class Snapshot
        {
            public FreeControllerV3 referenceController;
            public Vector3 previousReferencePosition;
            public Quaternion previousReferenceRotation;
            public AtomClipboardEntry clipboard;
        }
    }
}
