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
        private UIDynamicButton _deleteSegmentUI;

        #region Init

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            CreateChangeScreenButton("<b><</b> <i>Back</i>", AnimationsScreen.ScreenName);

            InitAnimationsListUI();

            prefabFactory.CreateSpacer();

            InitReorderAnimationsUI();
            InitDeleteAnimationsUI();

            prefabFactory.CreateSpacer();

            InitReorderLayersUI();
            InitDeleteLayerUI();

            prefabFactory.CreateSpacer();

            InitReorderSegmentsUI();
            InitDeleteSegmentUI();

            prefabFactory.CreateSpacer();

            InitSyncInAllAtomsUI();

            prefabFactory.CreateSpacer();

            CreateChangeScreenButton("<i><b>Add</b> animations/layers...</i>", AddAnimationsScreen.ScreenName);

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

        private void InitReorderLayersUI()
        {
            var moveAnimUpUI = prefabFactory.CreateButton("Reorder layer (move up)");
            moveAnimUpUI.button.onClick.AddListener(ReorderLayerMoveUp);

            var moveAnimDownUI = prefabFactory.CreateButton("Reorder layer (move down)");
            moveAnimDownUI.button.onClick.AddListener(ReorderLayerMoveDown);
        }

        private void InitDeleteLayerUI()
        {
            _deleteLayerUI = prefabFactory.CreateButton("Delete layer");
            _deleteLayerUI.button.onClick.AddListener(DeleteLayer);
            _deleteLayerUI.buttonColor = Color.red;
            _deleteLayerUI.textColor = Color.white;
        }

        private void InitReorderSegmentsUI()
        {
            var moveAnimUpUI = prefabFactory.CreateButton("Reorder segment (move up)");
            moveAnimUpUI.button.onClick.AddListener(ReorderSegmentMoveUp);

            var moveAnimDownUI = prefabFactory.CreateButton("Reorder segment (move down)");
            moveAnimDownUI.button.onClick.AddListener(ReorderSegmentMoveDown);
        }

        private void InitDeleteSegmentUI()
        {
            _deleteSegmentUI = prefabFactory.CreateButton("Delete segment");
            _deleteSegmentUI.button.onClick.AddListener(DeleteSegment);
            _deleteSegmentUI.buttonColor = Color.red;
            _deleteSegmentUI.textColor = Color.white;
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
            var index = animation.clips.IndexOf(current);
            ReorderMove(
                index,
                index,
                index - 1,
                min: animation.clips.FindIndex(c => c.animationLayerQualified == current.animationLayerQualified)
            );
        }

        private void ReorderAnimationMoveDown()
        {
            var index = animation.clips.IndexOf(current);
            ReorderMove(
                index,
                index,
                index + 2,
                max: animation.clips.FindLastIndex(c => c.animationLayerQualified == current.animationLayerQualified) + 1
            );
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

        private void ReorderLayerMoveUp()
        {
            if (currentSegment.layerNames[0] == current.animationLayer) return;

            var previousLayer = currentSegment.layerNames[currentSegment.layerNames.IndexOf(current.animationLayer) - 1];

            ReorderMove(
                animation.clips.FindIndex(c => c.animationLayerQualified == current.animationLayerQualified),
                animation.clips.FindLastIndex(c => c.animationLayerQualified == current.animationLayerQualified),
                animation.clips.FindIndex(c1 => c1.animationLayer == previousLayer)
            );
        }

        private void ReorderLayerMoveDown()
        {
            if (currentSegment.layerNames[currentSegment.layerNames.Count - 1] == current.animationLayer) return;

            var nextLayer = currentSegment.layerNames[currentSegment.layerNames.IndexOf(current.animationLayer) + 1];

            ReorderMove(
                animation.clips.FindIndex(c => c.animationLayerQualified == current.animationLayerQualified),
                animation.clips.FindLastIndex(c => c.animationLayerQualified == current.animationLayerQualified),
                animation.clips.FindLastIndex(c1 => c1.animationLayer == nextLayer) + 1
            );
        }

        private void DeleteLayer()
        {
            prefabFactory.CreateConfirm("Delete current layer", DeleteLayerConfirm);
        }

        private void DeleteLayerConfirm()
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

        private void ReorderSegmentMoveUp()
        {
            if (current.animationSegment == AtomAnimationClip.SharedAnimationSegment) return;
            if (animation.index.segmentNames[0] == current.animationSegment) return;

            var previousSegment = animation.index.segmentNames[animation.index.segmentNames.IndexOf(current.animationSegment) - 1];

            ReorderMove(
                animation.clips.FindIndex(c => c.animationSegment == current.animationSegment),
                animation.clips.FindLastIndex(c => c.animationSegment == current.animationSegment),
                animation.clips.FindIndex(c1 => c1.animationSegment == previousSegment)
            );
        }

        private void ReorderSegmentMoveDown()
        {
            if (current.animationSegment == AtomAnimationClip.SharedAnimationSegment) return;
            if (animation.index.segmentNames[animation.index.segmentNames.Count - 1] == current.animationSegment) return;

            var nextSegment = animation.index.segmentNames[animation.index.segmentNames.IndexOf(current.animationSegment) + 1];

            ReorderMove(
                animation.clips.FindIndex(c => c.animationSegment == current.animationSegment),
                animation.clips.FindLastIndex(c => c.animationSegment == current.animationSegment),
                animation.clips.FindLastIndex(c1 => c1.animationSegment == nextSegment) + 1
            );
        }

        private void DeleteSegment()
        {
            prefabFactory.CreateConfirm("Delete current segment", DeleteSegmentConfirm);
        }

        private void DeleteSegmentConfirm()
        {
            if (current.animationSegment == AtomAnimationClip.SharedAnimationSegment)
            {
                SuperController.LogError("Timeline: Cannot delete the shared segment.");
                return;
            }
            var clips = currentSegment.layers.SelectMany(c => c).ToList();
            var fallbackClip = animation.clips.First(c => c.animationSegment != current.animationSegment);
            animationEditContext.SelectAnimation(fallbackClip);
            if (animation.isPlaying) animation.playingAnimationSegment = fallbackClip.animationSegment;
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

        private void ReorderMove(int start, int end, int to, int min = 0, int max = int.MaxValue)
        {
            if (to < min || to > max)
            {
                SuperController.LogMessage($"Move range({start}:{end}) to {to} not in range({min}:{max})");
                return;
            }
            var count = end - start + 1;
            var clips = animation.clips.GetRange(start, count);
            animation.clips.RemoveRange(start, count);
            SuperController.LogMessage($"Move range({start}:{end}, count = {count}) to {to}");
            if (to > start) to -= count;
            SuperController.LogMessage($"  - to: {to}");
            animation.clips.InsertRange(to, clips);
            animation.index.Rebuild();
            animation.onClipsListChanged.Invoke();
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
            foreach (var segment in animation.index.segments)
            {
                if (segment.Key != current.animationSegment)
                {
                    sb.Append("<color=grey>");
                }

                if (segment.Key == current.animationSegment) sb.Append("<b>");
                sb.AppendLine($"{(segment.Key == AtomAnimationClip.SharedAnimationSegment ? "[Shared]" : segment.Key)}");
                if (segment.Key == current.animationSegment) sb.Append("</b>");

                foreach (var layer in segment.Value.layers)
                {
                    if (layer[0].animationLayerQualified == current.animationLayerQualified) sb.Append("<b>");
                    sb.AppendLine($"- {layer[0].animationLayer}");
                    if (layer[0].animationLayerQualified == current.animationLayerQualified) sb.Append("</b>");

                    foreach (var clip in layer)
                    {
                        if (clip.animationLayerQualified == current.animationLayerQualified)
                            animationsInLayer++;

                        sb.Append("  - ");
                        if (clip == current) sb.Append("<b>");
                        sb.Append(clip.animationName);
                        if (clip == current) sb.Append("</b>");
                        sb.AppendLine();
                    }
                }

                if (segment.Key != current.animationSegment)
                {
                    sb.Append("</color>");
                }
            }

            _animationsListJSON.val = sb.ToString();
            _deleteAnimationUI.button.interactable = animationsInLayer > 1;
            _deleteLayerUI.button.interactable = currentSegment.layers.Count > 1;
            _deleteSegmentUI.button.interactable = animation.index.segmentNames.Count > 1;
        }

        public override void OnDestroy()
        {
            animation.onClipsListChanged.RemoveListener(RefreshAnimationsList);
            base.OnDestroy();
        }

        #endregion
    }
}

