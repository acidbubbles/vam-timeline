using System.Collections;
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
    public class EditScreen : ScreenBase
    {
        public const string ScreenName = "Edit";
        public override string Name => ScreenName;

        private class TargetRef
        {
            public ITargetFrame Component;
            public IAnimationTargetWithCurves Target;

            public void Remove(IAtomPlugin plugin)
            {
                plugin.RemoveSpacer(Component.Container);
            }
        }

        private readonly List<TargetRef> _targets = new List<TargetRef>();
        private JSONStorableStringChooser _curveTypeJSON;
        private Curves _curves;
        private UIDynamicPopup _curveTypeUI;
        private bool _selectionChangedPending;
        private UIDynamicButton _manageTargetsUI;

        public EditScreen(IAtomPlugin plugin)
            : base(plugin)
        {

        }
        public override void Init()
        {
            base.Init();

            // Left side

            InitCurvesUI(false);

            InitChangeCurveTypeUI(false);

            InitClipboardUI(false);

            InitAutoKeyframeUI();

            // Right side

            Current.TargetsSelectionChanged.AddListener(SelectionChanged);

            SelectionChanged();
        }

        private void InitChangeCurveTypeUI(bool rightSide)
        {
            _curveTypeJSON = new JSONStorableStringChooser(StorableNames.ChangeCurve, CurveTypeValues.DisplayCurveTypes, "", "Change Curve", ChangeCurve);
            RegisterStorable(_curveTypeJSON);
            _curveTypeUI = Plugin.CreateScrollablePopup(_curveTypeJSON, rightSide);
            _curveTypeUI.popupPanelHeight = 340f;
            RegisterComponent(_curveTypeUI);
        }

        private void InitAutoKeyframeUI()
        {
            RegisterStorable(Plugin.AutoKeyframeAllControllersJSON);
            var autoKeyframeAllControllersUI = Plugin.CreateToggle(Plugin.AutoKeyframeAllControllersJSON, false);
            RegisterComponent(autoKeyframeAllControllersUI);
        }

        private void InitCurvesUI(bool rightSide)
        {
            var spacerUI = Plugin.CreateSpacer(rightSide);
            spacerUI.height = 200f;
            RegisterComponent(spacerUI);

            _curves = spacerUI.gameObject.AddComponent<Curves>();
        }

        private void RefreshCurves()
        {
            // TODO: Listen internally
            if (_curves == null) return;
            var targets = Current.GetAllOrSelectedTargets().ToList();
            if (targets.Count == 1)
                _curves.Bind(Plugin.Animation, targets[0]);
            else
                _curves.Bind(Plugin.Animation, null);
        }

        protected override void OnCurrentAnimationChanged(AtomAnimation.CurrentAnimationChangedEventArgs args)
        {
            base.OnCurrentAnimationChanged(args);
            args.Before.TargetsSelectionChanged.RemoveListener(SelectionChanged);
            args.After.TargetsSelectionChanged.AddListener(SelectionChanged);
            RefreshTargetsList();
        }

        private void SelectionChanged()
        {
            if (_selectionChangedPending) return;
            _selectionChangedPending = true;
            Plugin.StartCoroutine(SelectionChangedDeferred());
        }

        private IEnumerator SelectionChangedDeferred()
        {
            yield return new WaitForEndOfFrame();
            _selectionChangedPending = false;
            if (_disposing) yield break;
            RefreshCurves();
            RefreshTargetsList();
            _curveTypeUI.popup.topButton.interactable = Current.GetAllOrSelectedTargets().OfType<FreeControllerAnimationTarget>().Count() > 0;
        }

        private void UpdateCurrentCurveType()
        {
            if (_curveTypeJSON == null) return;

            var time = Plugin.Animation.Time.Snap();
            if (Current.Loop && (time.IsSameFrame(0) || time.IsSameFrame(Current.AnimationLength)))
            {
                _curveTypeJSON.valNoCallback = "(Loop)";
                return;
            }
            var ms = time.ToMilliseconds();
            var curveTypes = new HashSet<string>();
            foreach (var target in Current.GetAllOrSelectedTargets().OfType<FreeControllerAnimationTarget>())
            {
                KeyframeSettings v;
                if (!target.Settings.TryGetValue(ms, out v)) continue;
                curveTypes.Add(v.CurveType);
            }
            if (curveTypes.Count == 0)
                _curveTypeJSON.valNoCallback = "(No Keyframe)";
            else if (curveTypes.Count == 1)
                _curveTypeJSON.valNoCallback = curveTypes.First().ToString();
            else
                _curveTypeJSON.valNoCallback = "(" + string.Join("/", curveTypes.ToArray()) + ")";
        }

        private void RefreshTargetsList()
        {
            if (Plugin.Animation == null) return;
            RemoveTargets();
            Plugin.RemoveButton(_manageTargetsUI);
            var time = Plugin.Animation.Time;

            foreach (var target in Current.GetAllOrSelectedTargets().OfType<FreeControllerAnimationTarget>())
            {
                var keyframeUI = Plugin.CreateSpacer(true);
                keyframeUI.height = 60f;
                var component = keyframeUI.gameObject.AddComponent<ControllerTargetFrame>();
                component.Bind(Plugin, Plugin.Animation.Current, target);
                _targets.Add(new TargetRef
                {
                    Component = component,
                    Target = target
                });
            }

            foreach (var target in Current.GetAllOrSelectedTargets().OfType<FloatParamAnimationTarget>())
            {
                var keyframeUI = Plugin.CreateSpacer(true);
                keyframeUI.height = 60f;
                var component = keyframeUI.gameObject.AddComponent<FloatParamTargetFrame>();
                component.Bind(Plugin, Plugin.Animation.Current, target);
                _targets.Add(new TargetRef
                {
                    Component = component,
                    Target = target,
                });
            }

            _manageTargetsUI = Plugin.CreateButton("<b>[+/-]</b> Add/Remove Targets", true);
            if (Current.AllTargetsCount == 0)
                _manageTargetsUI.buttonColor = new Color(0f, 1f, 0f);
            else
                _manageTargetsUI.buttonColor = new Color(0.8f, 0.7f, 0.8f);
            _manageTargetsUI.button.onClick.AddListener(() => OnScreenChangeRequested.Invoke(TargetsScreen.ScreenName));
        }

        public override void Dispose()
        {
            Current.TargetsSelectionChanged.RemoveListener(SelectionChanged);
            Plugin.RemoveButton(_manageTargetsUI);
            RemoveTargets();
            base.Dispose();
        }

        private void RemoveTargets()
        {
            foreach (var targetRef in _targets)
            {
                targetRef.Remove(Plugin);
            }
            _targets.Clear();
        }

        private void ChangeCurve(string curveType)
        {
            if (string.IsNullOrEmpty(curveType) || curveType.StartsWith("("))
            {
                UpdateCurrentCurveType();
                return;
            }
            float time = Plugin.Animation.Time.Snap();
            if (time.IsSameFrame(0) && curveType == CurveTypeValues.CopyPrevious)
            {
                UpdateCurrentCurveType();
                return;
            }
            if (Plugin.Animation.IsPlaying()) return;
            if (Current.Loop && (time.IsSameFrame(0) || time.IsSameFrame(Current.AnimationLength)))
            {
                UpdateCurrentCurveType();
                return;
            }
            Current.ChangeCurve(time, curveType);
        }
    }
}

