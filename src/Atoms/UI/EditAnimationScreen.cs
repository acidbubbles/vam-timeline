using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class EditAnimationScreen : ScreenBase
    {
        public const string ScreenName = "Edit Animation";
        public override string Name => ScreenName;

        public const string ChangeLengthModeCropExtendEnd = "Crop/Extend End";
        public const string ChangeLengthModeAddKeyframeEnd = "Add Keyframe End";
        public const string ChangeLengthModeCropExtendBegin = "Crop/Extend Begin";
        public const string ChangeLengthModeAddKeyframeBegin = "Add Keyframe Begin";
        public const string ChangeLengthModeCropExtendAtTime = "Crop/Extend At Time";
        public const string ChangeLengthModeStretch = "Stretch";
        public const string ChangeLengthModeLoop = "Loop (Extend)";

        private JSONStorableString _animationNameJSON;
        private JSONStorableStringChooser _lengthModeJSON;
        private JSONStorableFloat _lengthJSON;
        private JSONStorableBool _ensureQuaternionContinuity;
        private JSONStorableBool _loop;
        private JSONStorableBool _autoPlayJSON;
        private JSONStorableStringChooser _linkedAnimationPatternJSON;
        private float _lengthWhenLengthModeChanged;

        public EditAnimationScreen(IAtomPlugin plugin)
            : base(plugin)
        {

        }

        #region Init

        public override void Init()
        {
            base.Init();

            // Right side

            CreateChangeScreenButton("<b><</b> <i>Back</i>", MoreScreen.ScreenName, true);

            CreateSpacer(true);

            InitAnimationNameUI(true);

            CreateSpacer(true);

            InitAnimationLengthUI(true);

            CreateSpacer(true);

            InitMiscSettingsUI(true);

            CreateSpacer(true);

            InitAnimationPatternLinkUI(true);

            _lengthWhenLengthModeChanged = Current?.AnimationLength ?? 0;
            UpdateValues();
        }

        private void InitAnimationNameUI(bool rightSide)
        {
            {
                var animationLabelJSON = new JSONStorableString("Rename Animation", "Rename animation:");
                RegisterStorable(animationLabelJSON);
                var animationNameLabelUI = Plugin.CreateTextField(animationLabelJSON, rightSide);
                RegisterComponent(animationNameLabelUI);
                var layout = animationNameLabelUI.GetComponent<LayoutElement>();
                layout.minHeight = 36f;
                animationNameLabelUI.height = 36f;
                UnityEngine.Object.Destroy(animationNameLabelUI.gameObject.GetComponentInChildren<Image>());
            }

            {
                _animationNameJSON = new JSONStorableString("Animation Name", "", (string val) => UpdateAnimationName(val));
                RegisterStorable(_animationNameJSON);
                var animationNameUI = Plugin.CreateTextInput(_animationNameJSON, rightSide);
                RegisterComponent(animationNameUI);
                var layout = animationNameUI.GetComponent<LayoutElement>();
                layout.minHeight = 50f;
                animationNameUI.height = 50;

                _animationNameJSON.valNoCallback = Current.AnimationName;
            }
        }

        private void InitAnimationLengthUI(bool rightSide)
        {
            UIDynamicButton applyLengthUI = null;

            _lengthModeJSON = new JSONStorableStringChooser("Change Length Mode", new List<string> {
                ChangeLengthModeCropExtendEnd,
                ChangeLengthModeAddKeyframeEnd,
                ChangeLengthModeCropExtendBegin,
                ChangeLengthModeAddKeyframeBegin,
                ChangeLengthModeCropExtendAtTime,
                ChangeLengthModeStretch,
                ChangeLengthModeLoop
             }, ChangeLengthModeCropExtendEnd, "Change Length Mode", (string val) =>
             {
                 _lengthWhenLengthModeChanged = Current?.AnimationLength ?? 0f;
             });
            RegisterStorable(_lengthModeJSON);
            var lengthModeUI = Plugin.CreateScrollablePopup(_lengthModeJSON, rightSide);
            lengthModeUI.popupPanelHeight = 550f;
            RegisterComponent(lengthModeUI);

            _lengthJSON = new JSONStorableFloat("Change Length To (s)", AtomAnimationClip.DefaultAnimationLength, 0.5f, 10f, false, true);
            RegisterStorable(_lengthJSON);
            var lengthUI = Plugin.CreateSlider(_lengthJSON, rightSide);
            lengthUI.valueFormat = "F3";
            RegisterComponent(lengthUI);

            applyLengthUI = Plugin.CreateButton("Apply", rightSide);
            RegisterComponent(applyLengthUI);
            applyLengthUI.button.onClick.AddListener(() =>
            {
                UpdateAnimationLength(_lengthJSON.val);
            });
        }

        private void InitMiscSettingsUI(bool rightSide)
        {
            _loop = new JSONStorableBool("Loop", Current?.Loop ?? true, (bool val) => ChangeLoop(val));
            RegisterStorable(_loop);
            var loopUI = Plugin.CreateToggle(_loop, rightSide);
            RegisterComponent(loopUI);

            _ensureQuaternionContinuity = new JSONStorableBool("Ensure Quaternion Continuity", true, (bool val) => SetEnsureQuaternionContinuity(val));
            RegisterStorable(_ensureQuaternionContinuity);
            var ensureQuaternionContinuityUI = Plugin.CreateToggle(_ensureQuaternionContinuity, rightSide);
            RegisterComponent(ensureQuaternionContinuityUI);

            _autoPlayJSON = new JSONStorableBool("Auto Play On Load", false, (bool val) =>
            {
                foreach (var c in Plugin.Animation.Clips)
                    c.AutoPlay = false;
                Current.AutoPlay = true;
            })
            {
                isStorable = false
            };
            RegisterStorable(_autoPlayJSON);
            var autoPlayUI = Plugin.CreateToggle(_autoPlayJSON, rightSide);
            RegisterComponent(autoPlayUI);
        }

        private void InitAnimationPatternLinkUI(bool rightSide)
        {
            _linkedAnimationPatternJSON = new JSONStorableStringChooser("Linked Animation Pattern", new[] { "" }.Concat(SuperController.singleton.GetAtoms().Where(a => a.type == "AnimationPattern").Select(a => a.uid)).ToList(), "", "Linked Animation Pattern", (string uid) => LinkAnimationPattern(uid))
            {
                isStorable = false
            };
            RegisterStorable(_linkedAnimationPatternJSON);
            var linkedAnimationPatternUI = Plugin.CreateScrollablePopup(_linkedAnimationPatternJSON, rightSide);
            linkedAnimationPatternUI.popupPanelHeight = 800f;
            linkedAnimationPatternUI.popup.onOpenPopupHandlers += () => _linkedAnimationPatternJSON.choices = new[] { "" }.Concat(SuperController.singleton.GetAtoms().Where(a => a.type == "AnimationPattern").Select(a => a.uid)).ToList();
            RegisterComponent(linkedAnimationPatternUI);
        }

        #endregion

        #region Callbacks

        private void UpdateAnimationName(string val)
        {
            var previousAnimationName = Current.AnimationName;
            if (string.IsNullOrEmpty(val))
            {
                _animationNameJSON.valNoCallback = previousAnimationName;
                return;
            }
            if (Plugin.Animation.Clips.Any(c => c.AnimationName == val))
            {
                _animationNameJSON.valNoCallback = previousAnimationName;
                return;
            }
            Current.AnimationName = val;
            foreach (var clip in Plugin.Animation.Clips)
            {
                if (clip.NextAnimationName == previousAnimationName)
                    clip.NextAnimationName = val;
            }
        }

        private void UpdateAnimationLength(float newLength)
        {
            if (_lengthWhenLengthModeChanged == 0f) return;

            newLength = newLength.Snap(Plugin.SnapJSON.val);
            if (newLength < 0.1f) newLength = 0.1f;
            var time = Plugin.Animation.Time.Snap();

            switch (_lengthModeJSON.val)
            {
                case ChangeLengthModeStretch:
                    Current.StretchLength(newLength);
                    _lengthWhenLengthModeChanged = newLength;
                    break;
                case ChangeLengthModeCropExtendEnd:
                    Current.CropOrExtendLengthEnd(newLength);
                    _lengthWhenLengthModeChanged = newLength;
                    break;
                case ChangeLengthModeCropExtendBegin:
                    Current.CropOrExtendLengthBegin(newLength);
                    _lengthWhenLengthModeChanged = newLength;
                    break;
                case ChangeLengthModeCropExtendAtTime:
                    {
                        if (Plugin.Animation.IsPlaying())
                        {
                            _lengthJSON.valNoCallback = Current.AnimationLength;
                            return;
                        }
                        var previousKeyframe = Current.AllTargets.SelectMany(t => t.GetAllKeyframesTime()).Where(t => t <= time + 0.0011f).Max();
                        var nextKeyframe = Current.AllTargets.SelectMany(t => t.GetAllKeyframesTime()).Where(t => t > time + 0.0001f).Min();

                        var keyframeAllowedDiff = (nextKeyframe - time - 0.001f).Snap();

                        if ((Current.AnimationLength - newLength) > keyframeAllowedDiff)
                        {
                            newLength = Current.AnimationLength - keyframeAllowedDiff;
                        }

                        Current.CropOrExtendLengthAtTime(newLength, time);
                        break;
                    }
                case ChangeLengthModeAddKeyframeEnd:
                    {
                        if (newLength <= _lengthWhenLengthModeChanged + float.Epsilon)
                        {
                            _lengthJSON.valNoCallback = Current.AnimationLength;
                            return;
                        }
                        var snapshot = Current.Copy(_lengthWhenLengthModeChanged, true);
                        Current.CropOrExtendLengthEnd(newLength);
                        Current.Paste(_lengthWhenLengthModeChanged, snapshot);
                        break;
                    }
                case ChangeLengthModeAddKeyframeBegin:
                    {
                        if (newLength <= _lengthWhenLengthModeChanged + float.Epsilon)
                        {
                            _lengthJSON.valNoCallback = Current.AnimationLength;
                            return;
                        }
                        var snapshot = Current.Copy(0f, true);
                        Current.CropOrExtendLengthBegin(newLength);
                        Current.Paste((newLength - _lengthWhenLengthModeChanged).Snap(), snapshot);
                        break;
                    }
                case ChangeLengthModeLoop:
                    {
                        newLength = newLength.Snap(_lengthWhenLengthModeChanged);
                        var loops = (int)Math.Round(newLength / _lengthWhenLengthModeChanged);
                        if (loops <= 1 || newLength <= _lengthWhenLengthModeChanged)
                        {
                            _lengthJSON.valNoCallback = Current.AnimationLength;
                            return;
                        }
                        var frames = Current
                            .TargetControllers.SelectMany(t => t.GetLeadCurve().keys.Select(k => k.time))
                            .Concat(Current.TargetFloatParams.SelectMany(t => t.Value.keys.Select(k => k.time)))
                            .Select(t => t.Snap())
                            .Where(t => t < _lengthWhenLengthModeChanged)
                            .Distinct()
                            .ToList();

                        var snapshots = frames.Select(f => Current.Copy(f, true)).ToList();
                        foreach (var c in snapshots[0].Controllers)
                        {
                            c.Snapshot.CurveType = CurveTypeValues.Smooth;
                        }

                        Current.CropOrExtendLengthEnd(newLength);

                        for (var repeat = 0; repeat < loops; repeat++)
                        {
                            for (var i = 0; i < frames.Count; i++)
                            {
                                var pasteTime = frames[i] + (_lengthWhenLengthModeChanged * repeat);
                                if (pasteTime >= newLength) continue;
                                Current.Paste(pasteTime, snapshots[i]);
                            }
                        }
                    }
                    break;
                default:
                    SuperController.LogError($"VamTimeline: Unknown animation length type: {_lengthModeJSON.val}");
                    break;
            }

            Current.DirtyAll();

            Plugin.Animation.Time = Math.Max(time, newLength);
        }

        private void ChangeLoop(bool val)
        {
            Current.Loop = val;
        }

        private void SetEnsureQuaternionContinuity(bool val)
        {
            Current.EnsureQuaternionContinuity = val;
        }

        private void LinkAnimationPattern(string uid)
        {
            if (string.IsNullOrEmpty(uid))
            {
                Current.AnimationPattern = null;
                return;
            }
            var animationPattern = SuperController.singleton.GetAtomByUid(uid)?.GetComponentInChildren<AnimationPattern>();
            if (animationPattern == null)
            {
                SuperController.LogError($"VamTimeline: Could not find Animation Pattern '{uid}'");
                return;
            }
            animationPattern.SetBoolParamValue("autoPlay", false);
            animationPattern.SetBoolParamValue("pause", false);
            animationPattern.SetBoolParamValue("loop", false);
            animationPattern.SetBoolParamValue("loopOnce", false);
            animationPattern.SetFloatParamValue("speed", Plugin.Animation.Speed);
            animationPattern.ResetAnimation();
            Current.AnimationPattern = animationPattern;
        }

        #endregion

        #region Events

        protected override void OnCurrentAnimationChanged(AtomAnimation.CurrentAnimationChangedEventArgs args)
        {
            base.OnCurrentAnimationChanged(args);

            UpdateValues();
        }

        private void UpdateValues()
        {
            _lengthJSON.valNoCallback = Current.AnimationLength;
            _animationNameJSON.valNoCallback = Current.AnimationName;
            _loop.valNoCallback = Current.Loop;
            _ensureQuaternionContinuity.valNoCallback = Current.EnsureQuaternionContinuity;
            _autoPlayJSON.valNoCallback = Current.AutoPlay;
            _linkedAnimationPatternJSON.valNoCallback = Current.AnimationPattern?.containingAtom.uid ?? "";
        }

        public override void Dispose()
        {
            base.Dispose();
        }

        #endregion
    }
}

