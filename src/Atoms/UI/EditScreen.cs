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

        public override string name => ScreenName;

        private const string _noKeyframeCurveType = "(No Keyframe)";
        private const string _loopCurveType = "(Loop)";

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

            current.onTargetsSelectionChanged.AddListener(OnSelectionChanged);
            plugin.animation.onTimeChanged.AddListener(OnTimeChanged);

            OnSelectionChanged();

            if (plugin.animation.IsEmpty()) InitExplanation();
        }

        private void InitChangeCurveTypeUI(bool rightSide)
        {
            _curveTypeJSON = new JSONStorableStringChooser(StorableNames.ChangeCurve, CurveTypeValues.DisplayCurveTypes, "", "Change Curve", ChangeCurve);
            RegisterStorable(_curveTypeJSON);
            _curveTypeUI = plugin.CreateScrollablePopup(_curveTypeJSON, rightSide);
            _curveTypeUI.popupPanelHeight = 450f;
            RegisterComponent(_curveTypeUI);
        }

        private void InitAutoKeyframeUI()
        {
            RegisterStorable(plugin.autoKeyframeAllControllersJSON);
            var autoKeyframeAllControllersUI = plugin.CreateToggle(plugin.autoKeyframeAllControllersJSON, false);
            RegisterComponent(autoKeyframeAllControllersUI);
        }

        private void InitCurvesUI(bool rightSide)
        {
            var spacerUI = plugin.CreateSpacer(rightSide);
            spacerUI.height = 300f;
            RegisterComponent(spacerUI);

            _curves = spacerUI.gameObject.AddComponent<Curves>();
        }

        private void InitExplanation()
        {
            var textJSON = new JSONStorableString("Help", HelpScreen.HelpText);
            RegisterStorable(textJSON);
            var textUI = plugin.CreateTextField(textJSON, true);
            textUI.height = 900;
            RegisterComponent(textUI);
        }

        private void RefreshCurves()
        {
            if (_curves == null) return;
            _curves.Bind(plugin.animation, current.allTargetsCount == 1 ? current.allTargets.ToList() : current.GetSelectedTargets().ToList());
        }

        protected override void OnCurrentAnimationChanged(AtomAnimation.CurrentAnimationChangedEventArgs args)
        {
            base.OnCurrentAnimationChanged(args);
            args.Before.onTargetsSelectionChanged.RemoveListener(OnSelectionChanged);
            args.After.onTargetsSelectionChanged.AddListener(OnSelectionChanged);
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
            plugin.StartCoroutine(SelectionChangedDeferred());
        }

        private IEnumerator SelectionChangedDeferred()
        {
            yield return new WaitForEndOfFrame();
            _selectionChangedPending = false;
            if (_disposing) yield break;
            RefreshCurrentCurveType();
            RefreshCurves();
            RefreshTargetsList();
            _curveTypeUI.popup.topButton.interactable = current.GetAllOrSelectedTargets().OfType<FreeControllerAnimationTarget>().Count() > 0;
        }

        private void RefreshCurrentCurveType()
        {
            if (_curveTypeJSON == null) return;

            var time = plugin.animation.time.Snap();
            if (current.loop && (time.IsSameFrame(0) || time.IsSameFrame(current.animationLength)))
            {
                _curveTypeJSON.valNoCallback = _loopCurveType;
                return;
            }
            var ms = time.ToMilliseconds();
            var curveTypes = new HashSet<string>();
            foreach (var target in current.GetAllOrSelectedTargets().OfType<FreeControllerAnimationTarget>())
            {
                KeyframeSettings v;
                if (!target.settings.TryGetValue(ms, out v)) continue;
                curveTypes.Add(v.curveType);
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
            if (plugin.animation == null) return;
            RemoveTargets();
            plugin.RemoveButton(_manageTargetsUI);
            var time = plugin.animation.time;

            foreach (var target in current.GetAllOrSelectedTargets().OfType<FreeControllerAnimationTarget>())
            {
                var keyframeUI = plugin.CreateSpacer(true);
                keyframeUI.height = 60f;
                var component = keyframeUI.gameObject.AddComponent<ControllerTargetFrame>();
                component.Bind(plugin, plugin.animation.current, target);
                _targets.Add(new TargetRef
                {
                    Component = component,
                    Target = target
                });
            }

            foreach (var target in current.GetAllOrSelectedTargets().OfType<FloatParamAnimationTarget>())
            {
                var keyframeUI = plugin.CreateSpacer(true);
                keyframeUI.height = 60f;
                var component = keyframeUI.gameObject.AddComponent<FloatParamTargetFrame>();
                component.Bind(plugin, plugin.animation.current, target);
                _targets.Add(new TargetRef
                {
                    Component = component,
                    Target = target,
                });
            }
            _manageTargetsUI = CreateChangeScreenButton("<b>[+/-]</b> Add/Remove Targets", TargetsScreen.ScreenName, true);
            if (current.allTargetsCount == 0)
                _manageTargetsUI.buttonColor = new Color(0f, 1f, 0f);
            else
                _manageTargetsUI.buttonColor = new Color(0.8f, 0.7f, 0.8f);
        }

        public override void Dispose()
        {
            current.onTargetsSelectionChanged.RemoveListener(OnSelectionChanged);
            plugin.animation.onTimeChanged.RemoveListener(OnTimeChanged);
            plugin.RemoveButton(_manageTargetsUI);
            RemoveTargets();
            base.Dispose();
        }

        private void RemoveTargets()
        {
            foreach (var targetRef in _targets)
            {
                targetRef.Remove(plugin);
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
            float time = plugin.animation.time.Snap();
            if (time.IsSameFrame(0) && curveType == CurveTypeValues.CopyPrevious)
            {
                RefreshCurrentCurveType();
                return;
            }
            if (plugin.animation.IsPlaying()) return;
            if (current.loop && (time.IsSameFrame(0) || time.IsSameFrame(current.animationLength)))
            {
                RefreshCurrentCurveType();
                return;
            }
            current.ChangeCurve(time, curveType);
            RefreshCurrentCurveType();
        }
    }
}

