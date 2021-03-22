using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class AddRemoveTargetsScreen : ScreenBase
    {
        public const string ScreenName = "Edit Targets";

        public override string screenId => ScreenName;

        private JSONStorableStringChooser _addControllerListJSON;
        private UIDynamicPopup _addControllerUI;
        private JSONStorableStringChooser _addStorableListJSON;
        private JSONStorableStringChooser _addParamListJSON;
        private UIDynamicPopup _addStorableListUI;
        private UIDynamicButton _toggleControllerUI;
        private UIDynamicButton _toggleFloatParamUI;
        private UIDynamicPopup _addParamListUI;
        private UIDynamicButton _removeUI;

        #region Init

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            CreateChangeScreenButton($"<b><</b> <i>Back to {TargetsScreen.ScreenName}</i>", TargetsScreen.ScreenName);

            prefabFactory.CreateHeader("Add/remove targets", 1);

            prefabFactory.CreateSpacer();

            prefabFactory.CreateHeader("Triggers", 2);

            InitTriggersUI();

            prefabFactory.CreateSpacer();

            prefabFactory.CreateHeader("Controllers", 2);

            InitControllersUI();

            prefabFactory.CreateSpacer();

            prefabFactory.CreateHeader("Storable floats", 2);

            InitFloatParamsUI();

            prefabFactory.CreateSpacer();

            prefabFactory.CreateHeader("Manage", 2);

            InitFixMissingUI();
            InitRemoveUI();

            prefabFactory.CreateSpacer();

            prefabFactory.CreateHeader("Presets", 2);

            // TODO: Extract
            if (plugin.containingAtom.type == "Person")
            {
                prefabFactory.CreateButton("Add fingers").button.onClick.AddListener(() =>
                {
                    var leftHandControl = plugin.containingAtom.GetStorableByID("LeftHandControl");
                    leftHandControl.SetStringChooserParamValue("fingerControlMode", "JSONParams");
                    var leftHandFingerControl = plugin.containingAtom.GetStorableByID("LeftHandFingerControl");
                    foreach (var paramName in leftHandFingerControl.GetFloatParamNames())
                        AddFloatParam(leftHandFingerControl, leftHandFingerControl.GetFloatJSONParam(paramName));

                    var rightHandControl = plugin.containingAtom.GetStorableByID("RightHandControl");
                    rightHandControl.SetStringChooserParamValue("fingerControlMode", "JSONParams");
                    var rightHandFingerControl = plugin.containingAtom.GetStorableByID("RightHandFingerControl");
                    foreach (var paramName in rightHandFingerControl.GetFloatParamNames())
                        AddFloatParam(rightHandFingerControl, rightHandFingerControl.GetFloatJSONParam(paramName));
                });
            }

            UpdateSelectDependentUI();
            current.onTargetsListChanged.AddListener(OnTargetsListChanged);
            animationEditContext.onTargetsSelectionChanged.AddListener(UpdateSelectDependentUI);
        }

        private void OnTargetsListChanged()
        {
            RefreshControllersList();
            RefreshStorableFloatsList();

            // don't change the param selection if it's still in the list
            if (!_addParamListJSON.choices.Contains(_addParamListJSON.val))
                _addParamListJSON.valNoCallback = _addParamListJSON.choices.FirstOrDefault() ?? "";

            UpdateSelectDependentUI();
        }

        private void InitFixMissingUI()
        {
            if (animation.index.ByLayer(current.animationLayer).Count() <= 1) return;

            foreach (var clip in animation.index.ByLayer(current.animationLayer))
            {
                foreach (var target in clip.targetFloatParams)
                {
                    target.EnsureAvailable(false);
                }
            }

            var clipList = current.GetAllTargets()
                .Where(t => !(t is TriggersAnimationTarget))
                .Select(t => t.name)
                .OrderBy(x => x);
            var otherList = animation.index
                .ByLayer(current.animationLayer)
                .Where(c => c != current)
                .SelectMany(c => c.GetAllTargets().Where(t => !(t is TriggersAnimationTarget)))
                .Select(t => t.name)
                .Distinct()
                .OrderBy(x => x);
            var ok = clipList.SequenceEqual(otherList);
            if (ok) return;

            prefabFactory.CreateSpacer();
            UIDynamicButton enableAllTargetsUI = null;
            UIDynamic spacerUI = null;
            enableAllTargetsUI = prefabFactory.CreateButton("Add all other animations' targets");
            enableAllTargetsUI.button.onClick.AddListener(() =>
            {
                AddMissingTargets();
                Destroy(enableAllTargetsUI);
                Destroy(spacerUI);
            });
            enableAllTargetsUI.buttonColor = Color.yellow;
        }

        private void InitTriggersUI()
        {
            var btn = prefabFactory.CreateButton("Add triggers track");
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
            _addControllerListJSON = new JSONStorableStringChooser("Controller", new List<string>(), "", "Controller");
            _addControllerUI = prefabFactory.CreatePopup(_addControllerListJSON, true, true);
            _addControllerUI.popupPanelHeight = 740f;

            _toggleControllerUI = prefabFactory.CreateButton("Add");
            _toggleControllerUI.button.onClick.AddListener(AddAnimatedController);

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
                ?? controllers.FirstOrDefault(c => c != "control" && c != "");
        }

        private void InitFloatParamsUI()
        {
            _addStorableListJSON = new JSONStorableStringChooser("Storable", new List<string>(), "", "Storable", (string name) =>
            {
                RefreshStorableFloatsList();
                _addParamListJSON.valNoCallback = _addParamListJSON.choices.FirstOrDefault() ?? "";
            });
            _addStorableListUI = prefabFactory.CreatePopup(_addStorableListJSON, true, true);
            _addStorableListUI.popupPanelHeight = 450f;
            _addStorableListUI.popup.onOpenPopupHandlers += RefreshStorablesList;

            _addParamListJSON = new JSONStorableStringChooser("Params", new List<string>(), "", "Param");
            _addParamListUI = prefabFactory.CreatePopup(_addParamListJSON, true, true);
            _addParamListUI.popup.onOpenPopupHandlers += RefreshStorableFloatsList;
            _addParamListUI.popupPanelHeight = 320f;

            _toggleFloatParamUI = prefabFactory.CreateButton("Add");
            _toggleFloatParamUI.button.onClick.AddListener(AddAnimatedFloatParam);

            RefreshStorablesList();
            RefreshStorableFloatsList();

            var character = plugin.containingAtom.GetComponentInChildren<DAZCharacterSelector>();
            if (character != null)
            {
                var makeMorphsAnimatableUI = prefabFactory.CreateButton("<i>Fav morphs & Resync in Control tab</i>");
                makeMorphsAnimatableUI.button.onClick.AddListener(() =>
                {
                    SuperController.singleton.SelectController(plugin.containingAtom.freeControllers.First(f => f.name == "control"));
                    var selector = plugin.containingAtom.gameObject.GetComponentInChildren<UITabSelector>();
                    if (selector == null)
                        SuperController.LogError("Could not find the tabs selector");
                    else if (character.selectedCharacter.isMale)
                        selector.SetActiveTab("Male Morphs");
                    else
                        selector.SetActiveTab("Female Morphs");
                });
            }
        }

        private void RefreshStorablesList()
        {
            if (_addStorableListJSON == null) return;
            _addStorableListJSON.choices = GetStorablesWithFloatParams().ToList();
            if (string.IsNullOrEmpty(_addParamListJSON.val))
                _addStorableListJSON.valNoCallback = _addStorableListJSON.choices.Contains("geometry") ? "geometry" : _addStorableListJSON.choices.FirstOrDefault();
        }

        private IEnumerable<string> GetStorablesWithFloatParams()
        {
            yield return "";
            foreach (var storableId in plugin.containingAtom.GetStorableIDs().OrderBy(s => s))
            {
                if (storableId.StartsWith("hairTool")) continue;
                var storable = plugin.containingAtom.GetStorableByID(storableId);
                if (storable == null) continue;
                if ((storable.GetFloatParamNames()?.Count ?? 0) > 0)
                    yield return storableId;
            }
        }

        private void RefreshStorableFloatsList()
        {
            if (_addStorableListJSON == null) return;

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
                .Where(t => t.storableId == storable.storeId)
                .Select(t => t.floatParamName));
            _addParamListJSON.choices = values
                .Where(v => !current.targetFloatParams.Any(t => t.storableId == storable.storeId && t.floatParamName == v))
                .Where(v => !reservedByOtherLayers.Contains(v))
                .OrderBy(v => v)
                .ToList();
            if (string.IsNullOrEmpty(_addParamListJSON.val))
                _addParamListJSON.valNoCallback = _addParamListJSON.choices.FirstOrDefault();
        }

        private void InitRemoveUI()
        {
            _removeUI = prefabFactory.CreateButton("Remove selected");
            _removeUI.buttonColor = Color.red;
            _removeUI.textColor = Color.white;
            _removeUI.button.onClick.AddListener(RemoveSelected);
        }

        private void RemoveSelected()
        {
            var selected = animationEditContext.GetSelectedTargets().ToList();
            foreach (var s in selected)
            {
                // We remove every selected target on every clip on the current layer, except triggers
                foreach (var clip in animation.index.ByLayer(current.animationLayer))
                {
                    var target = clip.GetAllTargets().Where(t => !(t is TriggersAnimationTarget)).FirstOrDefault(t => t.TargetsSameAs(s));
                    if (target == null) continue;
                    clip.Remove(target);
                }

                // We remove the selected  trigger targets
                if (s is TriggersAnimationTarget)
                {
                    // So other clips won't keep the deleted selection
                    animationEditContext.SetSelected(s, false);
                    current.Remove(s);
                }

                {
                    var target = s as FreeControllerAnimationTarget;
                    if (target != null)
                    {
                        _addControllerListJSON.val = target.name;
                    }
                }
                {
                    var target = s as FloatParamAnimationTarget;
                    if (target != null)
                    {
                        _addStorableListJSON.val = target.storableId;
                        _addParamListJSON.val = target.floatParamName;
                    }
                }
            }

            // Ensures shows on top
            _addControllerListJSON.popup.visible = true;
            _addControllerListJSON.popup.visible = false;
            _addStorableListJSON.popup.visible = true;
            _addStorableListJSON.popup.visible = false;
            _addParamListJSON.popup.visible = true;
            _addParamListJSON.popup.visible = false;

            animationEditContext.DeselectAll();
        }

        private void UpdateSelectDependentUI()
        {
            var count = animationEditContext.GetSelectedTargets().Count();
            _removeUI.button.interactable = count > 0;
            _removeUI.buttonText.text = count == 0 ? "Remove selected targets" : $"Remove {count} targets";
        }

        #endregion

        #region Callbacks

        private class FloatParamRef
        {
            public JSONStorable storable { get; set; }
            public JSONStorableFloat floatParam { get; set; }
        }

        private void AddMissingTargets()
        {
            try
            {
                var allControllers = animation.index
                    .ByLayer(current.animationLayer)
                    .SelectMany(c => c.targetControllers)
                    .Select(t => t.controller)
                    .Distinct()
                    .ToList();
                var h = new HashSet<JSONStorableFloat>();
                var allFloatParams = animation.index
                    .ByLayer(current.animationLayer)
                    .SelectMany(c => c.targetFloatParams)
                    .Where(t => t.EnsureAvailable(false))
                    .Where(t => h.Add(t.floatParam))
                    .Select(t => new FloatParamRef { storable = t.storable, floatParam = t.floatParam })
                    .ToList();

                foreach (var clip in animation.clips)
                {
                    foreach (var controller in allControllers)
                    {
                        if (clip.targetControllers.All(t => t.controller != controller))
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
                        if (clip.targetFloatParams.Any(t => t.floatParamName == floatParamRef.floatParam.name)) continue;
                        var target = clip.Add(floatParamRef.storable, floatParamRef.floatParam);
                        if (target == null) continue;
                        if (!target.EnsureAvailable(false)) continue;
                        target.SetKeyframe(0f, floatParamRef.floatParam.val);
                        target.SetKeyframe(clip.animationLength, floatParamRef.floatParam.val);
                    }
                    clip.targetFloatParams.Sort(new FloatParamAnimationTarget.Comparer());
                }
            }
            catch (Exception exc)
            {
                SuperController.LogError($"Timeline.{nameof(AddRemoveTargetsScreen)}.{nameof(AddMissingTargets)}: {exc}");
            }
        }

        private void AddAnimatedController()
        {
            try
            {
                var uid = _addControllerListJSON.val;
                if (string.IsNullOrEmpty(uid)) return;

                SelectNextInList(_addControllerListJSON);

                var controller = plugin.containingAtom.freeControllers.FirstOrDefault(x => x.name == uid);
                if (controller == null)
                {
                    SuperController.LogError($"Timeline: Controller {uid} in atom {plugin.containingAtom.uid} does not exist");
                    return;
                }

                if (current.targetControllers.Any(c => c.controller == controller))
                {
                    SuperController.LogError($"Timeline: Controller {uid} in atom {plugin.containingAtom.uid} was already added");
                    return;
                }

                if (controller.currentPositionState == FreeControllerV3.PositionState.Off && controller.currentRotationState == FreeControllerV3.RotationState.Off)
                {
                    controller.currentPositionState = FreeControllerV3.PositionState.On;
                    controller.currentRotationState = FreeControllerV3.RotationState.On;
                }

                foreach (var clip in animation.index.ByLayer(current.animationLayer))
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
                SuperController.LogError($"Timeline.{nameof(AddRemoveTargetsScreen)}.{nameof(AddAnimatedController)}: " + exc);
            }
        }

        private void AddAnimatedFloatParam()
        {
            try
            {
                var storableId = _addStorableListJSON.val;
                var floatParamName = _addParamListJSON.val;

                if (string.IsNullOrEmpty(storableId)) return;
                if (string.IsNullOrEmpty(floatParamName)) return;

                var storable = plugin.containingAtom.GetStorableByID(storableId);
                if (storable == null)
                {
                    SuperController.LogError($"Timeline: Storable {storableId} in atom {plugin.containingAtom.uid} does not exist");
                    return;
                }

                var sourceFloatParam = storable.GetFloatJSONParam(floatParamName);
                if (sourceFloatParam == null)
                {
                    SuperController.LogError($"Timeline: Param {floatParamName} in atom {plugin.containingAtom.uid} does not exist");
                    return;
                }

                SelectNextInList(_addParamListJSON);

                AddFloatParam(storable, sourceFloatParam);
            }
            catch (Exception exc)
            {
                SuperController.LogError($"Timeline.{nameof(AddRemoveTargetsScreen)}.{nameof(AddAnimatedFloatParam)}: " + exc);
            }
        }

        private bool AddFloatParam(JSONStorable storable, JSONStorableFloat jsf)
        {
            if (current.targetFloatParams.Any(c => c.floatParam == jsf))
                return false;

            foreach (var clip in animation.index.ByLayer(current.animationLayer))
            {
                var added = clip.Add(storable, jsf);
                if (added == null) continue;

                added.SetKeyframe(0f, jsf.val);
                added.SetKeyframe(clip.animationLength, jsf.val);
            }

            return true;
        }

        private static void SelectNextInList(JSONStorableStringChooser list)
        {
            var currentIndex = -1;

            // getting index of current selection
            for (var i = 0; i < list.choices.Count; ++i)
            {
                if (list.choices[i] == list.val)
                {
                    currentIndex = i;
                    break;
                }
            }

            if (currentIndex == -1)
            {
                // not found?
                return;
            }

            if (currentIndex == list.choices.Count - 1)
            {
                // value was last in list
                if (list.choices.Count <= 1)
                {
                    // and that was the last value, clear
                    list.val = "";
                }
                else
                {
                    // select next to last
                    var newIndex = list.choices.Count - 2;
                    list.val = list.choices[newIndex];
                }
            }
            else
            {
                // select next in list
                var newIndex = currentIndex + 1;
                list.val = list.choices[newIndex];
            }
        }

        #endregion

        #region Events

        protected override void OnCurrentAnimationChanged(AtomAnimationEditContext.CurrentAnimationChangedEventArgs args)
        {
            args.before.onTargetsListChanged.RemoveListener(OnTargetsListChanged);
            args.after.onTargetsListChanged.AddListener(OnTargetsListChanged);

            base.OnCurrentAnimationChanged(args);

            UpdateSelectDependentUI();
        }

        public void OnEnable()
        {
            RefreshStorablesList();
        }

        public override void OnDestroy()
        {
            if (_addParamListUI != null) _addParamListUI.popup.onOpenPopupHandlers -= RefreshStorableFloatsList;
            current.onTargetsListChanged.RemoveListener(OnTargetsListChanged);
            animationEditContext.onTargetsSelectionChanged.RemoveListener(UpdateSelectDependentUI);
            base.OnDestroy();
        }

        #endregion
    }
}

