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
    public class AtomAnimationEditUI : AtomAnimationBaseUI
    {
        public const string ScreenName = "Edit";
        public override string Name => ScreenName;

        private abstract class TargetRef
        {
            public JSONStorableBool KeyframeJSON;
            public IAnimationTargetWithCurves Target;
            public UIDynamicToggle KeyframeUI;

            public virtual void UpdateValue(float time)
            {
                KeyframeJSON.valNoCallback = Target.GetLeadCurve().KeyframeBinarySearch(time) != -1;
            }

            public virtual void Remove(IAtomPlugin plugin)
            {
                plugin.RemoveToggle(KeyframeJSON);
                plugin.RemoveToggle(KeyframeUI);
            }
        }

        private class ControllerTargetRef : TargetRef
        {
        }

        private class FloatParamTargetRef : TargetRef
        {
            public JSONStorableFloat FloatParamProxyJSON;
            public UIDynamicSlider SliderUI;

            public override void UpdateValue(float time)
            {
                base.UpdateValue(time);
                FloatParamProxyJSON.valNoCallback = ((FloatParamAnimationTarget)Target).FloatParam.val;
            }

            public override void Remove(IAtomPlugin plugin)
            {
                base.Remove(plugin);
                plugin.RemoveSlider(FloatParamProxyJSON);
                plugin.RemoveSlider(SliderUI);
            }
        }

        private readonly List<TargetRef> _targets = new List<TargetRef>();
        private JSONStorableStringChooser _curveTypeJSON;
        private Curves _curves;
        private UIDynamicPopup _curveTypeUI;

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
            UpdateValues();
            _curves?.SetScrubberPosition(Plugin.Animation.Time);
        }

        public override void AnimationFrameUpdated()
        {
            base.AnimationFrameUpdated();
            UpdateValues();
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

        private void UpdateValues()
        {
            var time = Plugin.Animation.Time;
            foreach (var targetRef in _targets)
            {
                targetRef.UpdateValue(time);
            }
        }

        private void RefreshTargetsList()
        {
            if (Plugin.Animation == null) return;
            RemoveTargets();
            var time = Plugin.Animation.Time;

            foreach (var target in Current.GetAllOrSelectedTargets().OfType<FreeControllerAnimationTarget>())
            {
                var keyframeJSON = new JSONStorableBool($"{target.Name} Keyframe", target.GetLeadCurve().KeyframeBinarySearch(time) != -1, (bool val) => ToggleKeyframe(target, val));
                var keyframeUI = Plugin.CreateToggle(keyframeJSON, true);
                _targets.Add(new ControllerTargetRef
                {
                    Target = target,
                    KeyframeJSON = keyframeJSON,
                    KeyframeUI = keyframeUI
                });
            }

            foreach (var target in Current.GetAllOrSelectedTargets().OfType<FloatParamAnimationTarget>())
            {
                var sourceFloatParamJSON = target.FloatParam;
                var keyframeJSON = new JSONStorableBool($"{target.Storable.name}/{sourceFloatParamJSON.name} Keyframe", target.Value.KeyframeBinarySearch(time) != -1, (bool val) => ToggleKeyframe(target, val))
                {
                    isStorable = false
                };
                var keyframeUI = Plugin.CreateToggle(keyframeJSON, true);
                var jsfJSONProxy = new JSONStorableFloat($"{target.Storable.name}/{sourceFloatParamJSON.name}", sourceFloatParamJSON.defaultVal, (float val) => SetFloatParamValue(target, val), sourceFloatParamJSON.min, sourceFloatParamJSON.max, sourceFloatParamJSON.constrained, true)
                {
                    isStorable = false,
                    valNoCallback = sourceFloatParamJSON.val
                };
                var sliderUI = Plugin.CreateSlider(jsfJSONProxy, true);
                _targets.Add(new FloatParamTargetRef
                {
                    Target = target,
                    FloatParamProxyJSON = jsfJSONProxy,
                    SliderUI = sliderUI,
                    KeyframeJSON = keyframeJSON,
                    KeyframeUI = keyframeUI
                });
            }
        }

        public override void Dispose()
        {
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

        private void ToggleKeyframe(FreeControllerAnimationTarget target, bool enable)
        {
            if (Plugin.Animation.IsPlaying()) return;
            var time = Plugin.Animation.Time.Snap();
            if (time.IsSameFrame(0f) || time.IsSameFrame(Current.AnimationLength))
            {
                _targets.First(t => t.Target == target).KeyframeJSON.valNoCallback = true;
                return;
            }
            if (enable)
            {
                if (Plugin.AutoKeyframeAllControllersJSON.val)
                {
                    foreach (var target1 in Current.TargetControllers)
                        SetControllerKeyframe(time, target1);
                }
                else
                {
                    SetControllerKeyframe(time, target);
                }
            }
            else
            {
                if (Plugin.AutoKeyframeAllControllersJSON.val)
                {
                    foreach (var target1 in Current.TargetControllers)
                        target1.DeleteFrame(time);
                }
                else
                {
                    target.DeleteFrame(time);
                }
            }
            Plugin.Animation.RebuildAnimation();
            Plugin.AnimationModified();
        }

        private void ToggleKeyframe(FloatParamAnimationTarget target, bool val)
        {
            if (Plugin.Animation.IsPlaying()) return;
            var time = Plugin.Animation.Time.Snap();
            if (time.IsSameFrame(0f) || time.IsSameFrame(Current.AnimationLength))
            {
                _targets.First(t => t.Target == target).KeyframeJSON.valNoCallback = true;
                return;
            }
            if (val)
            {
                Plugin.Animation.SetKeyframe(target, time, target.FloatParam.val);
            }
            else
            {
                target.DeleteFrame(time);
            }
            Plugin.Animation.RebuildAnimation();
            Plugin.AnimationModified();
        }

        private void SetControllerKeyframe(float time, FreeControllerAnimationTarget target)
        {
            Plugin.Animation.SetKeyframeToCurrentTransform(target, time);
            if (target.Settings[time.ToMilliseconds()]?.CurveType == CurveTypeValues.CopyPrevious)
                Current.ChangeCurve(time, CurveTypeValues.Smooth);
        }

        private void SetFloatParamValue(FloatParamAnimationTarget target, float val)
        {
            if (Plugin.Animation.IsPlaying()) return;
            target.FloatParam.val = val;
            var time = Plugin.Animation.Time;
            Plugin.Animation.SetKeyframe(target, time, val);
            // NOTE: We don't call AnimationModified for performance reasons. This could be improved by using more specific events.
            Plugin.Animation.RebuildAnimation();
            if (target.Selected) RefreshCurves();
            var targetRef = _targets.FirstOrDefault(j => j.Target == target);
            targetRef.KeyframeJSON.valNoCallback = true;
        }
    }
}

