using System;
using System.Collections.Generic;
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
    public class ManageAnimationsScreen : ScreenBase
    {
        public const string ScreenName = "Manage Animations";
        public override string Name => ScreenName;

        public ManageAnimationsScreen(IAtomPlugin plugin)
            : base(plugin)
        {

        }

        #region Init

        public override void Init()
        {
            base.Init();

            // Right side

            CreateChangeScreenButton("<b><</b> <i>Back</i>", MoreScreen.ScreenName, true);

            CreateSpacer(true);

            InitCreateAnimationUI(true);

            CreateSpacer(true);

            InitReorderAnimationsUI(true);

            CreateSpacer(true);

            InitDeleteAnimationsUI(true);
        }

        private void InitCreateAnimationUI(bool rightSide)
        {
            var addAnimationFromCurrentFrameUI = Plugin.CreateButton("Create Animation From Current Frame", rightSide);
            addAnimationFromCurrentFrameUI.button.onClick.AddListener(() => AddAnimationFromCurrentFrame());
            RegisterComponent(addAnimationFromCurrentFrameUI);

            var addAnimationAsCopyUI = Plugin.CreateButton("Create Copy Of Current Animation", rightSide);
            addAnimationAsCopyUI.button.onClick.AddListener(() => AddAnimationAsCopy());
            RegisterComponent(addAnimationAsCopyUI);
        }

        private void InitReorderAnimationsUI(bool rightSide)
        {
            var moveAnimUpUI = Plugin.CreateButton("Reorder Animation (Move Up)", rightSide);
            moveAnimUpUI.button.onClick.AddListener(() => ReorderAnimationMoveUp());
            RegisterComponent(moveAnimUpUI);

            var moveAnimDownUI = Plugin.CreateButton("Reorder Animation (Move Down)", rightSide);
            moveAnimDownUI.button.onClick.AddListener(() => ReorderAnimationMoveDown());
            RegisterComponent(moveAnimDownUI);
        }

        private void InitDeleteAnimationsUI(bool rightSide)
        {
            var deleteAnimationUI = Plugin.CreateButton("Delete Animation", rightSide);
            deleteAnimationUI.button.onClick.AddListener(() => DeleteAnimation());
            deleteAnimationUI.buttonColor = Color.red;
            deleteAnimationUI.textColor = Color.white;
            RegisterComponent(deleteAnimationUI);
        }

        #endregion

        #region Callbacks

        private void AddAnimationAsCopy()
        {
            var clip = Plugin.Animation.AddAnimation();
            clip.Loop = Current.Loop;
            clip.NextAnimationName = Current.NextAnimationName;
            clip.NextAnimationTime = Current.NextAnimationTime;
            clip.EnsureQuaternionContinuity = Current.EnsureQuaternionContinuity;
            clip.BlendDuration = Current.BlendDuration;
            clip.CropOrExtendLengthEnd(Current.AnimationLength);
            foreach (var origTarget in Current.TargetControllers)
            {
                var newTarget = clip.Add(origTarget.Controller);
                for (var i = 0; i < origTarget.Curves.Count; i++)
                {
                    newTarget.Curves[i].keys = origTarget.Curves[i].keys.ToArray();
                }
                foreach (var kvp in origTarget.Settings)
                {
                    newTarget.Settings[kvp.Key] = new KeyframeSettings { CurveType = kvp.Value.CurveType };
                }
                newTarget.Dirty = true;
            }
            foreach (var origTarget in Current.TargetFloatParams)
            {
                var newTarget = clip.Add(origTarget.Storable, origTarget.FloatParam);
                newTarget.Value.keys = origTarget.Value.keys.ToArray();
                newTarget.Dirty = true;
            }
            // TODO: The animation was built before, now it's built after. Make this this works.
            Plugin.Animation.ChangeAnimation(clip.AnimationName);
        }

        private void AddAnimationFromCurrentFrame()
        {
            var clip = Plugin.Animation.AddAnimation();
            clip.Loop = Current.Loop;
            clip.NextAnimationName = Current.NextAnimationName;
            clip.NextAnimationTime = Current.NextAnimationTime;
            clip.EnsureQuaternionContinuity = Current.EnsureQuaternionContinuity;
            clip.BlendDuration = Current.BlendDuration;
            clip.CropOrExtendLengthEnd(Current.AnimationLength);
            foreach (var origTarget in Current.TargetControllers)
            {
                var newTarget = clip.Add(origTarget.Controller);
                newTarget.SetKeyframeToCurrentTransform(0f);
                newTarget.SetKeyframeToCurrentTransform(clip.AnimationLength);
            }
            foreach (var origTarget in Current.TargetFloatParams)
            {
                var newTarget = clip.Add(origTarget.Storable, origTarget.FloatParam);
                newTarget.SetKeyframe(0f, origTarget.FloatParam.val);
                newTarget.SetKeyframe(clip.AnimationLength, origTarget.FloatParam.val);
            }
            Plugin.Animation.ChangeAnimation(clip.AnimationName);
        }

        private void ReorderAnimationMoveUp()
        {
            try
            {
                var anim = Current;
                if (anim == null) return;
                var idx = Plugin.Animation.Clips.IndexOf(anim);
                if (idx <= 0) return;
                Plugin.Animation.Clips.RemoveAt(idx);
                Plugin.Animation.Clips.Insert(idx - 1, anim);
                Plugin.Animation.ClipsListChanged.Invoke();
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AdvancedScreen)}.{nameof(ReorderAnimationMoveUp)}: {exc}");
            }
        }

        private void ReorderAnimationMoveDown()
        {
            try
            {
                var anim = Current;
                if (anim == null) return;
                var idx = Plugin.Animation.Clips.IndexOf(anim);
                if (idx >= Plugin.Animation.Clips.Count - 1) return;
                Plugin.Animation.Clips.RemoveAt(idx);
                Plugin.Animation.Clips.Insert(idx + 1, anim);
                Plugin.Animation.ClipsListChanged.Invoke();
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AdvancedScreen)}.{nameof(ReorderAnimationMoveDown)}: {exc}");
            }
        }

        private void DeleteAnimation()
        {
            try
            {
                var anim = Current;
                if (anim == null) return;
                if (Plugin.Animation.Clips.Count == 1)
                {
                    SuperController.LogError("VamTimeline: Cannot delete the only animation.");
                    return;
                }
                Plugin.Animation.RemoveClip(anim);
                foreach (var clip in Plugin.Animation.Clips)
                {
                    if (clip.NextAnimationName == anim.AnimationName)
                    {
                        clip.NextAnimationName = null;
                        clip.NextAnimationTime = 0;
                    }
                }
                Plugin.ChangeAnimation(Plugin.Animation.Clips[0].AnimationName);
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(AdvancedScreen)}.{nameof(DeleteAnimation)}: {exc}");
            }
        }

        #endregion
    }
}

