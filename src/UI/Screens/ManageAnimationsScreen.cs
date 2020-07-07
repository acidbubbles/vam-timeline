using System;
using System.Linq;
using System.Text;
using UnityEngine;

namespace VamTimeline
{
    public class ManageAnimationsScreen : ScreenBase
    {
        public const string ScreenName = "Manage Animations";

        public override string screenId => ScreenName;

        private JSONStorableString _animationsListJSON;
        private UIDynamicButton _deleteAnimationUI;
        private UIDynamicButton _deleteLayerUI;

        public ManageAnimationsScreen()
            : base()
        {
        }

        #region Init

        public override void Init(IAtomPlugin plugin)
        {
            base.Init(plugin);

            CreateChangeScreenButton("<b><</b> <i>Back</i>", AnimationsScreen.ScreenName);

            InitAnimationsListUI();

            prefabFactory.CreateSpacer();

            InitReorderAnimationsUI();

            prefabFactory.CreateSpacer();

            InitDeleteAnimationsUI();
            InitDeleteLayerUI();

            prefabFactory.CreateSpacer();

            CreateChangeScreenButton("<i><b>Add</b> animations/layers...</i>", AddAnimationScreen.ScreenName);

            RefreshAnimationsList();

            animation.onClipsListChanged.AddListener(RefreshAnimationsList);
        }

        private void InitAnimationsListUI()
        {
            _animationsListJSON = new JSONStorableString("Animations List", "");
            var animationsListUI = prefabFactory.CreateTextField(_animationsListJSON);
        }

        private void InitReorderAnimationsUI()
        {
            var moveAnimUpUI = prefabFactory.CreateButton("Reorder Animation (Move Up)");
            moveAnimUpUI.button.onClick.AddListener(() => ReorderAnimationMoveUp());

            var moveAnimDownUI = prefabFactory.CreateButton("Reorder Animation (Move Down)");
            moveAnimDownUI.button.onClick.AddListener(() => ReorderAnimationMoveDown());
        }

        private void InitDeleteAnimationsUI()
        {
            _deleteAnimationUI = prefabFactory.CreateButton("Delete Animation");
            _deleteAnimationUI.button.onClick.AddListener(() => DeleteAnimation());
            _deleteAnimationUI.buttonColor = Color.red;
            _deleteAnimationUI.textColor = Color.white;
        }

        private void InitDeleteLayerUI()
        {
            _deleteLayerUI = prefabFactory.CreateButton("Delete Layer");
            _deleteLayerUI.button.onClick.AddListener(() => DeleteLayer());
            _deleteLayerUI.buttonColor = Color.red;
            _deleteLayerUI.textColor = Color.white;
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
                if (idx <= 0 || animation.clips[idx - 1].animationLayer != current.animationLayer) return;
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
                if (idx >= animation.clips.Count - 1 || animation.clips[idx + 1].animationLayer != current.animationLayer) return;
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

        private void DeleteLayer()
        {
            try
            {
                if (!animation.EnumerateLayers().Skip(1).Any())
                {
                    SuperController.LogError("VamTimeline: Cannot delete the only layer.");
                    return;
                }
                var clips = animation.clips.Where(c => c.animationLayer == current.animationLayer).ToList();
                foreach (var clip in clips)
                    animation.RemoveClip(clip);
                plugin.ChangeAnimation(animation.clips[0].animationName);
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(ManageAnimationsScreen)}.{nameof(DeleteLayer)}: {exc}");
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

            string layer = null;
            var layersCount = 0;
            var animationsInLayer = 0;
            foreach (var clip in animation.clips)
            {
                if (clip.animationLayer != layer)
                {
                    layer = clip.animationLayer;
                    layersCount++;
                    sb.AppendLine($"=== {layer} ===");
                }

                if (clip.animationLayer == current.animationLayer)
                    animationsInLayer++;

                if (clip == current)
                    sb.Append("> ");
                else
                    sb.Append("   ");
                sb.AppendLine(clip.animationName);
            }

            _animationsListJSON.val = sb.ToString();
            _deleteAnimationUI.button.interactable = animationsInLayer > 1;
            _deleteLayerUI.button.interactable = layersCount > 1;
        }

        public override void OnDestroy()
        {
            animation.onClipsListChanged.RemoveListener(RefreshAnimationsList);
            base.OnDestroy();
        }

        #endregion
    }
}

