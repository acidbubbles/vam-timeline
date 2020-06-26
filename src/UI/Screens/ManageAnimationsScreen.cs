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

        public override string screenId => ScreenName;

        private JSONStorableString _animationsListJSON;

        public ManageAnimationsScreen()
            : base()
        {
        }

        #region Init

        public override void Init(IAtomPlugin plugin)
        {
            base.Init(plugin);

            // Left side

            InitAnimationsListUI(false);

            // Right side

            CreateChangeScreenButton("<b><</b> <i>Back</i>", MoreScreen.ScreenName, true);

            prefabFactory.CreateSpacer();

            InitReorderAnimationsUI(true);

            prefabFactory.CreateSpacer();

            InitDeleteAnimationsUI(true);

            prefabFactory.CreateSpacer();

            CreateChangeScreenButton("<i><b>Add</b> a new animation...</i>", AddAnimationScreen.ScreenName, true);

            RefreshAnimationsList();

            animation.onClipsListChanged.AddListener(RefreshAnimationsList);
        }

        private void InitAnimationsListUI(bool rightSide)
        {
            _animationsListJSON = new JSONStorableString("Animations List", "");
                        var animationsListUI = prefabFactory.CreateTextField(_animationsListJSON, rightSide);
        }

        private void InitReorderAnimationsUI(bool rightSide)
        {
            var moveAnimUpUI = prefabFactory.CreateButton("Reorder Animation (Move Up)", rightSide);
            moveAnimUpUI.button.onClick.AddListener(() => ReorderAnimationMoveUp());

            var moveAnimDownUI = prefabFactory.CreateButton("Reorder Animation (Move Down)", rightSide);
            moveAnimDownUI.button.onClick.AddListener(() => ReorderAnimationMoveDown());
        }

        private void InitDeleteAnimationsUI(bool rightSide)
        {
            var deleteAnimationUI = prefabFactory.CreateButton("Delete Animation", rightSide);
            deleteAnimationUI.button.onClick.AddListener(() => DeleteAnimation());
            deleteAnimationUI.buttonColor = Color.red;
            deleteAnimationUI.textColor = Color.white;
        }

        #endregion

        #region Callbacks

        private void ReorderAnimationMoveUp()
        {
            try
            {
                var anim = current;
                if (anim == null) return;
                var idx = animation.clips.IndexOf(anim);
                if (idx <= 0) return;
                animation.clips.RemoveAt(idx);
                animation.clips.Insert(idx - 1, anim);
                animation.onClipsListChanged.Invoke();
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
                var idx = animation.clips.IndexOf(anim);
                if (idx >= animation.clips.Count - 1) return;
                animation.clips.RemoveAt(idx);
                animation.clips.Insert(idx + 1, anim);
                animation.onClipsListChanged.Invoke();
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
                if (animation.clips.Count == 1)
                {
                    SuperController.LogError("VamTimeline: Cannot delete the only animation.");
                    return;
                }
                animation.RemoveClip(anim);
                foreach (var clip in animation.clips)
                {
                    if (clip.nextAnimationName == anim.animationName)
                    {
                        clip.nextAnimationName = null;
                        clip.nextAnimationTime = 0;
                    }
                }
                plugin.ChangeAnimation(animation.clips[0].animationName);
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

            foreach (var clip in animation.clips)
            {
                if (clip == current)
                    sb.Append("> ");
                else
                    sb.Append("  ");
                sb.AppendLine(clip.animationName);
            }

            _animationsListJSON.val = sb.ToString();
        }

        public override void OnDestroy()
        {
            animation.onClipsListChanged.RemoveListener(RefreshAnimationsList);
            base.OnDestroy();
        }

        #endregion
    }
}

