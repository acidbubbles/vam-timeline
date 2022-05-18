using System.Linq;
using UnityEngine.UI;

namespace VamTimeline
{
    public class AddLayerScreen : AddScreenBase
    {
        public const string ScreenName = "Add layer";

        public override string screenId => ScreenName;

        private JSONStorableBool _createAllAnimationsJSON;
        private UIDynamicButton _createLayerUI;
        private UIDynamicButton _splitLayerUI;
        private UIDynamicToggle _createAllAnimationsUI;

        #region Init

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            prefabFactory.CreateSpacer();
            prefabFactory.CreateHeader("Create layer", 1);

            InitNewClipNameUI();
            InitNewLayerNameUI();
            InitCreateAllAnimationsUI();
            InitCreateInOtherAtomsUI();
            InitCreateLayerUI();
            InitSplitLayerUI();

            RefreshUI();

            animation.animatables.onTargetsSelectionChanged.AddListener(RefreshUI);
        }

        private void InitCreateAllAnimationsUI()
        {
            _createAllAnimationsJSON = new JSONStorableBool("Create same animations as current", false, val =>
            {
                if (val) clipNameJSON.val = current.animationName;
            });
            _createAllAnimationsUI = prefabFactory.CreateToggle(_createAllAnimationsJSON);
        }

        private void InitCreateLayerUI()
        {
            _createLayerUI = prefabFactory.CreateButton("Create new layer");
            _createLayerUI.button.onClick.AddListener(AddLayer);
        }

        private void InitSplitLayerUI()
        {
            _splitLayerUI = prefabFactory.CreateButton("Split targets to new layer");
            _splitLayerUI.button.onClick.AddListener(SplitLayer);
        }

        #endregion

        #region Callbacks

        private void AddLayer()
        {
            AtomAnimationClip clip;
            if (_createAllAnimationsJSON.val)
            {
                var clips = operations.Layers().AddAndCarry(layerNameJSON.val);
                if (createInOtherAtomsJSON.val)
                    foreach (var c in clips)
                        plugin.peers.SendSyncAnimation(c);
                clip = clips[0];
            }
            else
            {
                clip = operations.Layers().Add(clipNameJSON.val, layerNameJSON.val);
            }

            if (clip == null) return;

            animationEditContext.SelectAnimation(clip);
            ChangeScreen(EditAnimationScreen.ScreenName);
        }

        private void SplitLayer()
        {
            var targets = animationEditContext.GetSelectedTargets().ToList();
            if (targets.Count == 0)
            {
                SuperController.LogError("Timeline: You must select a subset of targets to split to another layer.");
                return;
            }

            var clips = operations.Layers().SplitLayer(targets, layerNameJSON.val);
            if (clips.Count > 0)
                animationEditContext.SelectAnimation(clips.FirstOrDefault(c => c.animationName == current.animationName) ?? clips[0]);
        }

        #endregion

        protected override void RefreshUI()
        {
            base.RefreshUI();

            var selected = animationEditContext.GetSelectedTargets().ToList();
            if (selected.Count == 0)
            {
                _splitLayerUI.label = "Split targets to new layer";
                clipNameJSON.val = current.animationName;
                clipNameUI.GetComponent<InputField>().interactable = true;
                layerNameJSON.val = animation.GetUniqueLayerName(current, current.animationLayer == "Main" ? "Layer 1" : null);
                _createAllAnimationsJSON.val = false;
                _createAllAnimationsUI.toggle.interactable = true;
            }
            else
            {
                _splitLayerUI.label = $"Split {selected.Count} targets to new layer";
                clipNameJSON.val = "[AUTO]";
                clipNameUI.GetComponent<InputField>().interactable = false;
                layerNameJSON.val = animation.GetUniqueLayerName(current, selected[0].name);
                _createAllAnimationsJSON.val = true;
                _createAllAnimationsUI.toggle.interactable = false;
            }
        }

        protected override void OptionsUpdated()
        {
            var nameValid =
                !string.IsNullOrEmpty(clipNameJSON.val) &&
                current.isOnSharedSegment
                    ? animation.index.segmentsById.Where(kvp => kvp.Key != AtomAnimationClip.SharedAnimationSegmentId)
                        .SelectMany(l => l.Value.layers)
                        .SelectMany(l => l)
                        .All(c => c.animationName != clipNameJSON.val)
                    : animation.index.ByName(AtomAnimationClip.SharedAnimationSegment, clipNameJSON.val).Count == 0 &&
                !string.IsNullOrEmpty(layerNameJSON.val) &&
                !currentSegment.layerNames.Contains(layerNameJSON.val);

            var selected = animationEditContext.GetSelectedTargets().ToList();
            if (selected.Count == 0)
            {
                _createLayerUI.button.interactable = nameValid;
                _splitLayerUI.button.interactable = false;
            }
            else
            {
                _createLayerUI.button.interactable = false;
                _splitLayerUI.button.interactable = nameValid;
            }
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            animation.animatables.onTargetsSelectionChanged.RemoveListener(RefreshUI);
        }
    }
}
