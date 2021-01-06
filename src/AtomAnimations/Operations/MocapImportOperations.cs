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

        protected override IEnumerable ProcessController(MotionAnimationClip motClip, FreeControllerAnimationTarget target, FreeControllerV3 ctrl)
        {
            var frameLength = 1f / _settings.maxFramesPerSecond;

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
