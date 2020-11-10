using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public abstract class MocapOperationsBase
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
                var localPosition = s.positionOn ? s.position : ctrl.transform.localPosition;
                var locationRotation = s.rotationOn ? s.rotation : ctrl.transform.localRotation;
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

        protected MocapOperationsBase(Atom containingAtom, AtomAnimation animation, AtomAnimationClip clip)
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
                new ResizeAnimationOperations(clip).CropOrExtendEnd(length);
                requiresRebuild = true;
            }
            if (requiresRebuild)
            {
                _animation.RebuildAnimationNow();
            }
        }

        public IEnumerator Execute(List<FreeControllerV3> controllers)
        {
            var containingAtom = _containingAtom;
            var keyOps = new KeyframesOperations(clip);
            var targetOps = new TargetsOperations(_animation, clip);

            yield return 0;

            var controlCounter = 0;
            var motControls = containingAtom.motionAnimationControls
                .Where(m => m?.clip?.clipLength > 0.1f)
                .Where(m => controllers.Count == 0 || controllers.Contains(m.controller))
                .Where(m => m.clip.steps.Any(s => s.positionOn || s.rotationOn))
                .ToList();

            foreach (var mot in motControls)
            {
                FreeControllerAnimationTarget target = null;
                FreeControllerV3 ctrl;

                yield return new Progress { controllersProcessed = ++controlCounter, controllersTotal = motControls.Count };

                try
                {
                    ctrl = mot.controller;
                    target = clip.targetControllers.FirstOrDefault(t => t.controller == ctrl);

                    if (_animation.index.ByLayer().Where(l => l.Key != clip.animationLayer).Select(l => l.Value.First()).SelectMany(c => c.targetControllers).Any(t2 => t2.controller == ctrl))
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
                    target.Validate(clip.animationLength);
                    target.StartBulkUpdates();

                    var enumerator = ProcessController(mot.clip, target, ctrl).GetEnumerator();
                    while (TryMoveNext(enumerator, target))
                        yield return enumerator.Current;
                }
                finally
                {
                    target?.EndBulkUpdates();
                }
            }
        }

        protected abstract IEnumerable ProcessController(MotionAnimationClip clip, FreeControllerAnimationTarget target, FreeControllerV3 ctrl);

        private static bool TryMoveNext(IEnumerator enumerator, FreeControllerAnimationTarget target)
        {
            try
            {
                return enumerator.MoveNext();
            }
            catch
            {
                target.EndBulkUpdates();
                throw;
            }
        }
    }
}
