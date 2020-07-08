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

        private FreeControllerAnimationTarget _grabbedController;
        private bool _cancelNextGrabbedControllerRelease;

        public void Update()
        {
            var sc = SuperController.singleton;
            if (sc.gameMode != SuperController.GameMode.Edit) return;

            var grabbing = sc.RightGrabbedController ?? sc.LeftGrabbedController ?? sc.RightFullGrabbedController ?? sc.LeftFullGrabbedController;
            if (grabbing != null && grabbing.containingAtom != containingAtom)
                grabbing = null;
            else if (Input.GetMouseButton(0) && grabbing == null)
                grabbing = containingAtom.freeControllers.FirstOrDefault(c => _grabbingControllers.Contains(c.linkToRB?.gameObject.name));

            if (_grabbedController == null && grabbing != null && !grabbing.possessed)
            {
                _grabbedController = animation.current.targetControllers.FirstOrDefault(c => c.controller == grabbing);
            }
            if (_grabbedController != null && grabbing != null)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                    _cancelNextGrabbedControllerRelease = true;
            }
            else if (_grabbedController != null && grabbing == null)
            {
                var grabbedController = _grabbedController;
                _grabbedController = null;
                if (_cancelNextGrabbedControllerRelease)
                {
                    _cancelNextGrabbedControllerRelease = false;
                    return;
                }

                var time = animation.clipTime.Snap();
                if (animation.autoKeyframeAllControllers)
                {
                    foreach (var target in animation.current.GetAllOrSelectedTargets().OfType<FreeControllerAnimationTarget>())
                        SetControllerKeyframe(time, target);
                }
                else
                {
                    SetControllerKeyframe(time, grabbedController);
                }

                if (animation.current.transition && (animation.clipTime == 0 || animation.clipTime == animation.current.animationLength))
                    animation.Sample();
            }
        }

        private void SetControllerKeyframe(float time, FreeControllerAnimationTarget target)
        {
            animation.SetKeyframeToCurrentTransform(target, time);
        }
    }
}

