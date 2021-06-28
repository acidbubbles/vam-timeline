using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class OffsetOperations
    {
        public const string ChangePivotMode = "Change pivot";
        public const string OffsetMode = "Offset";

        private readonly AtomAnimationClip _clip;

        public OffsetOperations(AtomAnimationClip clip)
        {
            _clip = clip;
        }

        public void Apply(AtomClipboardEntry offsetSnapshot, float from, float to, string offsetMode)
        {
            foreach (var snap in offsetSnapshot.controllers)
            {
                var target = _clip.targetControllers.First(t => t.animatableRef == snap.controllerRef);
                if (!target.EnsureParentAvailable(false)) continue;
                var posLink = target.GetPositionParentRB();
                var hasPosLink = !ReferenceEquals(posLink, null);
                var rotLink = target.GetRotationParentRB();
                var hasRotLink = !ReferenceEquals(rotLink, null);

                Vector3 pivot;
                Vector3 positionDelta;
                Quaternion rotationDelta;

                {
                    var positionBefore = new Vector3(snap.snapshot.x.value, snap.snapshot.y.value, snap.snapshot.z.value);
                    var rotationBefore = new Quaternion(snap.snapshot.rotX.value, snap.snapshot.rotY.value, snap.snapshot.rotZ.value, snap.snapshot.rotW.value);

                    var positionAfter = hasPosLink ? posLink.transform.InverseTransformPoint(snap.controllerRef.controller.transform.position) : snap.controllerRef.controller.control.localPosition;
                    var rotationAfter = hasRotLink ? Quaternion.Inverse(rotLink.rotation) * snap.controllerRef.controller.transform.rotation : snap.controllerRef.controller.control.localRotation;

                    pivot = positionBefore;
                    positionDelta = positionAfter - positionBefore;
                    rotationDelta = Quaternion.Inverse(rotationBefore) * rotationAfter;
                }

                foreach (var key in target.GetAllKeyframesKeys())
                {
                    var time = target.GetKeyframeTime(key);
                    if (time < from - 0.0001f || time > to + 0.001f) continue;
                    // Do not double-apply
                    if (Math.Abs(time - offsetSnapshot.time) < 0.0001) continue;

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
                        default:
                            throw new NotImplementedException($"Offset mode '{offsetMode}' is not implemented");
                    }
                }
            }
        }

        public AtomClipboardEntry Start(float clipTime, IEnumerable<FreeControllerV3AnimationTarget> targets)
        {
            // ReSharper disable once RedundantEnumerableCastCall
            var snapshot = AtomAnimationClip.Copy(clipTime, targets.Cast<IAtomAnimationTarget>().ToList());
            if (snapshot.controllers.Count == 0)
            {
                SuperController.LogError($"Timeline: Cannot offset, no keyframes were found at time {clipTime}.");
                return null;
            }
            if (snapshot.controllers.Select(c => _clip.targetControllers.First(t => t.animatableRef == c.controllerRef)).Any(t => !t.EnsureParentAvailable(false)))
            {
                return null;
            }
            return snapshot;
        }
    }
}
