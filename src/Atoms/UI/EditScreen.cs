using System;
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
        private const string _noKeyframeCurveType = "(No Keyframe)";
        private const string _loopCurveType = "(Loop)";

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

            InitChangeCurveTypeUI(false);

            InitCurvesUI(false);

            InitClipboardUI(false);

            InitAutoKeyframeUI();

            // Right side

            Current.TargetsSelectionChanged.AddListener(OnSelectionChanged);
            Plugin.Animation.TimeChanged.AddListener(OnTimeChanged);

            OnSelectionChanged();

            if (Plugin.Animation.IsEmpty()) InitExplanation();
        }

        private void InitChangeCurveTypeUI(bool rightSide)
        {
            _curveTypeJSON = new JSONStorableStringChooser(StorableNames.ChangeCurve, CurveTypeValues.DisplayCurveTypes, "", "Change Curve", ChangeCurve);
            RegisterStorable(_curveTypeJSON);
            _curveTypeUI = Plugin.CreateScrollablePopup(_curveTypeJSON, rightSide);
            _curveTypeUI.popupPanelHeight = 500f;
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

        private void InitExplanation()
        {
            var textJSON = new JSONStorableString("Help", HelpScreen.HelpText);
            RegisterStorable(textJSON);
            var textUI = Plugin.CreateTextField(textJSON, true);
            textUI.height = 900;
            RegisterComponent(textUI);
        }

        private void RefreshCurves()
        {
            if (_curves == null) return;
            _curves.Bind(Plugin.Animation, Current.GetSelectedTargets().ToList());
        }

        protected override void OnCurrentAnimationChanged(AtomAnimation.CurrentAnimationChangedEventArgs args)
        {
            base.OnCurrentAnimationChanged(args);
            args.Before.TargetsSelectionChanged.RemoveListener(OnSelectionChanged);
            args.After.TargetsSelectionChanged.AddListener(OnSelectionChanged);
            RefreshTargetsList();
        }

        private void OnTimeChanged(float arg0)
        {
            RefreshCurrentCurveType();
        }

        private void OnSelectionChanged()
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
            RefreshCurrentCurveType();
            RefreshCurves();
            RefreshTargetsList();
            _curveTypeUI.popup.topButton.interactable = Current.GetAllOrSelectedTargets().OfType<FreeControllerAnimationTarget>().Count() > 0;
        }

        private void RefreshCurrentCurveType()
        {
            if (_curveTypeJSON == null) return;

            var time = Plugin.Animation.Time.Snap();
            if (Current.Loop && (time.IsSameFrame(0) || time.IsSameFrame(Current.AnimationLength)))
            {
                _curveTypeJSON.valNoCallback = _loopCurveType;
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
                _curveTypeJSON.valNoCallback = _noKeyframeCurveType;
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
            _manageTargetsUI = CreateChangeScreenButton("<b>[+/-]</b> Add/Remove Targets", TargetsScreen.ScreenName, true, false);
            if (Current.AllTargetsCount == 0)
                _manageTargetsUI.buttonColor = new Color(0f, 1f, 0f);
            else
                _manageTargetsUI.buttonColor = new Color(0.8f, 0.7f, 0.8f);
        }

        public override void Dispose()
        {
            Current.TargetsSelectionChanged.RemoveListener(OnSelectionChanged);
            Plugin.Animation.TimeChanged.RemoveListener(OnTimeChanged);
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
                RefreshCurrentCurveType();
                return;
            }
            float time = Plugin.Animation.Time.Snap();
            if (time.IsSameFrame(0) && curveType == CurveTypeValues.CopyPrevious)
            {
                RefreshCurrentCurveType();
                return;
            }
            if (Plugin.Animation.IsPlaying()) return;
            if (Current.Loop && (time.IsSameFrame(0) || time.IsSameFrame(Current.AnimationLength)))
            {
                RefreshCurrentCurveType();
                return;
            }
            Current.ChangeCurve(time, curveType);
            RefreshCurrentCurveType();
        }
    }
}

