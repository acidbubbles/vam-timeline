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
        private UIDynamicButton _bakeUI;
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

            var keyframeCurrentPoseUI = prefabFactory.CreateButton("Keyframe Pose (All On)");
            keyframeCurrentPoseUI.button.onClick.AddListener(() => KeyframeCurrentPose(true));

            var keyframeCurrentPoseTrackedUI = prefabFactory.CreateButton("Keyframe Pose (Animated)");
            keyframeCurrentPoseTrackedUI.button.onClick.AddListener(() => KeyframeCurrentPose(false));

            prefabFactory.CreateSpacer();

            _bakeUI = prefabFactory.CreateButton("Bake Animation (Arm & Record)");
            _bakeUI.button.onClick.AddListener(() => Bake());

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
                    if (!fc.name.EndsWith("Control")) continue;
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
                        foreach (var clip in animation.clips.Where(c => c.animationLayer == current.animationLayer))
                        {
                            var t = clip.Add(fc);
                            t.SetKeyframeToCurrentTransform(0f);
                            t.SetKeyframeToCurrentTransform(clip.animationLength);
                            if (clip == current) target = t;
                        }
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
                _bakeUI.label = "Bake Animation (Arm & Record)";

                SuperController.singleton.motionAnimationMaster.StopRecord();
                animation.StopAll();
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AdvancedKeyframeToolsScreen)}.{nameof(StopWhenPlaybackIsComplete)}: {exc}");
            }
        }
    }
}

