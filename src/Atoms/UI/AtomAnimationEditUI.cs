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
    public class AtomAnimationEditUI : AtomAnimationBaseUI
    {
        public const string ScreenName = "Edit";
        public override string Name => ScreenName;

        private class TargetRef
        {
            public ITargetFrame Component;
            public IAnimationTargetWithCurves Target;

            public void UpdateValue(float time, bool stopped)
            {
                Component.SetTime(time, stopped);
            }

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

        public AtomAnimationEditUI(IAtomPlugin plugin)
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

            Current.SelectedChanged.AddListener(SelectionChanged);

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
            if (_curves == null) return;
            var targets = Current.GetAllOrSelectedTargets().ToList();
            if (targets.Count == 1)
                _curves.Bind(targets[0]);
            else
                _curves.Bind(null);
            _curves?.SetScrubberPosition(Plugin.Animation.Time);
        }

        public override void UpdatePlaying()
        {
            base.UpdatePlaying();
            UpdateValues(false);
            _curves?.SetScrubberPosition(Plugin.Animation.Time);
        }

        public override void AnimationFrameUpdated()
        {
            base.AnimationFrameUpdated();
            UpdateValues(true);
            UpdateCurrentCurveType();
            _curves?.SetScrubberPosition(Plugin.Animation.Time);
        }

        public override void AnimationModified()
        {
            base.AnimationModified();
            UpdateCurrentCurveType();
            RefreshCurves();
        }

        protected override void AnimationChanged(AtomAnimationClip before, AtomAnimationClip after)
        {
            base.AnimationChanged(before, after);
            before.SelectedChanged.RemoveListener(SelectionChanged);
            after.SelectedChanged.AddListener(SelectionChanged);
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

        private void UpdateValues(bool stopped)
        {
            var time = Plugin.Animation.Time;
            foreach (var targetRef in _targets)
            {
                targetRef.UpdateValue(time, stopped);
            }
        }

        private void RefreshTargetsList()
        {
            if (Plugin.Animation == null) return;
            RemoveTargets();
            var time = Plugin.Animation.Time;

            foreach (var target in Current.GetAllOrSelectedTargets().OfType<FreeControllerAnimationTarget>())
            {
                var keyframeUI = Plugin.CreateSpacer(true);
                keyframeUI.height = 60f;
                var component = keyframeUI.gameObject.AddComponent<ControllerTargetFrame>();
                component.Bind(Plugin, Plugin.Animation.Current, target);
                component.SetTime(time, true);
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
                component.SetTime(time, true);
                _targets.Add(new TargetRef
                {
                    Component = component,
                    Target = target,
                });
            }
        }

        public override void Dispose()
        {
            Current.SelectedChanged.RemoveListener(SelectionChanged);
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
            Plugin.Animation.RebuildAnimation();
            Plugin.AnimationModified();
        }
    }
}

