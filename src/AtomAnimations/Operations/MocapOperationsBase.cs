using System;
using System.Collections;
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

            public static ControllerKeyframe FromStep(float time, MotionAnimationStep s, Atom containingAtom, FreeControllerV3 ctrl)
            {
                var localPosition = s.positionOn ? s.position - containingAtom.transform.position : ctrl.transform.localPosition;
                var locationRotation = s.rotationOn ? Quaternion.Inverse(containingAtom.transform.rotation) * s.rotation : ctrl.transform.localRotation;
                return new ControllerKeyframe
                {
                    time = time,
                    position = localPosition,
                    rotation = locationRotation
                };
            }
        }

        protected readonly Atom _containingAtom;
        protected readonly AtomAnimation _animation;
        protected readonly AtomAnimationClip _clip;

        public MocapOperationsBase(Atom containingAtom, AtomAnimation animation, AtomAnimationClip clip)
        {
            _containingAtom = containingAtom;
            _clip = clip;
            _animation = animation;
        }

        public void Prepare(bool resize)
        {
            if (SuperController.singleton.motionAnimationMaster == null || _containingAtom?.motionAnimationControls == null)
                throw new Exception("Timeline: Missing motion animation controls");

            var length = _containingAtom.motionAnimationControls.Select(m => m?.clip?.clipLength ?? 0).Max().Snap(0.01f);
            if (length < 0.01f)
                throw new Exception("Timeline: No motion animation to import.");

            var requiresRebuild = false;
            if (_clip.loop)
            {
                _clip.loop = SuperController.singleton.motionAnimationMaster.loop;
                requiresRebuild = true;
            }
            if (resize && length != _clip.animationLength)
            {
                new ResizeAnimationOperations(_clip).CropOrExtendEnd(length);
                requiresRebuild = true;
            }
            if (requiresRebuild)
            {
                _animation.RebuildAnimationNow();
            }
        }

        public IEnumerator Execute()
        {
            var containingAtom = _containingAtom;
            var totalStopwatch = Stopwatch.StartNew();

            yield return 0;

            var controlCounter = 0;
            var filterSelected = _clip.targetControllers.Any(c => c.selected);
            foreach (var mot in containingAtom.motionAnimationControls)
            {
                FreeControllerAnimationTarget target = null;
                FreeControllerV3 ctrl;

                yield return new Progress { controllersProcessed = ++controlCounter, controllersTotal = _containingAtom.motionAnimationControls.Length };

                try
                {
                    if (mot == null || mot.clip == null) continue;
                    if (mot.clip.clipLength <= 0.1) continue;
                    ctrl = mot.controller;
                    target = _clip.targetControllers.FirstOrDefault(t => t.controller == ctrl);
                    if (filterSelected && (target == null || !target.selected)) continue;

                    if (_animation.EnumerateLayers().Where(l => l != _clip.animationLayer).Select(l => _animation.clips.First(c => c.animationLayer == l)).SelectMany(c => c.targetControllers).Any(t2 => t2.controller == ctrl))
                    {
                        SuperController.LogError($"Skipping controller {ctrl.name} because it was used in another layer.");
                        continue;
                    }

                    if (target == null)
                    {
                        if (!mot.clip.steps.Any(s => s.positionOn || s.rotationOn)) continue;
                        target = new TargetsOperations(_animation, _clip).Add(ctrl);
                        target.AddEdgeFramesIfMissing(_clip.animationLength);
                    }
                    target.Validate(_clip.animationLength);
                    target.StartBulkUpdates();
                    new KeyframesOperations(_clip).RemoveAll(target);
                }
                finally
                {
                    target?.EndBulkUpdates();
                }

                IEnumerator enumerator;
                try
                {
                    enumerator = ProcessController(mot.clip, target, ctrl).GetEnumerator();
                }
                finally
                {
                    target.EndBulkUpdates();
                }

                while (TryMoveNext(enumerator, target))
                    yield return 0;

                target.EndBulkUpdates();
            }
        }

        protected abstract IEnumerable ProcessController(MotionAnimationClip clip, FreeControllerAnimationTarget target, FreeControllerV3 ctrl);

        private bool TryMoveNext(IEnumerator enumerator, FreeControllerAnimationTarget target)
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
