#if(VAM_GT_1_20_0_9)
using System.Collections;
#endif
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
        public AtomAnimation animation;

#if (VAM_GT_1_20_0_9)

        public void OnEnable()
        {
            if (containingAtom == null) return;
            foreach (var fc in containingAtom.freeControllers)
            {
                fc.onRotationChangeHandlers += OnFreeControllerPositionChanged;
                fc.onPositionChangeHandlers += OnFreeControllerPositionChanged;
                fc.onGrabEndHandlers += OnFreeControllerPositionChanged;
            }
        }

        public void OnDisable()
        {
            if (containingAtom == null) return;
            foreach (var fc in containingAtom.freeControllers)
            {
                fc.onRotationChangeHandlers -= OnFreeControllerPositionChanged;
                fc.onPositionChangeHandlers -= OnFreeControllerPositionChanged;
                fc.onGrabEndHandlers -= OnFreeControllerPositionChanged;
            }
        }

        private void OnFreeControllerPositionChanged(FreeControllerV3 controller)
        {
            // Only record moves in edit mode
            if (SuperController.singleton.gameMode != SuperController.GameMode.Edit) return;

            // Ignore when something else is happening that takes precedence
            if (animation.isPlaying || animation.isSampling) return;

            // Ignore grabbed event, we will receive the grab end later
            if (controller.isGrabbing || controller.possessed) return;

            // Ignore comply nodes, since they will dispatch during the animation
            if (controller.currentRotationState == FreeControllerV3.RotationState.Comply || controller.currentPositionState == FreeControllerV3.PositionState.Comply) return;

            // Only track animated targets
            var target = animation.current.targetControllers.FirstOrDefault(t => t.controller == controller);
            if (target == null) return;

            // Ignore grab release at the end of a mocap recording
            if (target.ignoreGrabEnd) return;

            RecordFreeControllerPosition(target);
        }

#else

        private FreeControllerAnimationTarget _grabbedTarget;
        private bool _cancelNextGrabbedControllerRelease;

        public void Update()
        {
            if (SuperController.singleton.gameMode != SuperController.GameMode.Edit) return;

            var grabbing = GetCurrentlyGrabbing();

            if (_grabbedTarget == null && grabbing != null && !grabbing.possessed)
            {
                _grabbedTarget = animation.current.targetControllers.FirstOrDefault(c => c.controller == grabbing);
            }
            if (_grabbedTarget != null && grabbing != null)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                    _cancelNextGrabbedControllerRelease = true;
            }
            else if (_grabbedTarget != null && grabbing == null)
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

        public void RecordFreeControllerPosition(FreeControllerAnimationTarget target)
        {
            var time = animation.clipTime.Snap();
            if (animation.autoKeyframeAllControllers)
            {
                foreach (var t in animation.current.GetAllOrSelectedTargets().OfType<FreeControllerAnimationTarget>())
                    animation.SetKeyframeToCurrentTransform(target, time);
            }
            else
            {
                animation.SetKeyframeToCurrentTransform(target, time);
            }

            if (animation.current.autoTransitionPrevious && time == 0)
                animation.Sample();
            else if (animation.current.autoTransitionNext && time == animation.current.animationLength)
                animation.Sample();
        }
    }
}
