using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class TargetsScreen : ScreenBase
    {
        public const string ScreenName = "Targets";

        public override string screenId => ScreenName;

        private readonly List<JSONStorableBool> _removeToggles = new List<JSONStorableBool>();
        private JSONStorableStringChooser _addControllerListJSON;
        private UIDynamicPopup _addControllerUI;
        private JSONStorableStringChooser _addStorableListJSON;
        private JSONStorableStringChooser _addParamListJSON;
        private UIDynamicPopup _addStorableListUI;
        private UIDynamicButton _toggleControllerUI;
        private UIDynamicButton _toggleFloatParamUI;
        private UIDynamicPopup _addParamListUI;
        private UIDynamicButton _removeUI;

        public TargetsScreen()
            : base()
        {
        }

        #region Init

        public override void Init(IAtomPlugin plugin)
        {
            base.Init(plugin);

            CreateChangeScreenButton("<b><</b> <i>Back to Edit</i>", EditScreen.ScreenName);

            prefabFactory.CreateSpacer();

            InitTriggersUI();

            prefabFactory.CreateSpacer();

            InitControllersUI();

            prefabFactory.CreateSpacer();

            InitFloatParamsUI();

            prefabFactory.CreateSpacer();

            InitFixMissingUI();

            prefabFactory.CreateSpacer();

            InitRemoveUI();

            UpdateRemoveUI();

            current.onTargetsListChanged.AddListener(OnTargetsListChanged);
            current.onTargetsSelectionChanged.AddListener(UpdateRemoveUI);
        }

        private void OnTargetsListChanged()
        {
            RefreshControllersList();
            RefreshStorableFloatsList();
            _addParamListJSON.valNoCallback = _addParamListJSON.choices.FirstOrDefault() ?? "";
            UpdateRemoveUI();
        }

        private void InitFixMissingUI()
        {
            if (animation.clips.Count <= 1) return;

            var clipList = current.allTargets.Select(t => t.name).OrderBy(x => x);
            var otherList = animation.clips
                .Where(c => c != current && c.animationLayer == current.animationLayer)
                .SelectMany(c => c.allTargets)
                .Select(t => t.name)
                .Distinct()
                .OrderBy(x => x);
            var ok = clipList.SequenceEqual(otherList);
            if (ok) return;

            UIDynamicButton enableAllTargetsUI = null;
            UIDynamic spacerUI = null;
            enableAllTargetsUI = prefabFactory.CreateButton("Add All Other Animations' Targets");
            enableAllTargetsUI.button.onClick.AddListener(() =>
            {
                AddMissingTargets();
                Destroy(enableAllTargetsUI);
                Destroy(spacerUI);
            });
            enableAllTargetsUI.buttonColor = Color.yellow;

            spacerUI = prefabFactory.CreateSpacer();
        }

        private void InitTriggersUI()
        {
            var btn = prefabFactory.CreateButton("Add Triggers Track");
            btn.button.onClick.AddListener(() =>
            {
                var target = new TriggersAnimationTarget
                {
                    name = $"Triggers {current.targetTriggers.Count + 1}"
                };
                target.AddEdgeFramesIfMissing(current.animationLength);
                current.Add(target);
            });
        }

        private void InitControllersUI()
        {
            _addControllerListJSON = new JSONStorableStringChooser("Animate Controller", new List<string>(), "", "Animate controller")
            {
                isStorable = false
            };
            _addControllerUI = prefabFactory.CreateScrollablePopup(_addControllerListJSON);
            _addControllerUI.popupPanelHeight = 900f;

            _toggleControllerUI = prefabFactory.CreateButton("Add Controller");
            _toggleControllerUI.button.onClick.AddListener(() => AddAnimatedController());

            RefreshControllersList();
        }

        private IEnumerable<string> GetEligibleFreeControllers()
        {
            yield return "";
            var reservedByOtherLayers = new HashSet<FreeControllerV3>(animation.clips
                .Where(c => c.animationLayer != current.animationLayer)
                .SelectMany(c => c.targetControllers)
                .Select(t => t.controller));
            foreach (var fc in plugin.containingAtom.freeControllers)
            {
                if (!fc.name.EndsWith("Control") && fc.name != "control") continue;
                if (current.targetControllers.Any(c => c.controller == fc)) continue;
                if (reservedByOtherLayers.Contains(fc)) continue;
                yield return fc.name;
            }
        }

        private void RefreshControllersList()
        {
            var controllers = GetEligibleFreeControllers().ToList();
            _addControllerListJSON.choices = controllers;
            if (!string.IsNullOrEmpty(_addControllerListJSON.val))
                return;

            if (controllers.Count == 2)
            {
                _addControllerListJSON.val = controllers[1];
                return;
            }

            var preferredSelection = new[] { "headControl", "lHandControl", "rHandControl", "hipControl", "chestControl" };
            _addControllerListJSON.val =
                preferredSelection.FirstOrDefault(pref => controllers.Contains(pref))
                ?? controllers.Where(c => c != "control" && c != "").FirstOrDefault();
        }

        private void InitFloatParamsUI()
        {
            var storables = GetStorablesWithFloatParams().ToList();
            _addStorableListJSON = new JSONStorableStringChooser("Animate Storable", storables, storables.Contains("geometry") ? "geometry" : storables.FirstOrDefault(), "Animate Storable", (string name) =>
            {
                RefreshStorableFloatsList();
                _addParamListJSON.valNoCallback = _addParamListJSON.choices.FirstOrDefault() ?? "";
            })
            {
                isStorable = false
            };
            _addStorableListUI = prefabFactory.CreateScrollablePopup(_addStorableListJSON);
            _addStorableListUI.popupPanelHeight = 700f;
            _addStorableListUI.popup.onOpenPopupHandlers += RefreshStorablesList;

            _addParamListJSON = new JSONStorableStringChooser("Animate Param", new List<string> { "" }, "", "Animate Param")
            {
                isStorable = false
            };
            _addParamListUI = prefabFactory.CreateScrollablePopup(_addParamListJSON);
            _addParamListUI.popup.onOpenPopupHandlers += RefreshStorableFloatsList;
            _addParamListUI.popupPanelHeight = 600f;

            _toggleFloatParamUI = prefabFactory.CreateButton("Add Param");
            _toggleFloatParamUI.button.onClick.AddListener(() => AddAnimatedFloatParam());

            RefreshStorableFloatsList();
            _addParamListJSON.valNoCallback = _addParamListJSON.choices.FirstOrDefault() ?? "";

            var character = plugin.containingAtom.GetComponentInChildren<DAZCharacterSelector>();
            if (character != null)
            {
                var makeMorphsAnimatableUI = prefabFactory.CreateButton("<i>Add morphs (Make Animatable)</i>");
                makeMorphsAnimatableUI.button.onClick.AddListener(() =>
                {
                    var selector = plugin.containingAtom.gameObject.GetComponentInChildren<UITabSelector>();
                    if (character.selectedCharacter.isMale)
                        selector.SetActiveTab("Male Morphs");
                    else
                        selector.SetActiveTab("Female Morphs");
                });
            }
        }

        private void RefreshStorablesList()
        {
            _addStorableListJSON.choices = GetStorablesWithFloatParams().ToList();
        }

        private IEnumerable<string> GetStorablesWithFloatParams()
        {
            yield return "";
            foreach (var storableId in plugin.containingAtom.GetStorableIDs().OrderBy(s => s))
            {
                if (storableId.StartsWith("hairTool")) continue;
                var storable = plugin.containingAtom.GetStorableByID(storableId);
                if (storable == null) continue;
                if (ReferenceEquals(storable, plugin)) continue;
                if ((storable.GetFloatParamNames()?.Count ?? 0) > 0)
                    yield return storableId;
            }
        }

        private void RefreshStorableFloatsList()
        {
            if (string.IsNullOrEmpty(_addStorableListJSON.val))
            {
                _addParamListJSON.choices = new List<string>();
                return;
            }

            var storable = plugin.containingAtom.GetStorableByID(_addStorableListJSON.val);

            if (storable == null)
            {
                _addParamListJSON.choices = new List<string>();
                return;
            }

            var values = storable.GetFloatParamNames() ?? new List<string>();
            var reservedByOtherLayers = new HashSet<string>(animation.clips
                .Where(c => c.animationLayer != current.animationLayer)
                .SelectMany(c => c.targetFloatParams)
                .Where(t => t.storable == storable)
                .Select(t => t.floatParam.name));
            _addParamListJSON.choices = values
                .Where(v => !current.targetFloatParams.Any(t => t.storable == storable && t.floatParam.name == v))
                .Where(v => !reservedByOtherLayers.Contains(v))
                .OrderBy(v => v)
                .ToList();
        }

        private void InitRemoveUI()
        {
            _removeUI = prefabFactory.CreateButton("Remove Selected");
            _removeUI.buttonColor = Color.red;
            _removeUI.textColor = Color.white;
            _removeUI.button.onClick.AddListener(RemoveSelected);

            UpdateRemoveUI();
        }

        private void RemoveSelected()
        {
            var selected = current.GetSelectedTargets().ToList();
            foreach (var s in selected)
            {
                {
                    var target = s as FreeControllerAnimationTarget;
                    if (target != null)
                    {
                        _addControllerListJSON.val = target.name;
                        current.Remove(target);
                        continue;
                    }
                }
                {
                    var target = s as FloatParamAnimationTarget;
                    if (target != null)
                    {
                        _addStorableListJSON.val = target.storable.name;
                        _addParamListJSON.val = target.floatParam.name;
                        current.Remove(target);
                        continue;
                    }
                }
                {
                    var target = s as TriggersAnimationTarget;
                    if (target != null)
                    {
                        current.Remove(target);
                        continue;
                    }
                }
                throw new NotSupportedException($"Removing target is not supported: {s}");
            }

            // Ensures shows on top
            _addControllerListJSON.popup.visible = true;
            _addControllerListJSON.popup.visible = false;
            _addStorableListJSON.popup.visible = true;
            _addStorableListJSON.popup.visible = false;
            _addParamListJSON.popup.visible = true;
            _addParamListJSON.popup.visible = false;
        }

        private void UpdateRemoveUI()
        {
            var count = current.GetSelectedTargets().Count();
            _removeUI.button.interactable = count > 0;
            _removeUI.buttonText.text = count == 0 ? "Remove Selected Targets" : $"Remove {count} Targets";
        }

        #endregion

        #region Callbacks

        private class FloatParamRef
        {
            public JSONStorable Storable { get; set; }
            public JSONStorableFloat FloatParam { get; set; }
        }

        private void AddMissingTargets()
        {
            try
            {
                var allControllers = animation.clips
                    .Where(c => c.animationLayer == current.animationLayer)
                    .SelectMany(c => c.targetControllers)
                    .Select(t => t.controller)
                    .Distinct()
                    .ToList();
                var h = new HashSet<JSONStorableFloat>();
                var allFloatParams = animation.clips
                    .Where(c => c.animationLayer == current.animationLayer)
                    .SelectMany(c => c.targetFloatParams)
                    .Where(t => h.Add(t.floatParam))
                    .Select(t => new FloatParamRef { Storable = t.storable, FloatParam = t.floatParam })
                    .ToList();

                foreach (var clip in animation.clips)
                {
                    foreach (var controller in allControllers)
                    {
                        if (!clip.targetControllers.Any(t => t.controller == controller))
                        {
                            var target = clip.Add(controller);
                            if (target != null)
                            {
                                target.SetKeyframeToCurrentTransform(0f);
                                target.SetKeyframeToCurrentTransform(clip.animationLength);
                            }
                        }
                    }
                    clip.targetControllers.Sort(new FreeControllerAnimationTarget.Comparer());

                    foreach (var floatParamRef in allFloatParams)
                    {
                        if (!clip.targetFloatParams.Any(t => t.floatParam == floatParamRef.FloatParam))
                        {
                            var target = clip.Add(floatParamRef.Storable, floatParamRef.FloatParam);
                            if (target != null)
                            {
                                target.SetKeyframe(0f, floatParamRef.FloatParam.val);
                                target.SetKeyframe(clip.animationLength, floatParamRef.FloatParam.val);
                            }
                        }
                    }
                    clip.targetFloatParams.Sort(new FloatParamAnimationTarget.Comparer());
                }
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(TargetsScreen)}.{nameof(AddMissingTargets)}: {exc}");
            }
        }

        private void AddAnimatedController()
        {
            try
            {
                var uid = _addControllerListJSON.val;
                if (string.IsNullOrEmpty(uid)) return;
                var controller = plugin.containingAtom.freeControllers.Where(x => x.name == uid).FirstOrDefault();
                if (controller == null)
                {
                    SuperController.LogError($"VamTimeline: Controller {uid} in atom {plugin.containingAtom.uid} does not exist");
                    return;
                }

                _addControllerListJSON.valNoCallback = "";

                if (current.targetControllers.Any(c => c.controller == controller))
                    return;

                controller.currentPositionState = FreeControllerV3.PositionState.On;
                controller.currentRotationState = FreeControllerV3.RotationState.On;

                foreach (var clip in animation.clips.Where(c => c.animationLayer == current.animationLayer))
                {
                    var added = clip.Add(controller);
                    if (added != null)
                    {
                        added.SetKeyframeToCurrentTransform(0f);
                        added.SetKeyframeToCurrentTransform(clip.animationLength);
                        if (!clip.loop)
                            added.ChangeCurve(clip.animationLength, CurveTypeValues.CopyPrevious);
                    }
                }
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(TargetsScreen)}.{nameof(AddAnimatedController)}: " + exc);
            }
        }

        private void AddAnimatedFloatParam()
        {
            try
            {
                if (string.IsNullOrEmpty(_addStorableListJSON.val)) return;
                if (string.IsNullOrEmpty(_addParamListJSON.val)) return;

                var storable = plugin.containingAtom.GetStorableByID(_addStorableListJSON.val);
                if (storable == null)
                {
                    SuperController.LogError($"VamTimeline: Storable {_addStorableListJSON.val} in atom {plugin.containingAtom.uid} does not exist");
                    return;
                }

                var sourceFloatParam = storable.GetFloatJSONParam(_addParamListJSON.val);
                if (sourceFloatParam == null)
                {
                    SuperController.LogError($"VamTimeline: Param {_addParamListJSON.val} in atom {plugin.containingAtom.uid} does not exist");
                    return;
                }

                _addParamListJSON.valNoCallback = "";

                if (current.targetFloatParams.Any(c => c.floatParam == sourceFloatParam))
                    return;

                foreach (var clip in animation.clips.Where(c => c.animationLayer == current.animationLayer))
                {
                    var added = clip.Add(storable, sourceFloatParam);
                    if (added != null)
                    {
                        added.SetKeyframe(0f, sourceFloatParam.val);
                        added.SetKeyframe(clip.animationLength, sourceFloatParam.val);
                    }
                }
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(TargetsScreen)}.{nameof(AddAnimatedFloatParam)}: " + exc);
            }
        }

        private void RemoveAnimatedController(FreeControllerAnimationTarget target)
        {
            try
            {
                foreach (var clip in animation.clips.Where(c => c.animationLayer == current.animationLayer))
                    clip.Remove(target.controller);
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(TargetsScreen)}.{nameof(RemoveAnimatedController)}: " + exc);
            }
        }

        private void RemoveFloatParam(FloatParamAnimationTarget target)
        {
            try
            {
                foreach (var clip in animation.clips.Where(c => c.animationLayer == current.animationLayer))
                    clip.Remove(target.storable, target.floatParam);
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(TargetsScreen)}.{nameof(RemoveFloatParam)}: " + exc);
            }
        }

        #endregion

        #region Events

        protected override void OnCurrentAnimationChanged(AtomAnimation.CurrentAnimationChangedEventArgs args)
        {
            args.before.onTargetsListChanged.RemoveListener(OnTargetsListChanged);
            args.before.onTargetsSelectionChanged.RemoveListener(UpdateRemoveUI);
            args.after.onTargetsListChanged.AddListener(OnTargetsListChanged);
            args.after.onTargetsSelectionChanged.AddListener(UpdateRemoveUI);

            base.OnCurrentAnimationChanged(args);

            UpdateRemoveUI();
        }

        public override void OnDestroy()
        {
            if (_addParamListUI != null) _addParamListUI.popup.onOpenPopupHandlers -= RefreshStorableFloatsList;
            current.onTargetsListChanged.RemoveListener(OnTargetsListChanged);
            current.onTargetsSelectionChanged.RemoveListener(UpdateRemoveUI);
            base.OnDestroy();
        }

        #endregion
    }
}

