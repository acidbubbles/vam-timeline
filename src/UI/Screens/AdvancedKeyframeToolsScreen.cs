using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class AdvancedKeyframeToolsScreen : ScreenBase
    {
        public const string ScreenName = "Advanced";
        private const string _offsetControllerUILabel = "Offset controllers";
        private const string _offsetControllerUIOfsettingLabel = "Click again to apply offset...";

        private static bool _offsetting;
        private static AtomClipboardEntry _offsetSnapshot;

        private UIDynamicButton _bakeUI;
        private UIDynamicButton _offsetControllerUI;
        private bool _baking;

        public override string screenId => ScreenName;

        public AdvancedKeyframeToolsScreen()
            : base()
        {
        }

        public override void Init(IAtomPlugin plugin)
        {
            base.Init(plugin);

            // Right side

            CreateChangeScreenButton("<b><</b> <i>Back</i>", MoreScreen.ScreenName);

            prefabFactory.CreateSpacer();

            var keyframeCurrentPoseUI = prefabFactory.CreateButton("Keyframe pose (all on controllers)");
            keyframeCurrentPoseUI.button.onClick.AddListener(() => KeyframeCurrentPose(true));

            var keyframeCurrentPoseTrackedUI = prefabFactory.CreateButton("Keyframe pose (animated targets only)");
            keyframeCurrentPoseTrackedUI.button.onClick.AddListener(() => KeyframeCurrentPose(false));

            prefabFactory.CreateSpacer();

            _bakeUI = prefabFactory.CreateButton("Bake animation (arm & record)");
            _bakeUI.button.onClick.AddListener(() => Bake());

            prefabFactory.CreateSpacer();

            var removeAllKeyframesUI = prefabFactory.CreateButton("Remove all keyframes");
            removeAllKeyframesUI.button.onClick.AddListener(() => RemoveAllKeyframes());

            var reverseAnimationUI = prefabFactory.CreateButton("Reverse keyframes");
            reverseAnimationUI.button.onClick.AddListener(() => ReverseAnimation());

            prefabFactory.CreateSpacer();

            _offsetControllerUI = prefabFactory.CreateButton(_offsetting ? _offsetControllerUIOfsettingLabel : _offsetControllerUILabel);
            _offsetControllerUI.button.onClick.AddListener(() => OffsetController());
        }

        private void RemoveAllKeyframes()
        {
            operations.Keyframes().RemoveAll();
        }

        private void ReverseAnimation()
        {
            try
            {
                foreach (var target in current.GetAllOrSelectedTargets())
                {
                    if (target is ICurveAnimationTarget)
                    {
                        var targetWithCurves = (ICurveAnimationTarget)target;
                        foreach (var curve in targetWithCurves.GetCurves())
                        {
                            curve.Reverse();
                        }

                        var settings = targetWithCurves.settings.ToList();
                        var length = settings.Last().Key;
                        targetWithCurves.settings.Clear();
                        foreach (var setting in settings)
                        {
                            targetWithCurves.settings.Add(length - setting.Key, setting.Value);
                        }
                    }
                    else if (target is TriggersAnimationTarget)
                    {
                        var triggersTarget = (TriggersAnimationTarget)target;
                        var keyframes = new List<int>(triggersTarget.triggersMap.Count);
                        var triggers = new List<AtomAnimationTrigger>(triggersTarget.triggersMap.Count);
                        foreach (var kvp in triggersTarget.triggersMap)
                        {
                            keyframes.Add(kvp.Key);
                            triggers.Add(kvp.Value);
                        }
                        triggersTarget.triggersMap.Clear();
                        var length = current.animationLength.ToMilliseconds();
                        for (var i = 0; i < keyframes.Count; i++)
                        {
                            triggersTarget.triggersMap.Add(length - keyframes[i], triggers[i]);
                        }
                    }
                    else
                    {
                        throw new NotSupportedException($"Target type {target} is not supported");
                    }

                    target.dirty = true;
                }
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AdvancedKeyframeToolsScreen)}.{nameof(ReverseAnimation)}: {exc}");
            }
        }

        private void KeyframeCurrentPose(bool all)
        {
            try
            {
                var time = animation.clipTime.Snap();
                foreach (var fc in plugin.containingAtom.freeControllers)
                {
                    if (fc.name == "control") continue;
                    if (!fc.name.EndsWith("Control")) continue;

                    if (fc.currentRotationState == FreeControllerV3.RotationState.Comply) fc.currentRotationState = FreeControllerV3.RotationState.On;
                    if (fc.currentPositionState == FreeControllerV3.PositionState.Comply) fc.currentPositionState = FreeControllerV3.PositionState.On;

                    if (fc.currentPositionState != FreeControllerV3.PositionState.On && fc.currentRotationState != FreeControllerV3.RotationState.On) continue;

                    var target = current.targetControllers.FirstOrDefault(tc => tc.controller == fc);
                    if (target == null)
                    {
                        if (!all) continue;
                        if (animation.EnumerateLayers().Where(l => l != current.animationLayer).Select(l => animation.clips.First(c => c.animationLayer == l)).SelectMany(c => c.targetControllers).Any(t2 => t2.controller == fc))
                        {
                            SuperController.LogError($"Cannot keyframe controller {fc.name} because it was used in another layer.");
                            continue;
                        }
                        target = operations.Targets().Add(fc);
                    }
                    animation.SetKeyframeToCurrentTransform(target, time);
                }
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AdvancedKeyframeToolsScreen)}.{nameof(KeyframeCurrentPose)}: {exc}");
            }
        }

        private void Bake()
        {
            try
            {
                if (_baking)
                {
                    animation.StopAll();
                    return;
                }

                var controllers = animation.clips.SelectMany(c => c.targetControllers).Select(c => c.controller).Distinct().ToList();
                foreach (var mac in plugin.containingAtom.motionAnimationControls)
                {
                    if (!controllers.Contains(mac.controller)) continue;
                    mac.armedForRecord = true;
                }

                _baking = true;
                _bakeUI.label = "Click or press Esc to stop...";

                animation.PlayAll();
                SuperController.singleton.motionAnimationMaster.StartRecord();

                StartCoroutine(StopWhenPlaybackIsComplete());
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AdvancedKeyframeToolsScreen)}.{nameof(Bake)}: {exc}");
            }
        }

        private IEnumerator StopWhenPlaybackIsComplete()
        {
            while (animation.isPlaying && !Input.GetKey(KeyCode.Escape))
                yield return 0;

            try
            {
                _baking = false;
                _bakeUI.label = "Bake animation (arm & record)";

                SuperController.singleton.motionAnimationMaster.StopRecord();
                animation.StopAll();
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AdvancedKeyframeToolsScreen)}.{nameof(StopWhenPlaybackIsComplete)}: {exc}");
            }
        }

        private void OffsetController()
        {
            if (animation.isPlaying) return;

            if (_offsetting)
                ApplyOffset();
            else
                StartRecordOffset();
        }

        private void StartRecordOffset()
        {
            _offsetSnapshot = current.Copy(current.clipTime, current.GetAllOrSelectedTargets().OfType<FreeControllerAnimationTarget>().Cast<IAtomAnimationTarget>());
            if (_offsetSnapshot.controllers.Count == 0)
            {
                SuperController.LogError($"VamTimeline: Cannot offset, no keyframes were found at time {current.clipTime}.");
                return;
            }

            _offsetControllerUI.label = _offsetControllerUIOfsettingLabel;
            _offsetting = true;
        }

        private void ApplyOffset()
        {
            _offsetting = false;
            _offsetControllerUI.label = _offsetControllerUILabel;

            foreach (var snap in _offsetSnapshot.controllers)
            {
                Vector3 positionDelta;
                Quaternion rotationDelta;

                {
                    var positionBefore = new Vector3(snap.snapshot.x.value, snap.snapshot.y.value, snap.snapshot.z.value);
                    var rotationBefore = new Quaternion(snap.snapshot.rotX.value, snap.snapshot.rotY.value, snap.snapshot.rotZ.value, snap.snapshot.rotW.value);

                    var positionAfter = snap.controller.control.position;
                    var rotationAfter = snap.controller.control.rotation;

                    positionDelta = positionAfter - positionBefore;
                    rotationDelta = Quaternion.Inverse(rotationBefore) * rotationAfter;
                }

                var target = current.targetControllers.First(t => t.controller == snap.controller);
                foreach (var key in target.GetAllKeyframesKeys())
                {
                    // Do not double-apply
                    if (target.GetKeyframeTime(key) == _offsetSnapshot.time) continue;

                    var positionBefore = target.GetKeyframePosition(key);
                    var rotationBefore = target.GetKeyframeRotation(key);

                    target.SetKeyframeByKey(key, positionBefore + positionDelta, rotationBefore * rotationDelta);
                }
            }
        }
    }
}

