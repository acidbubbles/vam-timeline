using System.Collections.Generic;
using System.Linq;
using Leap.Unity;
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
            InitAddAnotherUI();
            InitCreateLayerUI();
            InitSplitLayerUI();

            RefreshUI();

            animation.animatables.onTargetsSelectionChanged.AddListener(RefreshUI);
        }

        private void InitCreateAllAnimationsUI()
        {
            _createAllAnimationsJSON = new JSONStorableBool("Same layer animations", false, val =>
            {
                if (val) clipNameJSON.val = current.animationName;
            });
            _createAllAnimationsUI = prefabFactory.CreateToggle(_createAllAnimationsJSON);
        }

        private void InitCreateLayerUI()
        {
            _createLayerUI = prefabFactory.CreateButton("<b>Create new layer</b>");
            _createLayerUI.button.onClick.AddListener(AddLayer);
        }

        private void InitSplitLayerUI()
        {
            _splitLayerUI = prefabFactory.CreateButton("<b>Split targets to new layer</b>");
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
            if (!addAnotherJSON.val) ChangeScreen(TargetsScreen.ScreenName);
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
            {
                animationEditContext.SelectAnimation(clips.FirstOrDefault(c => c.animationName == current.animationName) ?? clips[0]);
                if (!addAnotherJSON.val) ChangeScreen(TargetsScreen.ScreenName);
            }
        }

        #endregion

        protected override void RefreshUI()
        {
            base.RefreshUI();

            var selected = animationEditContext.GetSelectedTargets().ToList();
            if (selected.Count == 0)
            {
                _splitLayerUI.label = "Split targets to new layer";
                clipNameUI.GetComponent<InputField>().interactable = true;
                clipNameJSON.val = current.animationName;
                layerNameJSON.val = animation.GetUniqueLayerName(current, current.animationLayer == "Main" ? "Layer 1" : null);
                _createAllAnimationsJSON.val = false;
                _createAllAnimationsUI.toggle.interactable = true;
            }
            else
            {
                _splitLayerUI.label = $"Split {selected.Count} targets to new layer";
                clipNameJSON.val = "[AUTO]";
                clipNameUI.GetComponent<InputField>().interactable = false;
                layerNameJSON.val = animation.GetUniqueLayerName(current, SuggestName(selected));
                _createAllAnimationsJSON.val = true;
                _createAllAnimationsUI.toggle.interactable = false;
            }
        }

        private string SuggestName(List<IAtomAnimationTarget> selected)
        {
            if (selected.OfType<JSONStorableFloatAnimationTarget>().Count(t => t.animatableRef.IsMorph()) == selected.Count)
                return "Morphs";
            var controllers = selected.OfType<FreeControllerV3AnimationTarget>().ToList();
            if (controllers.Count > 0 && controllers.All(t => t.animatableRef.controller.name.EndsWith("Control")))
            {
                var firstControllerName = controllers[0].animatableRef.controller.name;
                firstControllerName = firstControllerName.Substring(0, firstControllerName.Length - 7);
                // e.g. Head
                if (controllers.Count == 1)
                    return firstControllerName.Capitalize();
                // e.g. Hands
                if (controllers.Count == 2 && controllers[0].animatableRef.controller.name.Substring(1) == controllers[1].animatableRef.controller.name.Substring(1))
                {
                    var result = firstControllerName.Substring(1) + "s";
                    if (result == "Foots") result = "Feet";
                    return result;
                }
                if (controllers.All(c => c.name.StartsWith("penis") || c.name == "testesControl"))
                    return "Penis";
                return "Controls";
            }
            var s = selected[0];
            return selected[0].name.Capitalize();
        }

        protected override void OptionsUpdated()
        {
            var nameValid =
                !string.IsNullOrEmpty(clipNameJSON.val) &&
                current.isOnSharedSegment
                    ? animation.index.segmentsById.Where(kvp => kvp.Key != AtomAnimationClip.SharedAnimationSegmentId)
                        .SelectMany(l => l.Value.allClips)
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
