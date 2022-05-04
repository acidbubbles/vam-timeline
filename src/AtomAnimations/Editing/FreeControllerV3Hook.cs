using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class FreeControllerV3Hook : MonoBehaviour
    {
#if (!VAM_GT_1_20_0_9)
        private static readonly HashSet<string> _grabbingControllers = new HashSet<string> { "RightHandAnchor", "LeftHandAnchor", "MouseGrab", "SelectionHandles" };
#endif

        public Atom containingAtom;
        public AtomAnimationEditContext animationEditContext;

#if (VAM_GT_1_20_0_9)

        private List<FreeControllerV3> _controllers = new List<FreeControllerV3>();
        private List<FreeControllerV3> _watchedControllers = new List<FreeControllerV3>();

        public void SetControllers(IEnumerable<FreeControllerV3> controllers)
        {
            UnwatchAllControllers();
            _controllers.Clear();
            _controllers.AddRange(controllers);
            if (enabled)
                WatchAllControllers();
        }

        public void OnEnable()
        {
            if (containingAtom == null) return;
            WatchAllControllers();
        }

        public void OnDisable()
        {
            if (containingAtom == null) return;
            UnwatchAllControllers();
        }

        private void WatchAllControllers()
        {
            foreach (var fc in _controllers)
            {
                WatchController(fc);
            }
        }

        private void WatchController(FreeControllerV3 fc)
        {
            _watchedControllers.Add(fc);
            fc.onRotationChangeHandlers += OnFreeControllerPositionChanged;
            fc.onPositionChangeHandlers += OnFreeControllerPositionChanged;
            fc.onGrabEndHandlers += OnFreeControllerPositionChangedGrabEnd;
        }

        private void UnwatchAllControllers()
        {
            var watched = _watchedControllers.ToList();
            foreach (var fc in watched)
            {
                UnwatchController(fc);
            }
        }

        private void UnwatchController(FreeControllerV3 fc)
        {
            fc.onRotationChangeHandlers -= OnFreeControllerPositionChanged;
            fc.onPositionChangeHandlers -= OnFreeControllerPositionChanged;
            fc.onGrabEndHandlers -= OnFreeControllerPositionChangedGrabEnd;
            _watchedControllers.Remove(fc);
        }

        private void OnFreeControllerPositionChangedGrabEnd(FreeControllerV3 controller)
        {
            HandleControllerChanged(controller, true);
        }

        private void OnFreeControllerPositionChanged(FreeControllerV3 controller)
        {
            HandleControllerChanged(controller, false);
        }

        private void HandleControllerChanged(FreeControllerV3 controller, bool grabEnd)
        {
            // Only record moves in edit mode
            if (!animationEditContext.CanEdit()) return;

            // During recording AtomAnimation will update keyframes
            if (animationEditContext.animation.recording) return;

            // Ignore grabbed event, we will receive the grab end later
            if (controller.isGrabbing) return;

            // Ignore while possessed and startedPossess so that Embody restoring pose won't trigger recording
            if (controller.possessed || controller.startedPossess) return;

            // Do not create a keyframe when loading a preset
            if (controller.isPresetRestore) return;

            // Ignore comply nodes unless the event is grab end, since they will dispatch during the animation
            if (!grabEnd && (controller.currentRotationState == FreeControllerV3.RotationState.Comply || controller.currentPositionState == FreeControllerV3.PositionState.Comply)) return;

            // Only track animated targets
            var target = animationEditContext.current.targetControllers.FirstOrDefault(t => t.animatableRef.Targets(controller));
            if (target == null) return;

            // Ignore grab release at the end of a mocap recording
            if (animationEditContext.ignoreGrabEnd) return;

            RecordFreeControllerPosition(target);
        }

#else

        private FreeControllerV3AnimationTarget _grabbedTarget;
        private bool _cancelNextGrabbedControllerRelease;

        public void Update()
        {
            if (!animationEditContext.CanEdit()) return;

            var grabbing = GetCurrentlyGrabbing();
            var grabbingHasValue = grabbing != null;

            if (_grabbedTarget == null && grabbingHasValue && !grabbing.possessed)
            {
                _grabbedTarget = animationEditContext.current.targetControllers.FirstOrDefault(c => c.animatableRef.controller == grabbing);
            }
            if (_grabbedTarget != null && grabbingHasValue)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                    _cancelNextGrabbedControllerRelease = true;
            }
            else if (_grabbedTarget != null && !grabbingHasValue)
            {
                var grabbedTarget = _grabbedTarget;
                _grabbedTarget = null;
                if (_cancelNextGrabbedControllerRelease)
                {
                    _cancelNextGrabbedControllerRelease = false;
                    return;
                }

                RecordFreeControllerPosition(grabbedTarget);
            }
        }

        private FreeControllerV3 GetCurrentlyGrabbing()
        {
            var sc = SuperController.singleton;
            var grabbing = sc.RightGrabbedController ?? sc.LeftGrabbedController ?? sc.RightFullGrabbedController ?? sc.LeftFullGrabbedController;
            if (grabbing != null)
                return grabbing.containingAtom == containingAtom ? grabbing : null;
            if (Input.GetMouseButton(0))
                return containingAtom.freeControllers.FirstOrDefault(c => _grabbingControllers.Contains(c.linkToRB?.gameObject.name));
            return grabbing;
        }

#endif

        public void RecordFreeControllerPosition(FreeControllerV3AnimationTarget target)
        {
            var time = animationEditContext.clipTime.Snap();
            if (animationEditContext.autoKeyframeAllControllers)
            {
                foreach (var t in animationEditContext.GetAllOrSelectedTargets().OfType<FreeControllerV3AnimationTarget>())
                    animationEditContext.SetKeyframeToCurrentTransform(t, time);
            }
            else
            {
                animationEditContext.SetKeyframeToCurrentTransform(target, time);
            }

            if (animationEditContext.current.autoTransitionPrevious && time == 0)
                animationEditContext.Sample();
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            else if (animationEditContext.current.autoTransitionNext && time == animationEditContext.current.animationLength)
                animationEditContext.Sample();
        }
    }
}
