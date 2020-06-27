using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class AdvancedScreen : ScreenBase
    {
        public const string ScreenName = "Advanced";

        public override string screenId => ScreenName;

        public AdvancedScreen()
            : base()
        {
        }

        public override void Init(IAtomPlugin plugin)
        {
            base.Init(plugin);

            // Right side

            CreateChangeScreenButton("<b><</b> <i>Back</i>", MoreScreen.ScreenName, true);

            prefabFactory.CreateSpacer();

            var keyframeCurrentPoseUI = prefabFactory.CreateButton("Keyframe Pose (All On)");
            keyframeCurrentPoseUI.button.onClick.AddListener(() => KeyframeCurrentPose(true));

            var keyframeCurrentPoseTrackedUI = prefabFactory.CreateButton("Keyframe Pose (Animated)");
            keyframeCurrentPoseTrackedUI.button.onClick.AddListener(() => KeyframeCurrentPose(false));

            prefabFactory.CreateSpacer();

            var bakeUI = prefabFactory.CreateButton("Bake Animation (Arm & Record)");
            bakeUI.button.onClick.AddListener(() => Bake());

            prefabFactory.CreateSpacer();

            var removeAllKeyframesUI = prefabFactory.CreateButton("Remove All Keyframes");
            removeAllKeyframesUI.button.onClick.AddListener(() => RemoveAllKeyframes());

            var reverseAnimationUI = prefabFactory.CreateButton("Reverse Animation Keyframes");
            reverseAnimationUI.button.onClick.AddListener(() => ReverseAnimation());
        }

        private void RemoveAllKeyframes()
        {
            foreach (var target in current.GetAllOrSelectedTargets())
            {
                target.StartBulkUpdates();
                try
                {
                    foreach (var time in target.GetAllKeyframesTime())
                    {
                        if (time == 0f || time == current.animationLength) continue;
                        target.DeleteFrame(time);
                    }
                }
                finally
                {
                    target.EndBulkUpdates();
                }
            }
        }

        private void ReverseAnimation()
        {
            try
            {
                // TODO: This ignores triggers
                foreach (var target in current.GetAllOrSelectedTargets().OfType<IAnimationTargetWithCurves>())
                {
                    foreach (var curve in target.GetCurves())
                    {
                        curve.Reverse();
                    }

                    var controllerTarget = target as FreeControllerAnimationTarget;
                    if (controllerTarget != null)
                    {
                        var settings = controllerTarget.settings.ToList();
                        var length = settings.Last().Key;
                        controllerTarget.settings.Clear();
                        foreach (var setting in settings)
                        {
                            controllerTarget.settings.Add(length - setting.Key, setting.Value);
                        }
                    }

                    target.dirty = true;
                }
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AdvancedScreen)}.{nameof(ReverseAnimation)}: {exc}");
            }
        }

        private void KeyframeCurrentPose(bool all)
        {
            try
            {
                var time = animation.clipTime;
                foreach (var fc in plugin.containingAtom.freeControllers)
                {
                    if (!fc.name.EndsWith("Control")) continue;
                    if (fc.currentPositionState != FreeControllerV3.PositionState.On) continue;
                    if (fc.currentRotationState != FreeControllerV3.RotationState.On) continue;

                    var target = current.targetControllers.FirstOrDefault(tc => tc.controller == fc);
                    if (target == null)
                    {
                        if (!all) continue;
                        target = animation.current.Add(fc);
                    }
                    animation.SetKeyframeToCurrentTransform(target, time);
                }
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AdvancedScreen)}.{nameof(KeyframeCurrentPose)}: {exc}");
            }
        }

        private void Bake()
        {
            try
            {
                var controllers = animation.clips.SelectMany(c => c.targetControllers).Select(c => c.controller).Distinct().ToList();
                foreach (var mac in plugin.containingAtom.motionAnimationControls)
                {
                    if (!controllers.Contains(mac.controller)) continue;
                    mac.armedForRecord = true;
                }

                animation.PlayAll();
                SuperController.singleton.motionAnimationMaster.StartRecord();

                StartCoroutine(StopWhenPlaybackIsComplete());
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AdvancedScreen)}.{nameof(Bake)}: {exc}");
            }
        }

        private IEnumerator StopWhenPlaybackIsComplete()
        {
            var waitFor = animation.clips.Sum(c => c.nextAnimationTime.IsSameFrame(0) ? c.animationLength : c.nextAnimationTime);
            yield return new WaitForSeconds(waitFor);

            try
            {
                SuperController.singleton.motionAnimationMaster.StopRecord();
                animation.StopAll();
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AdvancedScreen)}.{nameof(StopWhenPlaybackIsComplete)}: {exc}");
            }
        }
    }
}

