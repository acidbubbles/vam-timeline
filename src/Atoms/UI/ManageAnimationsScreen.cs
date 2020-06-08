using System;
using System.Linq;
using System.Text;
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

        public override string name => ScreenName;

        private JSONStorableString _animationsListJSON;

        public ManageAnimationsScreen(IAtomPlugin plugin)
            : base(plugin)
        {

        }

        #region Init

        public override void Init()
        {
            base.Init();

            // Left side

            InitAnimationsListUI(false);

            // Right side

            CreateChangeScreenButton("<b><</b> <i>Back</i>", MoreScreen.ScreenName, true);

            CreateSpacer(true);

            InitReorderAnimationsUI(true);

            CreateSpacer(true);

            InitDeleteAnimationsUI(true);

            CreateSpacer(true);

            CreateChangeScreenButton("<i><b>Add</b> a new animation...</i>", AddAnimationScreen.ScreenName, true);

            RefreshAnimationsList();

            plugin.animation.ClipsListChanged.AddListener(RefreshAnimationsList);
        }

        private void InitAnimationsListUI(bool rightSide)
        {
            _animationsListJSON = new JSONStorableString("Animations List", "");
            RegisterStorable(_animationsListJSON);
            var animationsListUI = plugin.CreateTextField(_animationsListJSON, rightSide);
            RegisterComponent(animationsListUI);
        }

        private void InitReorderAnimationsUI(bool rightSide)
        {
            var moveAnimUpUI = plugin.CreateButton("Reorder Animation (Move Up)", rightSide);
            moveAnimUpUI.button.onClick.AddListener(() => ReorderAnimationMoveUp());
            RegisterComponent(moveAnimUpUI);

            var moveAnimDownUI = plugin.CreateButton("Reorder Animation (Move Down)", rightSide);
            moveAnimDownUI.button.onClick.AddListener(() => ReorderAnimationMoveDown());
            RegisterComponent(moveAnimDownUI);
        }

        private void InitDeleteAnimationsUI(bool rightSide)
        {
            var deleteAnimationUI = plugin.CreateButton("Delete Animation", rightSide);
            deleteAnimationUI.button.onClick.AddListener(() => DeleteAnimation());
            deleteAnimationUI.buttonColor = Color.red;
            deleteAnimationUI.textColor = Color.white;
            RegisterComponent(deleteAnimationUI);
        }

        #endregion

        #region Callbacks

        private void ReorderAnimationMoveUp()
        {
            try
            {
                var anim = current;
                if (anim == null) return;
                var idx = plugin.animation.Clips.IndexOf(anim);
                if (idx <= 0) return;
                plugin.animation.Clips.RemoveAt(idx);
                plugin.animation.Clips.Insert(idx - 1, anim);
                plugin.animation.ClipsListChanged.Invoke();
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(ManageAnimationsScreen)}.{nameof(ReorderAnimationMoveUp)}: {exc}");
            }
        }

        private void ReorderAnimationMoveDown()
        {
            try
            {
                var anim = current;
                if (anim == null) return;
                var idx = plugin.animation.Clips.IndexOf(anim);
                if (idx >= plugin.animation.Clips.Count - 1) return;
                plugin.animation.Clips.RemoveAt(idx);
                plugin.animation.Clips.Insert(idx + 1, anim);
                plugin.animation.ClipsListChanged.Invoke();
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(ManageAnimationsScreen)}.{nameof(ReorderAnimationMoveDown)}: {exc}");
            }
        }

        private void DeleteAnimation()
        {
            try
            {
                var anim = current;
                if (anim == null) return;
                if (plugin.animation.Clips.Count == 1)
                {
                    SuperController.LogError("VamTimeline: Cannot delete the only animation.");
                    return;
                }
                plugin.animation.RemoveClip(anim);
                foreach (var clip in plugin.animation.Clips)
                {
                    if (clip.NextAnimationName == anim.AnimationName)
                    {
                        clip.NextAnimationName = null;
                        clip.NextAnimationTime = 0;
                    }
                }
                plugin.ChangeAnimation(plugin.animation.Clips[0].AnimationName);
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(ManageAnimationsScreen)}.{nameof(DeleteAnimation)}: {exc}");
            }
        }

        #endregion

        #region Events

        protected override void OnCurrentAnimationChanged(AtomAnimation.CurrentAnimationChangedEventArgs args)
        {
            base.OnCurrentAnimationChanged(args);

            RefreshAnimationsList();
        }

        private void RefreshAnimationsList()
        {
            var sb = new StringBuilder();

            foreach (var clip in plugin.animation.Clips)
            {
                if (clip == current)
                    sb.Append("> ");
                else
                    sb.Append("  ");
                sb.AppendLine(clip.AnimationName);
            }

            _animationsListJSON.val = sb.ToString();
        }

        public override void Dispose()
        {
            plugin.animation.ClipsListChanged.RemoveListener(RefreshAnimationsList);
            base.Dispose();
        }

        #endregion
    }
}

