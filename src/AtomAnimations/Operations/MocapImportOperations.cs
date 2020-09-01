using System.Collections;
using UnityEngine;

namespace VamTimeline
{
    public class MocapImportSettings
    {
        public float maxFramesPerSecond;
    }

    public class MocapImportOperations : MocapOperationsBase
    {
        private readonly MocapImportSettings _settings;

        public MocapImportOperations(Atom containingAtom, AtomAnimation animation, AtomAnimationClip clip, MocapImportSettings settings)
            : base(containingAtom, animation, clip)
        {
            _settings = settings;
        }

        protected override IEnumerable ProcessController(MotionAnimationClip clip, FreeControllerAnimationTarget target, FreeControllerV3 ctrl)
        {
            var minPositionDistanceForFlat = 0.01f;
            var frameLength = 1f / _settings.maxFramesPerSecond;

            var lastRecordedFrame = float.MinValue;
            MotionAnimationStep previousStep = null;
            for (var stepIndex = 0; stepIndex < (clip.steps.Count - (_clip.loop ? 1 : 0)); stepIndex++)
            {
                var step = clip.steps[stepIndex];
                var time = step.timeStep.Snap(0.01f);
                if (time - lastRecordedFrame < frameLength) continue;
                if (time > _clip.animationLength) break;
                var k = ControllerKeyframe.FromStep(time, step, target.GetParent(), ctrl);
                var key = target.SetKeyframe(time, k.position, k.rotation, CurveTypeValues.SmoothLocal);
                if (previousStep != null && (target.controller.name == "lFootControl" || target.controller.name == "rFootControl") && Vector3.Distance(previousStep.position, step.position) <= minPositionDistanceForFlat)
                {
                    target.ChangeCurve(previousStep.timeStep, CurveTypeValues.Linear, _clip.loop);
                }
                lastRecordedFrame = time;
                previousStep = step;
            }

            target.AddEdgeFramesIfMissing(_clip.animationLength);
            yield break;
        }
    }
}
