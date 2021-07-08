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
            public float time;
            public Vector3 position;
            public Quaternion rotation;

            public static ControllerKeyframe FromStep(float time, MotionAnimationStep s, FreeControllerV3 ctrl)
            {
                var controllerTransform = ctrl.transform;
                var localPosition = s.positionOn ? s.position : controllerTransform.localPosition;
                var locationRotation = s.rotationOn ? s.rotation : controllerTransform.localRotation;
                return new ControllerKeyframe
                {
                    time = time,
                    position = localPosition,
                    rotation = locationRotation
                };
            }
        }

        private readonly AtomAnimation _animation;
        protected readonly AtomAnimationClip clip;
        private readonly Atom _containingAtom;

        public MocapImportOperations(Atom containingAtom, AtomAnimation animation, AtomAnimationClip clip)
        {
            _containingAtom = containingAtom;
            _animation = animation;
            this.clip = clip;
        }

        public void Prepare(bool resize)
        {
            if (SuperController.singleton.motionAnimationMaster == null || _containingAtom?.motionAnimationControls == null)
                throw new Exception("Timeline: Missing motion animation controls");

            var length = _containingAtom.motionAnimationControls.Select(m => m?.clip?.clipLength ?? 0).Max().Snap(0.01f);
            if (length < 0.01f)
                throw new Exception("Timeline: No motion animation to import.");

            var requiresRebuild = false;
            if (clip.loop)
            {
                clip.loop = SuperController.singleton.motionAnimationMaster.loop;
                requiresRebuild = true;
            }
            if (resize && length != clip.animationLength)
            {
                new ResizeAnimationOperations().CropOrExtendEnd(clip, length);
                requiresRebuild = true;
            }
            if (requiresRebuild)
            {
                _animation.RebuildAnimationNow();
            }
        }

        public IEnumerator Execute(List<FreeControllerV3> controllers)
        {
            var keyOps = new KeyframesOperations(clip);
            var targetOps = new TargetsOperations(_containingAtom, _animation, clip);

            yield return 0;

            var controlCounter = 0;
            var motControls = _containingAtom.motionAnimationControls
                .Where(m => m?.clip?.clipLength > 0.1f)
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
                    target = clip.targetControllers.FirstOrDefault(t => t.animatableRef.Targets(ctrl));

                    if (_animation.index.ByLayer().Where(l => l.Key != clip.animationLayer).Select(l => l.Value.First()).SelectMany(c => c.targetControllers).Any(t2 => t2.animatableRef.Targets(ctrl)))
                    {
                        SuperController.LogError($"Skipping controller {ctrl.name} because it was used in another layer.");
                        continue;
                    }

                    if (target == null)
                    {
                        target = targetOps.Add(ctrl);
                        target.AddEdgeFramesIfMissing(clip.animationLength);
                    }
                    else
                    {
                        keyOps.RemoveAll(target);
                    }
                    target.StartBulkUpdates();
                    target.Validate(clip.animationLength);

                    var enumerator = ProcessController(mot.clip, target, ctrl).GetEnumerator();
                    while (enumerator.MoveNext())
                        yield return enumerator.Current;
                }
                finally
                {
                    // NOTE: This should not be necessary, but for some reason dirty is set back to false too early and some changes are not picked up
                    target.dirty = true;
                    target?.EndBulkUpdates();
                }
            }
        }

        protected IEnumerable ProcessController(MotionAnimationClip motClip, FreeControllerV3AnimationTarget target, FreeControllerV3 ctrl)
        {
            var frameLength = 0.001f;

            var lastRecordedFrame = float.MinValue;
            // Vector3? previousPosition = null;
            // var previousTime = 0f;
            for (var stepIndex = 0; stepIndex < motClip.steps.Count - (clip.loop ? 1 : 0); stepIndex++)
            {
                var step = motClip.steps[stepIndex];
                var time = step.timeStep.Snap(0.01f);
                if (time - lastRecordedFrame < frameLength) continue;
                if (time > clip.animationLength) break;
                if (Mathf.Abs(time - clip.animationLength) < 0.001f) time = clip.animationLength;
                var k = ControllerKeyframe.FromStep(time, step, ctrl);
                target.SetKeyframe(time, k.position, k.rotation, CurveTypeValues.SmoothLocal);
                // SuperController.LogMessage($"{k.position.x:0.0000}, {k.position.y:0.0000},{k.position.z:0.0000}");
                // if (previousPosition.HasValue && (target.controller.name == "lFootControl" || target.controller.name == "rFootControl") && Vector3.Distance(previousPosition.Value, step.position) <= minPositionDistanceForFlat)
                // {
                //     target.ChangeCurve(previousTime, CurveTypeValues.Linear);
                // }
                lastRecordedFrame = time;
                // previousPosition = step.position;
                // previousTime = time;
            }

            target.AddEdgeFramesIfMissing(clip.animationLength);
            yield break;
        }
    }
}
