using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;

namespace VamTimeline
{
    public class EditAnimationScreen : ScreenBase
    {
        public const string ScreenName = "Edit";
        public const string ChangeLengthModeCropExtendEnd = "Crop/Extend End";
        public const string ChangeLengthModeCropExtendBegin = "Crop/Extend Begin";
        public const string ChangeLengthModeCropExtendAtTime = "Crop/Extend At Time";
        public const string ChangeLengthModeStretch = "Stretch";
        public const string ChangeLengthModeLoop = "Loop (Extend)";

        public override string screenId => ScreenName;

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

            prefabFactory.CreateSpacer();

            InitMiscSettingsUI();

            prefabFactory.CreateSpacer();

            InitAnimationLengthUI();

            prefabFactory.CreateSpacer();

            InitAnimationPatternLinkUI();

            _lengthWhenLengthModeChanged = current?.animationLength ?? 0;
            UpdateValues();
        }

        private void InitAnimationLengthUI()
        {
            UIDynamicButton applyLengthUI = null;

            _lengthModeJSON = new JSONStorableStringChooser("Change Length Mode", new List<string> {
                ChangeLengthModeCropExtendEnd,
                ChangeLengthModeCropExtendBegin,
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

        private void UpdateAnimationLength(float newLength)
        {
            if (_lengthWhenLengthModeChanged == 0f) return;
            if (animation.isPlaying)
            {
                _lengthJSON.valNoCallback = current.animationLength;
                return;
            }

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
                    operations.Resize().CropOrExtendAtTime(newLength, time);
                    break;
                case ChangeLengthModeLoop:
                    operations.Resize().Loop(newLength, _lengthWhenLengthModeChanged);
                    break;
                default:
                    SuperController.LogError($"VamTimeline: Unknown animation length type: {_lengthModeJSON.val}");
                    break;
            }

            _lengthJSON.valNoCallback = current.animationLength;
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

