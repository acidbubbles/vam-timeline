using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class MocapImportOperations
    {
        public class Progress
        {
            public int controllersProcessed;
            public int controllersTotal;
        }

        protected struct ControllerKeyframe
        {
            public Vector3 position;
            public Quaternion rotation;

            public static ControllerKeyframe FromStep(MotionAnimationStep s, FreeControllerV3 ctrl)
            {
                var controllerTransform = ctrl.transform;
                var localPosition = s.positionOn ? s.position : controllerTransform.localPosition;
                var locationRotation = s.rotationOn ? s.rotation : controllerTransform.localRotation;
                return new ControllerKeyframe
                {
                    position = localPosition,
                    rotation = locationRotation
                };
            }
        }

        private readonly AtomAnimation _animation;
        private readonly AtomAnimationClip _clip;
        private readonly Atom _containingAtom;

        public MocapImportOperations(Atom containingAtom, AtomAnimation animation, AtomAnimationClip clip)
        {
            _containingAtom = containingAtom;
            _animation = animation;
            _clip = clip;
        }

        public void Prepare(bool resize)
        {
            if (SuperController.singleton.motionAnimationMaster == null || _containingAtom.motionAnimationControls == null)
                throw new Exception("Timeline: Missing motion animation controls");

            var length = _containingAtom.motionAnimationControls.Select(m => m.clip.clipLength).Max().Snap();
            if (length < 0.01f)
                throw new Exception("Timeline: No motion animation to import.");

            _clip.loop = SuperController.singleton.motionAnimationMaster.loop;
            if (resize && !Mathf.Approximately(length, _clip.animationLength)) ;
                new ResizeAnimationOperations().CropOrExtendEnd(_clip, length);
            _animation.RebuildAnimationNow();
        }

        public IEnumerator Execute(List<FreeControllerV3> controllers)
        {
            var keyOps = new KeyframesOperations(_clip);
            var targetOps = new TargetsOperations(_containingAtom, _animation, _clip);

            yield return 0;

            var controlCounter = 0;
            var motControls = _containingAtom.motionAnimationControls
                .Where(m => m.clip.clipLength > 0.1f)
                .Where(m => controllers.Count == 0 || controllers.Contains(m.controller))
                .Where(m => m.clip.steps.Any(s => s.positionOn || s.rotationOn))
                .ToList();

            foreach (var mot in motControls)
            {
                FreeControllerV3AnimationTarget target = null;
                FreeControllerV3 ctrl;

                yield return new Progress { controllersProcessed = ++controlCounter, controllersTotal = motControls.Count };

                try
                {
                    ctrl = mot.controller;
                    target = _clip.targetControllers.FirstOrDefault(t => t.animatableRef.Targets(ctrl));

                    if (target == null)
                    {
                        if (_animation.index.ByLayer().Where(l => l[0].animationLayer != _clip.animationLayer).Select(l => l[0]).SelectMany(c => c.targetControllers).Any(t2 => t2.animatableRef.Targets(ctrl)))
                        {
                            SuperController.LogError($"Skipping controller {ctrl.name} because it was used in another layer.");
                            continue;
                        }

                        target = targetOps.Add(ctrl);
                        target.AddEdgeFramesIfMissing(_clip.animationLength);
                    }
                    else
                    {
                        keyOps.RemoveAll(target);
                    }

                    target.StartBulkUpdates();
                    target.Validate(_clip.animationLength);

                    var enumerator = ProcessController(mot.clip, target, ctrl).GetEnumerator();
                    while (enumerator.MoveNext())
                        yield return enumerator.Current;
                }
                finally
                {
                    // NOTE: This should not be necessary, but for some reason dirty is set back to false too early and some changes are not picked up
                    if (target != null)
                    {
                        target.dirty = true;
                        target.EndBulkUpdates();
                    }
                }
            }
        }

        private IEnumerable ProcessController(MotionAnimationClip motClip, FreeControllerV3AnimationTarget target, FreeControllerV3 ctrl)
        {
            var lastRecordedFrame = float.MinValue;
            // Vector3? previousPosition = null;
            // var previousTime = 0f;
            for (var stepIndex = 0; stepIndex < motClip.steps.Count - (_clip.loop ? 1 : 0); stepIndex++)
            {
                var step = motClip.steps[stepIndex];
                var time = step.timeStep.Snap();
                if (time <= lastRecordedFrame + float.Epsilon) continue;
                if (time > _clip.animationLength) break;
                if (Mathf.Abs(time - _clip.animationLength) < 0.001f) time = _clip.animationLength;
                var k = ControllerKeyframe.FromStep(step, ctrl);
                target.SetKeyframe(time, k.position, k.rotation, CurveTypeValues.SmoothLocal);
                lastRecordedFrame = time;
            }

            target.AddEdgeFramesIfMissing(_clip.animationLength);
            yield break;
        }
    }
}
