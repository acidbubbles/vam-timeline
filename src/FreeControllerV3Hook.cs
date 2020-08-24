#if(VAM_GT_1_20_TEMPORARILY_DISABLED)
using System.Collections;
#endif
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class FreeControllerV3Hook : MonoBehaviour
    {
        private static readonly HashSet<string> _grabbingControllers = new HashSet<string> { "RightHandAnchor", "LeftHandAnchor", "MouseGrab", "SelectionHandles" };

        public Atom containingAtom;
        public AtomAnimation animation;

        private FreeControllerAnimationTarget _grabbedTarget;

#if (VAM_GT_1_20_TEMPORARILY_DISABLED)
        private Coroutine _waitForControllerReleaseCoroutine;

        public void OnEnable()
        {
            if (containingAtom == null) return;
            foreach (var fc in containingAtom.freeControllers)
            {
                fc.onRotationChangeHandlers += OnFreeControllerPositionChanged;
                fc.onPositionChangeHandlers += OnFreeControllerPositionChanged;
            }
        }

        public void OnDisable()
        {
            if (_waitForControllerReleaseCoroutine != null)
            {
                StopCoroutine(_waitForControllerReleaseCoroutine);
                _waitForControllerReleaseCoroutine = null;
                _grabbedTarget = null;
            }
            if (containingAtom == null) return;
            foreach (var fc in containingAtom.freeControllers)
            {
                fc.onRotationChangeHandlers -= OnFreeControllerPositionChanged;
                fc.onPositionChangeHandlers -= OnFreeControllerPositionChanged;
            }
        }

        private void OnFreeControllerPositionChanged(FreeControllerV3 controller)
        {
            // Wait for the current transform to be complete before starting another
            if (_waitForControllerReleaseCoroutine != null) return;

            // Only track animated targets
            var target = animation.current.targetControllers.FirstOrDefault(t => t.controller == controller);
            if (target == null) return;

            // Only handle transformations initiated by a user action
            if (GetCurrentlyGrabbing() != target.controller) return;

            _grabbedTarget = target;
            _waitForControllerReleaseCoroutine = StartCoroutine(WaitForControllerRelease());
        }

        private IEnumerator WaitForControllerRelease()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                _waitForControllerReleaseCoroutine = null;
                yield break;
            }

            while (GetCurrentlyGrabbing() == _grabbedTarget.controller)
                yield return 0;

            if (!_grabbedTarget.controller.possessed && !animation.isPlaying)
            {
                RecordFreeControllerPosition(_grabbedTarget);
                _waitForControllerReleaseCoroutine = null;
                _grabbedTarget = null;
            }
        }

#else

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
#endif

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

            if (animation.current.transition && (time == 0 || time == animation.current.animationLength))
                animation.Sample();
        }
    }
}
