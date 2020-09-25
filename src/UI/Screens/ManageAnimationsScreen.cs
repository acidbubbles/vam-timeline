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

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            CreateChangeScreenButton("<b><</b> <i>Back</i>", AnimationsScreen.ScreenName);

            InitAnimationsListUI();

            prefabFactory.CreateSpacer();

            InitReorderAnimationsUI();

            prefabFactory.CreateSpacer();

            InitDeleteAnimationsUI();
            InitDeleteLayerUI();

            prefabFactory.CreateSpacer();

            InitSyncInAllAtomsUI();

            prefabFactory.CreateSpacer();

            CreateChangeScreenButton("<i><b>Add</b> animations/layers...</i>", AddAnimationScreen.ScreenName);

            RefreshAnimationsList();

            animation.onClipsListChanged.AddListener(RefreshAnimationsList);
        }

        private void InitAnimationsListUI()
        {
            _animationsListJSON = new JSONStorableString("Animations list", "");
            var animationsListUI = prefabFactory.CreateTextField(_animationsListJSON);
        }

        private void InitReorderAnimationsUI()
        {
            var moveAnimUpUI = prefabFactory.CreateButton("Reorder animation (move up)");
            moveAnimUpUI.button.onClick.AddListener(() => ReorderAnimationMoveUp());

            var moveAnimDownUI = prefabFactory.CreateButton("Reorder animation (move down)");
            moveAnimDownUI.button.onClick.AddListener(() => ReorderAnimationMoveDown());
        }

        private void InitDeleteAnimationsUI()
        {
            _deleteAnimationUI = prefabFactory.CreateButton("Delete animation");
            _deleteAnimationUI.button.onClick.AddListener(() => DeleteAnimation());
            _deleteAnimationUI.buttonColor = Color.red;
            _deleteAnimationUI.textColor = Color.white;
        }

        private void InitDeleteLayerUI()
        {
            _deleteLayerUI = prefabFactory.CreateButton("Delete layer");
            _deleteLayerUI.button.onClick.AddListener(() => DeleteLayer());
            _deleteLayerUI.buttonColor = Color.red;
            _deleteLayerUI.textColor = Color.white;
        }

        private void InitSyncInAllAtomsUI()
        {
            var syncInAllAtoms = prefabFactory.CreateButton("Create/sync in all atoms");
            syncInAllAtoms.button.onClick.AddListener(() => SyncInAllAtoms());
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
                SuperController.LogError($"Timeline.{nameof(ManageAnimationsScreen)}.{nameof(ReorderAnimationMoveUp)}: {exc}");
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
                SuperController.LogError($"Timeline.{nameof(ManageAnimationsScreen)}.{nameof(ReorderAnimationMoveDown)}: {exc}");
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
                    SuperController.LogError("Timeline: Cannot delete the only animation.");
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
                animationEditContext.SelectAnimation(animation.clips[0]);
            }
            catch (Exception exc)
            {
                SuperController.LogError($"Timeline.{nameof(ManageAnimationsScreen)}.{nameof(DeleteAnimation)}: {exc}");
            }
        }

        private void DeleteLayer()
        {
            try
            {
                if (!animation.EnumerateLayers().Skip(1).Any())
                {
                    SuperController.LogError("Timeline: Cannot delete the only layer.");
                    return;
                }
                var clips = animation.clips.Where(c => c.animationLayer == current.animationLayer).ToList();
                animationEditContext.SelectAnimation(animation.clips.First(c => c.animationLayer != current.animationLayer));
                foreach (var clip in clips)
                    animation.RemoveClip(clip);
            }
            catch (Exception exc)
            {
                SuperController.LogError($"Timeline.{nameof(ManageAnimationsScreen)}.{nameof(DeleteLayer)}: {exc}");
            }
        }

        private void SyncInAllAtoms()
        {
            plugin.peers.SendSyncAnimation(current);
        }

        #endregion

        #region Events

        protected override void OnCurrentAnimationChanged(AtomAnimationEditContext.CurrentAnimationChangedEventArgs args)
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

