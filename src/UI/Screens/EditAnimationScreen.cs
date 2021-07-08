using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class EditAnimationScreen : ScreenBase
    {
        public const string ScreenName = "Edit";
        private const string _changeLengthModeCropExtendEnd = "Crop/Extend (End)";
        private const string _changeLengthModeCropExtendBegin = "Crop/Extend (Begin)";
        private const string _changeLengthModeCropExtendAtTime = "Crop/Extend (Time)";
        private const string _changeLengthModeStretch = "Stretch";
        private const string _changeLengthModeLoop = "Loop";

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

        #region Init

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            InitRenameLayer();
            InitRenameAnimation();

            prefabFactory.CreateHeader("Speed", 1);
            InitPlaybackUI();

            prefabFactory.CreateHeader("Options", 1);
            InitLoopUI();

            prefabFactory.CreateHeader("Length", 1);
            InitAnimationLengthUI();

            prefabFactory.CreateHeader("Advanced", 1);
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
            _animationSpeedJSON = new JSONStorableFloat("Speed (Global)", 1f, val => animation.speed = val, -1f, 5f, false)
            {
                valNoCallback = animation.speed
            };
            var animationSpeedUI = prefabFactory.CreateSlider(_animationSpeedJSON);
            animationSpeedUI.valueFormat = "F3";

            _clipSpeedJSON = new JSONStorableFloat("Speed (Local)", 1f, val => { foreach (var clip in animation.GetClips(current.animationName)) { clip.speed = val; } }, -1f, 5f, false)
            {
                valNoCallback = current.speed
            };
            var clipSpeedUI = prefabFactory.CreateSlider(_clipSpeedJSON);
            clipSpeedUI.valueFormat = "F3";
        }

        private void InitWeightUI()
        {
            _clipWeightJSON = new JSONStorableFloat("Weight", 1f, val => current.weight = val, 0f, 1f)
            {
                valNoCallback = current.weight
            };
            var clipWeightUI = prefabFactory.CreateSlider(_clipWeightJSON);
            clipWeightUI.valueFormat = "F4";
        }

        private void InitRenameLayer()
        {
            _layerNameJSON = new JSONStorableString("Layer name (share targets)", "", UpdateLayerName);
            prefabFactory.CreateTextInput(_layerNameJSON);
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
            _animationNameJSON = new JSONStorableString("Animation name (group with 'group/anim')", "", UpdateAnimationName);
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
            if (animation.index.ByLayer(current.animationLayer).Any(c => c.animationName == val))
            {
                _animationNameJSON.valNoCallback = current.animationName;
                return;
            }
            current.animationName = val;
            var existing = animation.clips.FirstOrDefault(c => c != current && c.animationName == current.animationName);
            if (existing?.nextAnimationName != null)
            {
                var next = animation.index.ByLayer(current.animationLayer).FirstOrDefault(c => c.animationName == existing.nextAnimationName);
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
                _changeLengthModeCropExtendEnd,
                _changeLengthModeCropExtendBegin,
                _changeLengthModeCropExtendAtTime,
                _changeLengthModeStretch,
                _changeLengthModeLoop
             }, _changeLengthModeCropExtendEnd, "Length mode");
            var lengthModeUI = prefabFactory.CreatePopup(_lengthModeJSON, false, true);
            lengthModeUI.popupPanelHeight = 350f;

            _lengthJSON = new JSONStorableFloat(
                "Change length to (s)",
                AtomAnimationClip.DefaultAnimationLength,
                val =>
                {
                    _lengthJSON.valNoCallback = val.Snap(animationEditContext.snap);
                    if (_lengthJSON.valNoCallback < 0.1f)
                        _lengthJSON.valNoCallback = 0.1f;
                    if (_lengthModeJSON.val == _changeLengthModeCropExtendAtTime && _lengthJSON.valNoCallback < animationEditContext.clipTime + animationEditContext.snap)
                        _lengthJSON.valNoCallback = animationEditContext.clipTime + animationEditContext.snap;
                    _applyLengthUI.button.interactable = !_lengthJSON.val.IsSameFrame(current.animationLength);
                },
                0f,
                Mathf.Max((current.animationLength * 5f).Snap(10f), 10f),
                false);
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
            _ensureQuaternionContinuity = new JSONStorableBool("Ensure Quaternion Continuity", true, SetEnsureQuaternionContinuity);
            prefabFactory.CreateToggle(_ensureQuaternionContinuity);
        }

        private void InitAnimationPatternLinkUI()
        {
            _linkedAnimationPatternJSON = new JSONStorableStringChooser("Link", new[] { "" }.Concat(SuperController.singleton.GetAtoms().Where(a => a.type == "AnimationPattern").Select(a => a.uid)).ToList(), "", "Link", LinkAnimationPattern)
            {
                isStorable = false
            };
            var linkedAnimationPatternUI = prefabFactory.CreatePopup(_linkedAnimationPatternJSON, true, true);
            linkedAnimationPatternUI.popupPanelHeight = 240f;
            linkedAnimationPatternUI.popup.onOpenPopupHandlers += () => _linkedAnimationPatternJSON.choices = new[] { "" }.Concat(SuperController.singleton.GetAtoms().Where(a => a.type == "AnimationPattern").Select(a => a.uid)).ToList();
        }

        private void InitLoopUI()
        {
            _loop = new JSONStorableBool("Loop", current?.loop ?? true, val =>
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
                case _changeLengthModeStretch:
                    operations.Resize().Stretch(current, newLength);
                    break;
                case _changeLengthModeCropExtendEnd:
                    operations.Resize().CropOrExtendEnd(current, newLength);
                    break;
                case _changeLengthModeCropExtendBegin:
                    operations.Resize().CropOrExtendAt(current, newLength, 0f);
                    break;
                case _changeLengthModeCropExtendAtTime:
                    if (_lengthJSON.valNoCallback < animationEditContext.clipTime + animationEditContext.snap)
                        _lengthJSON.valNoCallback = animationEditContext.clipTime + animationEditContext.snap;
                    operations.Resize().CropOrExtendAt(current, newLength, time);
                    break;
                case _changeLengthModeLoop:
                    operations.Resize().Loop(current, newLength);
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

