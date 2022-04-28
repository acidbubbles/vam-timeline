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
            prefabFactory.CreateTextField(_animationsListJSON);
        }

        private void InitReorderAnimationsUI()
        {
            var moveAnimUpUI = prefabFactory.CreateButton("Reorder animation (move up)");
            moveAnimUpUI.button.onClick.AddListener(ReorderAnimationMoveUp);

            var moveAnimDownUI = prefabFactory.CreateButton("Reorder animation (move down)");
            moveAnimDownUI.button.onClick.AddListener(ReorderAnimationMoveDown);
        }

        private void InitDeleteAnimationsUI()
        {
            _deleteAnimationUI = prefabFactory.CreateButton("Delete animation");
            _deleteAnimationUI.button.onClick.AddListener(DeleteAnimation);
            _deleteAnimationUI.buttonColor = Color.red;
            _deleteAnimationUI.textColor = Color.white;
        }

        private void InitDeleteLayerUI()
        {
            _deleteLayerUI = prefabFactory.CreateButton("Delete layer");
            _deleteLayerUI.button.onClick.AddListener(DeleteLayer);
            _deleteLayerUI.buttonColor = Color.red;
            _deleteLayerUI.textColor = Color.white;
        }

        private void InitSyncInAllAtomsUI()
        {
            var syncInAllAtoms = prefabFactory.CreateButton("Create/sync in all atoms");
            syncInAllAtoms.button.onClick.AddListener(SyncInAllAtoms);
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
                if (idx <= 0 || animation.clips[idx - 1].animationLayerQualified != current.animationLayerQualified) return;
                animation.clips.RemoveAt(idx);
                animation.clips.Insert(idx - 1, anim);
                animation.index.Rebuild();
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
                if (idx >= animation.clips.Count - 1 || animation.clips[idx + 1].animationLayerQualified != current.animationLayerQualified) return;
                animation.clips.RemoveAt(idx);
                animation.clips.Insert(idx + 1, anim);
                animation.index.Rebuild();
                animation.onClipsListChanged.Invoke();
            }
            catch (Exception exc)
            {
                SuperController.LogError($"Timeline.{nameof(ManageAnimationsScreen)}.{nameof(ReorderAnimationMoveDown)}: {exc}");
            }
        }

        private void DeleteAnimation()
        {
            prefabFactory.CreateConfirm("Delete current animation", DeleteAnimationConfirm);
        }

        private void DeleteAnimationConfirm()
        {
            operations.AddAnimation().DeleteAnimation(current);
            animationEditContext.SelectAnimation(currentLayer.FirstOrDefault());
        }

        private void DeleteLayer()
        {
            prefabFactory.CreateConfirm("Delete current layer", DeleteLayerConfirm);
        }

        private void DeleteLayerConfirm()
        {
            try
            {
                if (!animation.EnumerateLayers(current.animationLayerQualified).Skip(1).Any())
                {
                    SuperController.LogError("Timeline: Cannot delete the only layer.");
                    return;
                }
                var clips = currentLayer;
                animationEditContext.SelectAnimation(animation.clips.First(c => c.animationLayerQualified != current.animationLayerQualified));
                animation.index.StartBulkUpdates();
                try
                {
                    foreach (var clip in clips)
                        animation.RemoveClip(clip);
                }
                finally
                {
                    animation.index.EndBulkUpdates();
                }
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

            var animationsInLayer = 0;
            foreach (var layer in currentSegment.layers)
            {
                sb.AppendLine($"=== {layer[0].animationLayer} ===");
                foreach (var clip in layer)
                {
                    if (clip.animationLayer == current.animationLayer)
                        animationsInLayer++;

                    if (clip == current)
                        sb.Append("> ");
                    else
                        sb.Append("   ");
                    sb.AppendLine(clip.animationName);
                }
            }

            _animationsListJSON.val = sb.ToString();
            _deleteAnimationUI.button.interactable = animationsInLayer > 1;
            _deleteLayerUI.button.interactable = currentSegment.layers.Count > 1;
        }

        public override void OnDestroy()
        {
            animation.onClipsListChanged.RemoveListener(RefreshAnimationsList);
            base.OnDestroy();
        }

        #endregion
    }
}

