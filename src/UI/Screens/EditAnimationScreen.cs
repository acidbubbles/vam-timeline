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
        public const string ChangeLengthModeCropExtendEnd = "Crop/Extend End";
        public const string ChangeLengthModeAddKeyframeEnd = "Add Keyframe End";
        public const string ChangeLengthModeCropExtendBegin = "Crop/Extend Begin";
        public const string ChangeLengthModeAddKeyframeBegin = "Add Keyframe Begin";
        public const string ChangeLengthModeCropExtendAtTime = "Crop/Extend At Time";
        public const string ChangeLengthModeStretch = "Stretch";
        public const string ChangeLengthModeLoop = "Loop (Extend)";

        public override string screenId => ScreenName;

        private JSONStorableString _animationNameJSON;
        private JSONStorableStringChooser _lengthModeJSON;
        private JSONStorableFloat _lengthJSON;
        private JSONStorableBool _ensureQuaternionContinuity;
        private JSONStorableBool _loop;
        private JSONStorableBool _autoPlayJSON;
        private JSONStorableStringChooser _linkedAnimationPatternJSON;
        private float _lengthWhenLengthModeChanged;

        public EditAnimationScreen()
            : base()
        {
        }

        #region Init

        public override void Init(IAtomPlugin plugin)
        {
            base.Init(plugin);

            // Right side

            CreateChangeScreenButton("<b><</b> <i>Back</i>", MoreScreen.ScreenName);

            prefabFactory.CreateSpacer();

            InitAnimationNameUI();

            prefabFactory.CreateSpacer();

            InitAnimationLengthUI();

            prefabFactory.CreateSpacer();

            InitMiscSettingsUI();

            prefabFactory.CreateSpacer();

            InitAnimationPatternLinkUI();

            prefabFactory.CreateSpacer();

            CreateChangeScreenButton("<i><b>Sequence</b> animations...</i>", EditSequenceScreen.ScreenName);

            _lengthWhenLengthModeChanged = current?.animationLength ?? 0;
            UpdateValues();
        }

        private void InitAnimationNameUI()
        {
            {
                var animationLabelJSON = new JSONStorableString("Rename Animation", "Rename animation:");
                var animationNameLabelUI = prefabFactory.CreateTextField(animationLabelJSON);
                var layout = animationNameLabelUI.GetComponent<LayoutElement>();
                layout.minHeight = 36f;
                animationNameLabelUI.height = 36f;
                Destroy(animationNameLabelUI.gameObject.GetComponentInChildren<Image>());
            }

            {
                _animationNameJSON = new JSONStorableString("Animation Name", "", (string val) => UpdateAnimationName(val));
                var animationNameUI = prefabFactory.CreateTextInput(_animationNameJSON);
                var layout = animationNameUI.GetComponent<LayoutElement>();
                layout.minHeight = 50f;
                animationNameUI.height = 50;

                _animationNameJSON.valNoCallback = current.animationName;
            }
        }

        private void InitAnimationLengthUI()
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
                 _lengthWhenLengthModeChanged = current?.animationLength ?? 0f;
             });
            var lengthModeUI = prefabFactory.CreateScrollablePopup(_lengthModeJSON);
            lengthModeUI.popupPanelHeight = 550f;

            _lengthJSON = new JSONStorableFloat("Change Length To (s)", AtomAnimationClip.DefaultAnimationLength, 0.5f, 10f, false, true);
            var lengthUI = prefabFactory.CreateSlider(_lengthJSON);
            lengthUI.valueFormat = "F3";

            applyLengthUI = prefabFactory.CreateButton("Apply");
            applyLengthUI.button.onClick.AddListener(() =>
            {
                UpdateAnimationLength(_lengthJSON.val);
            });
        }

        private void InitMiscSettingsUI()
        {
            _loop = new JSONStorableBool("Loop", current?.loop ?? true, (bool val) => ChangeLoop(val));
            var loopUI = prefabFactory.CreateToggle(_loop);

            _ensureQuaternionContinuity = new JSONStorableBool("Ensure Quaternion Continuity", true, (bool val) => SetEnsureQuaternionContinuity(val));
            var ensureQuaternionContinuityUI = prefabFactory.CreateToggle(_ensureQuaternionContinuity);

            _autoPlayJSON = new JSONStorableBool("Auto Play On Load", false, (bool val) =>
            {
                foreach (var c in animation.clips.Where(c => c != current && c.animationLayer == current.animationLayer))
                    c.autoPlay = false;
                current.autoPlay = true;
            })
            {
                isStorable = false
            };
            var autoPlayUI = prefabFactory.CreateToggle(_autoPlayJSON);
        }

        private void InitAnimationPatternLinkUI()
        {
            _linkedAnimationPatternJSON = new JSONStorableStringChooser("Linked Animation Pattern", new[] { "" }.Concat(SuperController.singleton.GetAtoms().Where(a => a.type == "AnimationPattern").Select(a => a.uid)).ToList(), "", "Linked Animation Pattern", (string uid) => LinkAnimationPattern(uid))
            {
                isStorable = false
            };
            var linkedAnimationPatternUI = prefabFactory.CreateScrollablePopup(_linkedAnimationPatternJSON);
            linkedAnimationPatternUI.popupPanelHeight = 800f;
            linkedAnimationPatternUI.popup.onOpenPopupHandlers += () => _linkedAnimationPatternJSON.choices = new[] { "" }.Concat(SuperController.singleton.GetAtoms().Where(a => a.type == "AnimationPattern").Select(a => a.uid)).ToList();
        }

        #endregion

        #region Callbacks

        private void UpdateAnimationName(string val)
        {
            var previousAnimationName = current.animationName;
            if (string.IsNullOrEmpty(val))
            {
                _animationNameJSON.valNoCallback = previousAnimationName;
                return;
            }
            if (animation.clips.Any(c => c.animationName == val))
            {
                _animationNameJSON.valNoCallback = previousAnimationName;
                return;
            }
            current.animationName = val;
            foreach (var clip in animation.clips)
            {
                if (clip.nextAnimationName == previousAnimationName)
                    clip.nextAnimationName = val;
            }
        }

        private void UpdateAnimationLength(float newLength)
        {
            if (_lengthWhenLengthModeChanged == 0f) return;

            newLength = newLength.Snap(plugin.snapJSON.val);
            if (newLength < 0.1f) newLength = 0.1f;
            var time = animation.clipTime.Snap();

            switch (_lengthModeJSON.val)
            {
                case ChangeLengthModeStretch:
                    operations.Resize().Stretch(newLength);
                    _lengthWhenLengthModeChanged = newLength;
                    break;
                case ChangeLengthModeCropExtendEnd:
                    operations.Resize().CropOrExtendEnd(newLength);
                    _lengthWhenLengthModeChanged = newLength;
                    break;
                case ChangeLengthModeCropExtendBegin:
                    operations.Resize().CropOrExtendBegin(newLength);
                    _lengthWhenLengthModeChanged = newLength;
                    break;
                case ChangeLengthModeCropExtendAtTime:
                    {
                        if (animation.isPlaying)
                        {
                            _lengthJSON.valNoCallback = current.animationLength;
                            return;
                        }
                        var previousKeyframe = current.allTargets.SelectMany(t => t.GetAllKeyframesTime()).Where(t => t <= time + 0.0011f).Max();
                        var nextKeyframe = current.allTargets.SelectMany(t => t.GetAllKeyframesTime()).Where(t => t > time + 0.0001f).Min();

                        var keyframeAllowedDiff = (nextKeyframe - time - 0.001f).Snap();

                        if ((current.animationLength - newLength) > keyframeAllowedDiff)
                        {
                            newLength = current.animationLength - keyframeAllowedDiff;
                        }

                        operations.Resize().CropOrExtendAtTime(newLength, time);
                        break;
                    }
                case ChangeLengthModeAddKeyframeEnd:
                    {
                        if (newLength <= _lengthWhenLengthModeChanged + float.Epsilon)
                        {
                            _lengthJSON.valNoCallback = current.animationLength;
                            return;
                        }
                        var snapshot = current.Copy(_lengthWhenLengthModeChanged, true);
                        operations.Resize().CropOrExtendEnd(newLength);
                        current.Paste(_lengthWhenLengthModeChanged, snapshot);
                        break;
                    }
                case ChangeLengthModeAddKeyframeBegin:
                    {
                        if (newLength <= _lengthWhenLengthModeChanged + float.Epsilon)
                        {
                            _lengthJSON.valNoCallback = current.animationLength;
                            return;
                        }
                        var snapshot = current.Copy(0f, true);
                        operations.Resize().CropOrExtendBegin(newLength);
                        current.Paste((newLength - _lengthWhenLengthModeChanged).Snap(), snapshot);
                        break;
                    }
                case ChangeLengthModeLoop:
                    {
                        newLength = newLength.Snap(_lengthWhenLengthModeChanged);
                        var loops = (int)Math.Round(newLength / _lengthWhenLengthModeChanged);
                        if (loops <= 1 || newLength <= _lengthWhenLengthModeChanged)
                        {
                            _lengthJSON.valNoCallback = current.animationLength;
                            return;
                        }
                        var frames = current
                            .targetControllers.SelectMany(t => t.GetLeadCurve().keys.Select(k => k.time))
                            .Concat(current.targetFloatParams.SelectMany(t => t.value.keys.Select(k => k.time)))
                            .Select(t => t.Snap())
                            .Where(t => t < _lengthWhenLengthModeChanged)
                            .Distinct()
                            .ToList();

                        var snapshots = frames.Select(f => current.Copy(f, true)).ToList();
                        foreach (var c in snapshots[0].controllers)
                        {
                            c.snapshot.curveType = CurveTypeValues.Smooth;
                        }

                        operations.Resize().CropOrExtendEnd(newLength);

                        for (var repeat = 0; repeat < loops; repeat++)
                        {
                            for (var i = 0; i < frames.Count; i++)
                            {
                                var pasteTime = frames[i] + (_lengthWhenLengthModeChanged * repeat);
                                if (pasteTime >= newLength) continue;
                                current.Paste(pasteTime, snapshots[i]);
                            }
                        }
                    }
                    break;
                default:
                    SuperController.LogError($"VamTimeline: Unknown animation length type: {_lengthModeJSON.val}");
                    break;
            }

            current.DirtyAll();

            animation.clipTime = Math.Max(time, newLength);
        }

        private void ChangeLoop(bool val)
        {
            current.loop = val;
        }

        private void SetEnsureQuaternionContinuity(bool val)
        {
            current.ensureQuaternionContinuity = val;
        }

        private void LinkAnimationPattern(string uid)
        {
            if (string.IsNullOrEmpty(uid))
            {
                current.animationPattern = null;
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
            animationPattern.SetFloatParamValue("speed", animation.speed);
            animationPattern.ResetAnimation();
            current.animationPattern = animationPattern;
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
            _lengthJSON.valNoCallback = current.animationLength;
            _animationNameJSON.valNoCallback = current.animationName;
            _loop.valNoCallback = current.loop;
            _ensureQuaternionContinuity.valNoCallback = current.ensureQuaternionContinuity;
            _autoPlayJSON.valNoCallback = current.autoPlay;
            _linkedAnimationPatternJSON.valNoCallback = current.animationPattern?.containingAtom.uid ?? "";
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
        }

        #endregion
    }
}

