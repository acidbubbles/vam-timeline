using System;
using System.Collections.Generic;
using System.Linq;

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
        public override string Name => ScreenName;

        private JSONStorableStringChooser _addControllerListJSON;
        private UIDynamicPopup _addControllerUI;
        private JSONStorableStringChooser _addStorableListJSON;
        private JSONStorableStringChooser _addParamListJSON;
        private UIDynamicPopup _addFloatParamListUI;
        private UIDynamicButton _toggleControllerUI;
        private UIDynamicButton _toggleFloatParamUI;
        private UIDynamicPopup _addParamListUI;
        private readonly List<JSONStorableBool> _removeToggles = new List<JSONStorableBool>();

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

            InitControllersUI();

            CreateSpacer(true);

            InitFloatParamsUI();

            CreateSpacer(true);

            GenerateRemoveToggles();
        }

        private void InitControllersUI()
        {
            _addControllerListJSON = new JSONStorableStringChooser("Animate Controller", GetEligibleFreeControllers().ToList(), GetEligibleFreeControllers().FirstOrDefault(), "Animate controller", (string name) => GenerateRemoveToggles())
            {
                isStorable = false
            };
            RegisterStorable(_addControllerListJSON);
            _addControllerUI = Plugin.CreateScrollablePopup(_addControllerListJSON, true);
            _addControllerUI.popupPanelHeight = 900f;
            RegisterComponent(_addControllerUI);

            _toggleControllerUI = Plugin.CreateButton("Add Controller", true);
            _toggleControllerUI.button.onClick.AddListener(() => AddAnimatedController());
            RegisterComponent(_toggleControllerUI);
        }

        private IEnumerable<string> GetEligibleFreeControllers()
        {
            yield return "";
            foreach (var fc in Plugin.ContainingAtom.freeControllers)
            {
                if (fc.name == "control") yield return fc.name;
                if (!fc.name.EndsWith("Control")) continue;
                yield return fc.name;
            }
        }

        private void InitFloatParamsUI()
        {
            var storables = GetStorablesWithFloatParams().ToList();
            _addStorableListJSON = new JSONStorableStringChooser("Animate Storable", storables, storables.Contains("geometry") ? "geometry" : storables.FirstOrDefault(), "Animate Storable", (string name) => RefreshStorableFloatsList(true))
            {
                isStorable = false
            };
            RegisterStorable(_addStorableListJSON);
            _addFloatParamListUI = Plugin.CreateScrollablePopup(_addStorableListJSON, true);
            _addFloatParamListUI.popupPanelHeight = 700f;
            _addFloatParamListUI.popup.onOpenPopupHandlers += () => _addStorableListJSON.choices = GetStorablesWithFloatParams().ToList();
            RegisterComponent(_addFloatParamListUI);

            _addParamListJSON = new JSONStorableStringChooser("Animate Param", new List<string> { "" }, "", "Animate Param", (string name) => GenerateRemoveToggles())
            {
                isStorable = false
            };
            RegisterStorable(_addParamListJSON);
            _addParamListUI = Plugin.CreateScrollablePopup(_addParamListJSON, true);
            _addParamListUI.popupPanelHeight = 600f;
            _addParamListUI.popup.onOpenPopupHandlers += () => RefreshStorableFloatsList(false);
            RegisterComponent(_addParamListUI);

            _toggleFloatParamUI = Plugin.CreateButton("Add Param", true);
            _toggleFloatParamUI.button.onClick.AddListener(() => AddAnimatedFloatParam());
            RegisterComponent(_toggleFloatParamUI);
        }

        private IEnumerable<string> GetStorablesWithFloatParams()
        {
            yield return "";
            foreach (var storableId in Plugin.ContainingAtom.GetStorableIDs().OrderBy(s => s))
            {
                if (storableId.StartsWith("hairTool")) continue;
                var storable = Plugin.ContainingAtom.GetStorableByID(storableId);
                if (storable == null) continue;
                if (Object.ReferenceEquals(storable, Plugin)) continue;
                if ((storable.GetFloatParamNames()?.Count ?? 0) > 0)
                    yield return storableId;
            }
        }

        private void RefreshStorableFloatsList(bool autoSelect)
        {
            if (string.IsNullOrEmpty(_addStorableListJSON.val))
            {
                _addParamListJSON.choices = new List<string>();
                if (autoSelect)
                    _addParamListJSON.valNoCallback = "";
                return;
            }
            var values = Plugin.ContainingAtom.GetStorableByID(_addStorableListJSON.val)?.GetFloatParamNames() ?? new List<string>();
            _addParamListJSON.choices = values.OrderBy(v => v).ToList();
            if (autoSelect && !values.Contains(_addParamListJSON.val))
                _addParamListJSON.valNoCallback = values.FirstOrDefault();
        }

        private void GenerateRemoveToggles()
        {
            if (string.Join(",", Current.AllTargets.Select(tc => tc.Name).OrderBy(n => n).ToArray()) == string.Join(",", _removeToggles.Select(ct => ct.name).OrderBy(n => n).ToArray()))
                return;

            ClearRemoveToggles();
            foreach (var target in Current.TargetControllers)
            {
                var jsb = new JSONStorableBool(target.Name, true, (bool val) =>
                {
                    _addControllerListJSON.val = target.Name;
                    RemoveAnimatedController(target);
                });
                var jsbUI = Plugin.CreateToggle(jsb, true);
                _removeToggles.Add(jsb);
            }
            foreach (var target in Current.TargetFloatParams)
            {
                var jsb = new JSONStorableBool(target.Name, true, (bool val) =>
                {
                    _addStorableListJSON.val = target.Storable.name;
                    _addParamListJSON.val = target.FloatParam.name;
                    RemoveFloatParam(target);
                });
                var jsbUI = Plugin.CreateToggle(jsb, true);
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
                Plugin.RemoveToggle(toggleJSON);
            }
        }

        #endregion

        #region Callbacks

        private void AddAnimatedController()
        {
            try
            {
                var uid = _addControllerListJSON.val;
                if (string.IsNullOrEmpty(uid)) return;
                var controller = Plugin.ContainingAtom.freeControllers.Where(x => x.name == uid).FirstOrDefault();
                if (controller == null)
                {
                    SuperController.LogError($"VamTimeline: Controller {uid} in atom {Plugin.ContainingAtom.uid} does not exist");
                    return;
                }
                if (Current.TargetControllers.Any(c => c.Controller == controller))
                    return;

                controller.currentPositionState = FreeControllerV3.PositionState.On;
                controller.currentRotationState = FreeControllerV3.RotationState.On;
                Plugin.Animation.Add(controller);
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

                var storable = Plugin.ContainingAtom.GetStorableByID(_addStorableListJSON.val);
                if (storable == null)
                {
                    SuperController.LogError($"VamTimeline: Storable {_addStorableListJSON.val} in atom {Plugin.ContainingAtom.uid} does not exist");
                    return;
                }
                var sourceFloatParam = storable.GetFloatJSONParam(_addParamListJSON.val);
                if (sourceFloatParam == null)
                {
                    SuperController.LogError($"VamTimeline: Param {_addParamListJSON.val} in atom {Plugin.ContainingAtom.uid} does not exist");
                    return;
                }
                if (Current.TargetFloatParams.Any(c => c.FloatParam == sourceFloatParam))
                {
                    return;
                }

                Plugin.Animation.Add(storable, sourceFloatParam);
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
                Current.Remove(target.Controller);
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
                Current.Remove(target);
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(TargetsScreen)}.{nameof(RemoveAnimatedController)}: " + exc);
            }
        }

        #endregion

        #region Events

        protected override void OnCurrentAnimationChanged(AtomAnimation.CurrentAnimationChangedEventArgs args)
        {
            base.OnCurrentAnimationChanged(args);

            GenerateRemoveToggles();
        }

        public override void Dispose()
        {
            ClearRemoveToggles();

            base.Dispose();
        }

        #endregion
    }
}

