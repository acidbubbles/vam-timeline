using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class TargetsScreen : ScreenBase
    {
        public const string ScreenName = "Targets";

        public override string name => ScreenName;

        private readonly List<JSONStorableBool> _removeToggles = new List<JSONStorableBool>();
        private JSONStorableStringChooser _addControllerListJSON;
        private UIDynamicPopup _addControllerUI;
        private JSONStorableStringChooser _addStorableListJSON;
        private JSONStorableStringChooser _addParamListJSON;
        private UIDynamicPopup _addStorableListUI;
        private UIDynamicButton _toggleControllerUI;
        private UIDynamicButton _toggleFloatParamUI;
        private UIDynamicPopup _addParamListUI;

        public TargetsScreen(IAtomPlugin plugin)
            : base(plugin)
        {

        }

        #region Init

        public override void Init()
        {
            base.Init();

            // Left side

            // Right side

            CreateChangeScreenButton("<b><</b> <i>Back to Edit</i>", EditScreen.ScreenName, true);

            CreateSpacer(true);

            InitControllersUI();

            CreateSpacer(true);

            InitFloatParamsUI(true);

            CreateSpacer(true);

            InitFixMissingUI();

            GenerateRemoveToggles();

            current.onTargetsListChanged.AddListener(OnTargetsListChanged);
        }

        private void OnTargetsListChanged()
        {
            RefreshControllersList();
            RefreshStorableFloatsList();
            _addParamListJSON.valNoCallback = _addParamListJSON.choices.FirstOrDefault() ?? "";
            GenerateRemoveToggles();
        }

        private void InitFixMissingUI()
        {
            if (plugin.animation.Clips.Count <= 1) return;

            var clipList = current.AllTargets.Select(t => t.name).OrderBy(x => x);
            var otherList = plugin.animation.Clips.Where(c => c != current).SelectMany(c => c.AllTargets).Select(t => t.name).OrderBy(x => x).Distinct();
            var ok = clipList.SequenceEqual(otherList);
            if (ok) return;

            UIDynamicButton enableAllTargetsUI = null;
            UIDynamic spacerUI = null;
            enableAllTargetsUI = plugin.CreateButton("Add All Other Animations' Targets", true);
            enableAllTargetsUI.button.onClick.AddListener(() =>
            {
                EnableAllTargets();
                plugin.RemoveButton(enableAllTargetsUI);
                plugin.RemoveSpacer(spacerUI);
            });
            enableAllTargetsUI.buttonColor = Color.yellow;
            RegisterComponent(enableAllTargetsUI);

            spacerUI = CreateSpacer(true);
        }

        private void InitControllersUI()
        {
            _addControllerListJSON = new JSONStorableStringChooser("Animate Controller", new List<string>(), "", "Animate controller")
            {
                isStorable = false
            };
            RegisterStorable(_addControllerListJSON);
            _addControllerUI = plugin.CreateScrollablePopup(_addControllerListJSON, true);
            _addControllerUI.popupPanelHeight = 900f;
            RegisterComponent(_addControllerUI);

            _toggleControllerUI = plugin.CreateButton("Add Controller", true);
            _toggleControllerUI.button.onClick.AddListener(() => AddAnimatedController());
            RegisterComponent(_toggleControllerUI);

            RefreshControllersList();
        }

        private IEnumerable<string> GetEligibleFreeControllers()
        {
            yield return "";
            foreach (var fc in plugin.containingAtom.freeControllers)
            {
                if (fc.name == "control") yield return fc.name;
                if (!fc.name.EndsWith("Control")) continue;
                if (current.TargetControllers.Any(c => c.controller == fc)) continue;
                yield return fc.name;
            }
        }

        private void RefreshControllersList()
        {
            var controllers = GetEligibleFreeControllers().ToList();
            _addControllerListJSON.choices = controllers;
            if (!string.IsNullOrEmpty(_addControllerListJSON.val))
                return;

            if (controllers.Count == 1)
            {
                _addControllerListJSON.val = controllers[0];
                return;
            }

            var preferredSelection = new[] { "headControl", "lHandControl", "rHandControl", "hipControl", "chestControl" };
            _addControllerListJSON.val = preferredSelection
                .FirstOrDefault(pref => controllers.Contains(pref)) ?? controllers
                .Where(c => c != "control" && c != "")
                .FirstOrDefault();
        }

        private void InitFloatParamsUI(bool rightSide)
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
            RegisterStorable(_addStorableListJSON);
            _addStorableListUI = plugin.CreateScrollablePopup(_addStorableListJSON, rightSide);
            _addStorableListUI.popupPanelHeight = 700f;
            _addStorableListUI.popup.onOpenPopupHandlers += RefreshStorablesList;
            RegisterComponent(_addStorableListUI);

            _addParamListJSON = new JSONStorableStringChooser("Animate Param", new List<string> { "" }, "", "Animate Param")
            {
                isStorable = false
            };
            RegisterStorable(_addParamListJSON);
            _addParamListUI = plugin.CreateScrollablePopup(_addParamListJSON, rightSide);
            _addParamListUI.popup.onOpenPopupHandlers += RefreshStorableFloatsList;
            _addParamListUI.popupPanelHeight = 600f;
            RegisterComponent(_addParamListUI);

            _toggleFloatParamUI = plugin.CreateButton("Add Param", rightSide);
            _toggleFloatParamUI.button.onClick.AddListener(() => AddAnimatedFloatParam());
            RegisterComponent(_toggleFloatParamUI);

            RefreshStorableFloatsList();
            _addParamListJSON.valNoCallback = _addParamListJSON.choices.FirstOrDefault() ?? "";

            var character = plugin.containingAtom.GetComponentInChildren<DAZCharacterSelector>();
            if (character != null)
            {
                var makeMorphsAnimatableUI = plugin.CreateButton("<i>Add morphs (Make Animatable)</i>", rightSide);
                RegisterComponent(makeMorphsAnimatableUI);
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
                if (UnityEngine.Object.ReferenceEquals(storable, plugin)) continue;
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
            _addParamListJSON.choices = values.Where(v => !current.TargetFloatParams.Any(t => t.storable == storable && t.floatParam.name == v)).OrderBy(v => v).ToList();
        }

        private void GenerateRemoveToggles()
        {
            ClearRemoveToggles();

            foreach (var target in current.TargetControllers)
            {
                UIDynamicToggle jsbUI = null;
                JSONStorableBool jsb = null;
                jsb = new JSONStorableBool($"Remove {target.name}", true, (bool val) =>
                {
                    _addControllerListJSON.val = target.name;
                    RemoveAnimatedController(target);
                    plugin.RemoveToggle(jsb);
                    plugin.RemoveToggle(jsbUI);
                    _removeToggles.Remove(jsb);
                });
                jsbUI = plugin.CreateToggle(jsb, true);
                jsbUI.backgroundColor = Color.red;
                jsbUI.textColor = Color.white;
                _removeToggles.Add(jsb);
            }
            foreach (var target in current.TargetFloatParams)
            {
                UIDynamicToggle jsbUI = null;
                JSONStorableBool jsb = null;
                jsb = new JSONStorableBool($"Remove {target.name}", true, (bool val) =>
                {
                    _addStorableListJSON.val = target.storable.name;
                    _addParamListJSON.val = target.floatParam.name;
                    RemoveFloatParam(target);
                    plugin.RemoveToggle(jsb);
                    plugin.RemoveToggle(jsbUI);
                    _removeToggles.Remove(jsb);
                });
                jsbUI = plugin.CreateToggle(jsb, true);
                jsbUI.backgroundColor = Color.red;
                jsbUI.textColor = Color.white;
                _removeToggles.Add(jsb);
            }
            // Ensures shows on top
            _addControllerListJSON.popup.Toggle();
            _addControllerListJSON.popup.Toggle();
            _addStorableListJSON.popup.Toggle();
            _addStorableListJSON.popup.Toggle();
            _addParamListJSON.popup.Toggle();
            _addParamListJSON.popup.Toggle();
        }

        private void ClearRemoveToggles()
        {
            if (_removeToggles == null) return;
            foreach (var toggleJSON in _removeToggles)
            {
                plugin.RemoveToggle(toggleJSON);
            }
        }

        #endregion

        #region Callbacks

        private class FloatParamRef
        {
            public JSONStorable Storable { get; set; }
            public JSONStorableFloat FloatParam { get; set; }
        }

        private void EnableAllTargets()
        {
            try
            {
                var allControllers = plugin.animation.Clips.SelectMany(c => c.TargetControllers).Select(t => t.controller).Distinct().ToList();
                var h = new HashSet<JSONStorableFloat>();
                var allFloatParams = plugin.animation.Clips.SelectMany(c => c.TargetFloatParams).Where(t => h.Add(t.floatParam)).Select(t => new FloatParamRef { Storable = t.storable, FloatParam = t.floatParam }).ToList();

                foreach (var clip in plugin.animation.Clips)
                {
                    foreach (var controller in allControllers)
                    {
                        if (!clip.TargetControllers.Any(t => t.controller == controller))
                        {
                            var target = clip.Add(controller);
                            if (target != null)
                            {
                                target.SetKeyframeToCurrentTransform(0f);
                                target.SetKeyframeToCurrentTransform(clip.animationLength);
                            }
                        }
                    }
                    clip.TargetControllers.Sort(new FreeControllerAnimationTarget.Comparer());

                    foreach (var floatParamRef in allFloatParams)
                    {
                        if (!clip.TargetFloatParams.Any(t => t.floatParam == floatParamRef.FloatParam))
                        {
                            var target = clip.Add(floatParamRef.Storable, floatParamRef.FloatParam);
                            if (target != null)
                            {
                                target.SetKeyframe(0f, floatParamRef.FloatParam.val);
                                target.SetKeyframe(clip.animationLength, floatParamRef.FloatParam.val);
                            }
                        }
                    }
                    clip.TargetFloatParams.Sort(new FloatParamAnimationTarget.Comparer());
                }
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(TargetsScreen)}.{nameof(EnableAllTargets)}: {exc}");
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

                if (current.TargetControllers.Any(c => c.controller == controller))
                    return;

                controller.currentPositionState = FreeControllerV3.PositionState.On;
                controller.currentRotationState = FreeControllerV3.RotationState.On;

                foreach (var clip in plugin.animation.Clips)
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

                if (current.TargetFloatParams.Any(c => c.floatParam == sourceFloatParam))
                    return;

                foreach (var clip in plugin.animation.Clips)
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
                foreach (var clip in plugin.animation.Clips)
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
                foreach (var clip in plugin.animation.Clips)
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
            args.Before.onTargetsListChanged.RemoveListener(OnTargetsListChanged);
            args.After.onTargetsListChanged.AddListener(OnTargetsListChanged);

            base.OnCurrentAnimationChanged(args);

            GenerateRemoveToggles();
        }

        public override void Dispose()
        {
            if (_addParamListUI != null) _addParamListUI.popup.onOpenPopupHandlers -= RefreshStorableFloatsList;
            current.onTargetsListChanged.RemoveListener(OnTargetsListChanged);
            ClearRemoveToggles();
            base.Dispose();
        }

        #endregion
    }
}

