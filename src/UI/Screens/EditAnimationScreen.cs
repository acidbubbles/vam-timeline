using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class EditAnimationScreen : ScreenBase
    {
        public const string ScreenName = "Edit";
        public const string ChangeLengthModeCropExtendEnd = "Crop/Extend (End)";
        public const string ChangeLengthModeCropExtendBegin = "Crop/Extend (Begin)";
        public const string ChangeLengthModeCropExtendAtTime = "Crop/Extend (Time)";
        public const string ChangeLengthModeStretch = "Stretch";
        public const string ChangeLengthModeLoop = "Loop";

        public override string screenId => ScreenName;

        private JSONStorableStringChooser _lengthModeJSON;
        private JSONStorableFloat _lengthJSON;
        private JSONStorableBool _ensureQuaternionContinuity;
        private JSONStorableBool _loop;
        private JSONStorableStringChooser _linkedAnimationPatternJSON;
        private JSONStorableString _layerNameJSON;
        private JSONStorableString _animationNameJSON;
        private UIDynamicToggle _loopUI;
        private JSONStorableFloat _animationSpeedJSON;
        private JSONStorableFloat _clipSpeedJSON;
        private JSONStorableFloat _clipWeightJSON;
        private UIDynamicButton _applyLengthUI;

        public EditAnimationScreen()
            : base()
        {
        }

        #region Init

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            InitRenameLayer();
            InitRenameAnimation();

            CreateHeader("Speed", 1);
            InitPlaybackUI();

            CreateHeader("Options", 1);
            InitLoopUI();

            CreateHeader("Length", 1);
            InitAnimationLengthUI();

            CreateHeader("Advanced", 1);
            InitWeightUI();
            InitEnsureQuaternionContinuityUI();
            InitAnimationPatternLinkUI();

            current.onAnimationSettingsChanged.AddListener(OnAnimationSettingsChanged);
            current.onPlaybackSettingsChanged.AddListener(OnPlaybackSettingsChanged);
            animation.onSpeedChanged.AddListener(OnSpeedChanged);
            OnSpeedChanged();
            OnPlaybackSettingsChanged();
            UpdateValues();
        }

        private void InitPlaybackUI()
        {
            _animationSpeedJSON = new JSONStorableFloat("Speed (Global)", 1f, (float val) => animation.speed = val, -1f, 5f, false)
            {
                valNoCallback = animation.speed
            };
            var animationSpeedUI = prefabFactory.CreateSlider(_animationSpeedJSON);
            animationSpeedUI.valueFormat = "F3";

            _clipSpeedJSON = new JSONStorableFloat("Speed (Local)", 1f, (float val) => { foreach (var clip in animation.GetClips(current.animationName)) { clip.speed = val; } }, -1f, 5f, false)
            {
                valNoCallback = current.speed
            };
            var clipSpeedUI = prefabFactory.CreateSlider(_clipSpeedJSON);
            clipSpeedUI.valueFormat = "F3";
        }

        private void InitWeightUI()
        {
            _clipWeightJSON = new JSONStorableFloat("Weight", 1f, (float val) => current.weight = val, 0f, 1f, true)
            {
                valNoCallback = current.weight
            };
            var clipWeigthUI = prefabFactory.CreateSlider(_clipWeightJSON);
            clipWeigthUI.valueFormat = "F4";
        }

        private void InitRenameLayer()
        {
            _layerNameJSON = new JSONStorableString("Layer name (share targets)", "", (string val) => UpdateLayerName(val));
            var layerNameUI = prefabFactory.CreateTextInput(_layerNameJSON);
            _layerNameJSON.valNoCallback = current.animationLayer;
        }
        private void UpdateLayerName(string to)
        {
            to = to.Trim();
            if (to == "" || to == current.animationLayer)
            {
                _layerNameJSON.valNoCallback = current.animationLayer;
                return;
            }

            var from = current.animationLayer;
            if (animation.clips.Any(c => c.animationLayer == to))
            {
                _layerNameJSON.valNoCallback = current.animationLayer;
                return;
            }

            foreach (var clip in animation.clips.Where(c => c.animationLayer == from))
            {
                clip.animationLayer = to;
            }
        }

        private void InitRenameAnimation()
        {
            _animationNameJSON = new JSONStorableString("Animation name (group with 'group/anim')", "", (string val) => UpdateAnimationName(val));
            var animationNameUI = prefabFactory.CreateTextInput(_animationNameJSON);
            _animationNameJSON.valNoCallback = current.animationName;
        }

        private void UpdateAnimationName(string val)
        {
            var previousAnimationName = current.animationName;
            if (string.IsNullOrEmpty(val))
            {
                _animationNameJSON.valNoCallback = current.animationName;
                return;
            }
            if (animation.clips.Any(c => c.animationLayer == current.animationLayer && c.animationName == val))
            {
                _animationNameJSON.valNoCallback = current.animationName;
                return;
            }
            current.animationName = val;
            var existing = animation.clips.FirstOrDefault(c => c != current && c.animationName == current.animationName);
            if (existing != null && existing.nextAnimationName != null)
            {
                var next = animation.clips.FirstOrDefault(c => c.animationLayer == current.animationLayer && c.animationName == existing.nextAnimationName);
                if (next != null)
                {
                    current.nextAnimationName = next.nextAnimationName;
                    current.nextAnimationTime = next.nextAnimationTime;
                }
            }
            foreach (var other in animation.clips)
            {
                if (other == current) continue;
                if (other.nextAnimationName == previousAnimationName && other.animationLayer == current.animationLayer)
                {
                    other.nextAnimationName = val;
                }
            }
        }

        private void InitAnimationLengthUI()
        {
            _lengthModeJSON = new JSONStorableStringChooser("Length mode", new List<string> {
                ChangeLengthModeCropExtendEnd,
                ChangeLengthModeCropExtendBegin,
                ChangeLengthModeCropExtendAtTime,
                ChangeLengthModeStretch,
                ChangeLengthModeLoop
             }, ChangeLengthModeCropExtendEnd, "Length mode");
            var lengthModeUI = prefabFactory.CreatePopup(_lengthModeJSON, false, true);
            lengthModeUI.popupPanelHeight = 350f;

            _lengthJSON = new JSONStorableFloat(
                "Change length to (s)",
                AtomAnimationClip.DefaultAnimationLength,
                (float val) =>
                {
                    _lengthJSON.valNoCallback = val.Snap(animationEditContext.snap);
                    if (_lengthJSON.valNoCallback < 0.1f)
                        _lengthJSON.valNoCallback = 0.1f;
                    if (_lengthModeJSON.val == ChangeLengthModeCropExtendAtTime && _lengthJSON.valNoCallback < animationEditContext.clipTime + animationEditContext.snap)
                        _lengthJSON.valNoCallback = animationEditContext.clipTime + animationEditContext.snap;
                    _applyLengthUI.button.interactable = !_lengthJSON.val.IsSameFrame(current.animationLength);
                },
                0f,
                Mathf.Max((current.animationLength * 5f).Snap(10f), 10f),
                false,
                true);
            var lengthUI = prefabFactory.CreateSlider(_lengthJSON);
            lengthUI.valueFormat = "F3";

            _applyLengthUI = prefabFactory.CreateButton("Apply");
            _applyLengthUI.button.onClick.AddListener(() =>
            {
                UpdateAnimationLength(_lengthJSON.val);
                _applyLengthUI.button.interactable = false;
            });
            _applyLengthUI.button.interactable = false;
        }

        private void InitEnsureQuaternionContinuityUI()
        {
            _ensureQuaternionContinuity = new JSONStorableBool("Ensure Quaternion Continuity", true, (bool val) => SetEnsureQuaternionContinuity(val));
            var ensureQuaternionContinuityUI = prefabFactory.CreateToggle(_ensureQuaternionContinuity);
        }

        private void InitAnimationPatternLinkUI()
        {
            _linkedAnimationPatternJSON = new JSONStorableStringChooser("Link", new[] { "" }.Concat(SuperController.singleton.GetAtoms().Where(a => a.type == "AnimationPattern").Select(a => a.uid)).ToList(), "", "Link", (string uid) => LinkAnimationPattern(uid))
            {
                isStorable = false
            };
            var linkedAnimationPatternUI = prefabFactory.CreatePopup(_linkedAnimationPatternJSON, true, true);
            linkedAnimationPatternUI.popupPanelHeight = 240f;
            linkedAnimationPatternUI.popup.onOpenPopupHandlers += () => _linkedAnimationPatternJSON.choices = new[] { "" }.Concat(SuperController.singleton.GetAtoms().Where(a => a.type == "AnimationPattern").Select(a => a.uid)).ToList();
        }

        private void InitLoopUI()
        {
            _loop = new JSONStorableBool("Loop", current?.loop ?? true, (bool val) =>
            {
                current.loop = val;
            });
            _loopUI = prefabFactory.CreateToggle(_loop);
        }

        #endregion

        #region Callbacks

        private void UpdateAnimationLength(float newLength)
        {
            if (!animationEditContext.CanEdit())
            {
                _lengthJSON.valNoCallback = current.animationLength;
                return;
            }

            newLength = newLength.Snap(animationEditContext.snap);
            if (newLength < 0.1f) newLength = 0.1f;
            var time = animationEditContext.clipTime.Snap();

            switch (_lengthModeJSON.val)
            {
                case ChangeLengthModeStretch:
                    operations.Resize().Stretch(newLength);
                    break;
                case ChangeLengthModeCropExtendEnd:
                    operations.Resize().CropOrExtendEnd(newLength);
                    break;
                case ChangeLengthModeCropExtendBegin:
                    operations.Resize().CropOrExtendAt(newLength, 0f);
                    break;
                case ChangeLengthModeCropExtendAtTime:
                    if (_lengthJSON.valNoCallback < animationEditContext.clipTime + animationEditContext.snap)
                        _lengthJSON.valNoCallback = animationEditContext.clipTime + animationEditContext.snap;
                    operations.Resize().CropOrExtendAt(newLength, time);
                    break;
                case ChangeLengthModeLoop:
                    operations.Resize().Loop(newLength);
                    break;
                default:
                    SuperController.LogError($"Timeline: Unknown animation length type: {_lengthModeJSON.val}");
                    break;
            }

            _lengthJSON.valNoCallback = current.animationLength;
            current.DirtyAll();

            animationEditContext.clipTime = Math.Min(time, newLength);
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
                SuperController.LogError($"Timeline: Could not find Animation Pattern '{uid}'");
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

        protected override void OnCurrentAnimationChanged(AtomAnimationEditContext.CurrentAnimationChangedEventArgs args)
        {
            base.OnCurrentAnimationChanged(args);

            args.before.onAnimationSettingsChanged.RemoveListener(OnAnimationSettingsChanged);
            args.before.onPlaybackSettingsChanged.RemoveListener(OnPlaybackSettingsChanged);
            args.after.onAnimationSettingsChanged.AddListener(OnAnimationSettingsChanged);
            args.after.onPlaybackSettingsChanged.AddListener(OnPlaybackSettingsChanged);

            OnPlaybackSettingsChanged();
            UpdateValues();
        }

        private void OnAnimationSettingsChanged(string _)
        {
            UpdateValues();
        }

        private void OnPlaybackSettingsChanged()
        {
            _clipWeightJSON.valNoCallback = current.weight;
            _clipSpeedJSON.valNoCallback = current.speed;
        }

        private void OnSpeedChanged()
        {
            _animationSpeedJSON.valNoCallback = animation.speed;
        }

        private void UpdateValues()
        {
            _animationNameJSON.valNoCallback = current.animationName;
            _layerNameJSON.valNoCallback = current.animationLayer;
            _lengthJSON.valNoCallback = current.animationLength;
            _lengthJSON.max = Mathf.Max((current.animationLength * 5f).Snap(10f), 10f);
            _loop.valNoCallback = current.loop;
            _loopUI.toggle.interactable = !current.autoTransitionNext;
            _ensureQuaternionContinuity.valNoCallback = current.ensureQuaternionContinuity;
            _linkedAnimationPatternJSON.valNoCallback = current.animationPattern?.containingAtom.uid ?? "";
        }

        public override void OnDestroy()
        {
            animation.onSpeedChanged.RemoveListener(OnSpeedChanged);
            current.onAnimationSettingsChanged.RemoveListener(OnAnimationSettingsChanged);
            current.onPlaybackSettingsChanged.RemoveListener(OnPlaybackSettingsChanged);
            base.OnDestroy();
        }

        #endregion
    }
}

