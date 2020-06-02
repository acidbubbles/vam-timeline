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
        private JSONStorableString _animationsListJSON;

        public override string Name => ScreenName;


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

            Plugin.Animation.ClipsListChanged.AddListener(RefreshAnimationsList);
        }

        private void InitAnimationsListUI(bool rightSide)
        {
            _animationsListJSON = new JSONStorableString("Animations List", "");
            RegisterStorable(_animationsListJSON);
            var animationsListUI = Plugin.CreateTextField(_animationsListJSON, rightSide);
            RegisterComponent(animationsListUI);
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

        #region Events

        protected override void OnCurrentAnimationChanged(AtomAnimation.CurrentAnimationChangedEventArgs args)
        {
            base.OnCurrentAnimationChanged(args);

            RefreshAnimationsList();
        }

        private void RefreshAnimationsList()
        {
            var sb = new StringBuilder();

            foreach (var clip in Plugin.Animation.Clips)
            {
                if (clip == Current)
                    sb.Append("> ");
                else
                    sb.Append("  ");
                sb.AppendLine(clip.AnimationName);
            }

            _animationsListJSON.val = sb.ToString();
        }

        public override void Dispose()
        {
            Plugin.Animation.ClipsListChanged.RemoveListener(RefreshAnimationsList);
            base.Dispose();
        }

        #endregion
    }
}

