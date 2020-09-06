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
        public const string NoNextAnimation = "[None]";

        public override string screenId => ScreenName;

        private JSONStorableStringChooser _lengthModeJSON;
        private JSONStorableFloat _lengthJSON;
        private JSONStorableBool _ensureQuaternionContinuity;
        private JSONStorableBool _loop;
        private JSONStorableBool _autoPlayJSON;
        private JSONStorableStringChooser _linkedAnimationPatternJSON;
        private JSONStorableString _layerNameJSON;
        private JSONStorableString _animationNameJSON;
        private JSONStorableFloat _blendDurationJSON;
        private JSONStorableStringChooser _nextAnimationJSON;
        private JSONStorableFloat _nextAnimationTimeJSON;
        private JSONStorableString _nextAnimationPreviewJSON;
        private JSONStorableBool _transitionPreviousJSON;
        private JSONStorableBool _transitionNextJSON;
        private JSONStorableBool _transitionSyncTime;
        private UIDynamicToggle _loopUI;
        private JSONStorableFloat _animationSpeedJSON;
        private JSONStorableFloat _clipSpeedJSON;
        private JSONStorableFloat _clipWeightJSON;

        public EditAnimationScreen()
            : base()
        {
        }

        #region Init

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            CreateHeader("Playback", 1);
            InitPlaybackUI();

            CreateHeader("Name", 1);
            InitRenameLayer();
            InitRenameAnimation();

            CreateHeader("Options", 1);
            InitLoopUI();
            InitEnsureQuaternionContinuityUI();
            InitAutoPlayUI();

            CreateHeader("Length", 1);
            InitAnimationLengthUI();

            CreateHeader("Sequencing", 1);
            InitSequenceUI();
            InitTransitionUI();
            InitPreviewUI();

            CreateHeader("Animation Pattern Link", 1);
            InitAnimationPatternLinkUI();

            // To allow selecting in the popup
            prefabFactory.CreateSpacer().height = 200f;

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

            _clipSpeedJSON = new JSONStorableFloat("Speed (Local)", 1f, (float val) => current.speed = val, -1f, 5f, false)
            {
                valNoCallback = current.speed
            };
            var clipSpeedUI = prefabFactory.CreateSlider(_clipSpeedJSON);
            clipSpeedUI.valueFormat = "F3";

            _clipWeightJSON = new JSONStorableFloat("Weight", 1f, (float val) => current.weight = val, 0f, 1f, true)
            {
                valNoCallback = current.weight
            };
            var clipWeigthUI = prefabFactory.CreateSlider(_clipWeightJSON);
            clipWeigthUI.valueFormat = "F4";
        }

        private void InitRenameLayer()
        {
            _layerNameJSON = new JSONStorableString("Layer Name", "", (string val) => UpdateLayerName(val));
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
            _animationNameJSON = new JSONStorableString("Animation Name", "", (string val) => UpdateAnimationName(val));
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
            if (animation.clips.Any(c => c.animationName == val))
            {
                _animationNameJSON.valNoCallback = current.animationName;
                return;
            }
            current.animationName = val;
            foreach (var other in animation.clips)
            {
                if (other.nextAnimationName == previousAnimationName)
                    other.nextAnimationName = val;
            }
        }

        private void InitAnimationLengthUI()
        {
            UIDynamicButton applyLengthUI = null;

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
                    _lengthJSON.valNoCallback = val.Snap(animation.snap);
                    if (_lengthJSON.valNoCallback < 0.1f)
                        _lengthJSON.valNoCallback = 0.1f;
                },
                0f,
                Mathf.Max((current.animationLength * 5f).Snap(10f), 10f),
                false,
                true);
            var lengthUI = prefabFactory.CreateSlider(_lengthJSON);
            lengthUI.valueFormat = "F3";

            applyLengthUI = prefabFactory.CreateButton("Apply");
            applyLengthUI.button.onClick.AddListener(() =>
            {
                UpdateAnimationLength(_lengthJSON.val);
            });
        }

        private void InitEnsureQuaternionContinuityUI()
        {
            _ensureQuaternionContinuity = new JSONStorableBool("Ensure Quaternion Continuity", true, (bool val) => SetEnsureQuaternionContinuity(val));
            var ensureQuaternionContinuityUI = prefabFactory.CreateToggle(_ensureQuaternionContinuity);
        }

        private void InitAutoPlayUI()
        {
            _autoPlayJSON = new JSONStorableBool("Auto Play On Load", false, (bool val) =>
            {
                foreach (var c in animation.clips.Where(c => c != current && c.animationLayer == current.animationLayer))
                    c.autoPlay = false;
                current.autoPlay = val;
            })
            {
                isStorable = false
            };
            var autoPlayUI = prefabFactory.CreateToggle(_autoPlayJSON);
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

        private void InitSequenceUI()
        {
            _nextAnimationJSON = new JSONStorableStringChooser("Play next", GetEligibleNextAnimations(), "", "Play next", (string val) => ChangeNextAnimation(val));
            var nextAnimationUI = prefabFactory.CreatePopup(_nextAnimationJSON, true, true);
            nextAnimationUI.popupPanelHeight = 360f;

            _nextAnimationTimeJSON = new JSONStorableFloat("Play after seconds", 0f, (float val) => SetNextAnimationTime(val), 0f, 60f, false)
            {
                valNoCallback = current.nextAnimationTime
            };
            var nextAnimationTimeUI = prefabFactory.CreateSlider(_nextAnimationTimeJSON);
            nextAnimationTimeUI.valueFormat = "F3";

            _blendDurationJSON = new JSONStorableFloat("Blend duration", AtomAnimationClip.DefaultBlendDuration, v => UpdateBlendDuration(v), 0f, 5f, false);
            var blendDurationUI = prefabFactory.CreateSlider(_blendDurationJSON);
            blendDurationUI.valueFormat = "F3";
        }

        private void InitPreviewUI()
        {
            _nextAnimationPreviewJSON = new JSONStorableString("Next Preview", "");
            var nextAnimationResultUI = prefabFactory.CreateTextField(_nextAnimationPreviewJSON);
            nextAnimationResultUI.height = 50f;
        }

        private void InitTransitionUI()
        {
            _transitionPreviousJSON = new JSONStorableBool("Transition (Previous)", false, (bool val) => ChangeTransitionPrevious(val));
            prefabFactory.CreateToggle(_transitionPreviousJSON);

            _transitionNextJSON = new JSONStorableBool("Transition (Next)", false, (bool val) => ChangeTransitionNext(val));
            prefabFactory.CreateToggle(_transitionNextJSON);

            _transitionSyncTime = new JSONStorableBool("Transition (Sync Time)", true, (bool val) => current.syncTransitionTime = val);
            prefabFactory.CreateToggle(_transitionSyncTime);
        }

        private void InitLoopUI()
        {
            _loop = new JSONStorableBool("Loop", current?.loop ?? true, (bool val) =>
            {
                current.loop = val;
                UpdateNextAnimationPreview();
                RefreshTransitionUI();
            });
            _loopUI = prefabFactory.CreateToggle(_loop);
        }

        private void RefreshTransitionUI()
        {
            _transitionPreviousJSON.toggle.interactable = true;
            _transitionNextJSON.toggle.interactable = true;
            _loopUI.toggle.interactable = false;

            if (!current.autoTransitionPrevious)
            {
                var clipsPointingToHere = animation.clips.Where(c => c != current && c.nextAnimationName == current.animationName).ToList();
                if (clipsPointingToHere.Count == 0 || clipsPointingToHere.Any(c => c.autoTransitionNext))
                {
                    _transitionPreviousJSON.toggle.interactable = false;
                }
            }

            if (!current.autoTransitionNext)
            {
                if (current.loop)
                {
                    _transitionNextJSON.toggle.interactable = false;
                    _loopUI.toggle.interactable = true;
                }
                else
                {
                    var targetClip = animation.clips.FirstOrDefault(c => c != current && c.animationName == current.nextAnimationName);
                    if (targetClip == null || targetClip.autoTransitionNext == true)
                    {
                        _transitionNextJSON.toggle.interactable = false;
                        _loopUI.toggle.interactable = true;
                    }
                }
            }
        }

        private void UpdateNextAnimationPreview()
        {
            if (current.nextAnimationName == null)
            {
                _nextAnimationPreviewJSON.val = "No next animation configured";
                return;
            }

            if (!current.loop)
            {
                _nextAnimationPreviewJSON.val = $"Will play once and blend at {current.nextAnimationTime}s";
                return;
            }

            if (_nextAnimationTimeJSON.val.IsSameFrame(0))
            {
                _nextAnimationPreviewJSON.val = "Will loop indefinitely";
            }
            else
            {
                _nextAnimationPreviewJSON.val = $"Will loop {Math.Round((current.nextAnimationTime + current.blendDuration) / current.animationLength, 2)} times including blending";
            }
        }

        private List<string> GetEligibleNextAnimations()
        {
            var animations = animation.clips
                .Where(c => c.animationLayer == current.animationLayer)
                .Where(c => c.animationName != current.animationName)
                .Select(c => c.animationName)
                .GroupBy(x =>
                {
                    var i = x.IndexOf("/");
                    if (i == -1) return null;
                    return x.Substring(0, i);
                });
            return new[] { NoNextAnimation }
                .Concat(animations.SelectMany(EnumerateAnimations))
                .Concat(new[] { AtomAnimation.RandomizeAnimationName })
                .ToList();
        }

        private IEnumerable<string> EnumerateAnimations(IGrouping<string, string> group)
        {
            foreach (var name in group)
                yield return name;

            if (group.Key != null)
                yield return group.Key + AtomAnimation.RandomizeGroupSuffix;
        }

        #endregion

        #region Callbacks

        private void UpdateAnimationLength(float newLength)
        {
            if (animation.isPlaying)
            {
                _lengthJSON.valNoCallback = current.animationLength;
                return;
            }

            newLength = newLength.Snap(animation.snap);
            if (newLength < 0.1f) newLength = 0.1f;
            var time = animation.clipTime.Snap();

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

            animation.clipTime = Math.Min(time, newLength);
        }

        private void SetEnsureQuaternionContinuity(bool val)
        {
            current.ensureQuaternionContinuity = val;
        }

        private void UpdateBlendDuration(float v)
        {
            if (v < 0)
                _blendDurationJSON.valNoCallback = v = 0f;
            v = v.Snap();
            if (!current.loop && v >= (current.animationLength - 0.001f))
                _blendDurationJSON.valNoCallback = v = (current.animationLength - 0.001f).Snap();
            current.blendDuration = v;
        }

        private void ChangeTransitionPrevious(bool val)
        {
            current.autoTransitionPrevious = val;
            RefreshTransitionUI();
            plugin.animation.Sample();
        }

        private void ChangeTransitionNext(bool val)
        {
            current.autoTransitionNext = val;
            RefreshTransitionUI();
            plugin.animation.Sample();
        }

        private void ChangeNextAnimation(string val)
        {
            if (val == NoNextAnimation) val = "";
            current.nextAnimationName = val;
            SetNextAnimationTime(
                current.nextAnimationTime == 0
                ? current.nextAnimationTime = current.animationLength - current.blendDuration
                : current.nextAnimationTime
            );
            RefreshTransitionUI();
        }

        private void SetNextAnimationTime(float nextTime)
        {
            if (current.nextAnimationName == null)
            {
                _nextAnimationTimeJSON.valNoCallback = 0f;
                current.nextAnimationTime = 0f;
                return;
            }
            else if (!current.loop)
            {
                nextTime = (current.animationLength - current.blendDuration).Snap();
                current.nextAnimationTime = nextTime;
                _nextAnimationTimeJSON.valNoCallback = nextTime;
                return;
            }

            nextTime = nextTime.Snap();

            _nextAnimationTimeJSON.valNoCallback = nextTime;
            current.nextAnimationTime = nextTime;
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

        protected override void OnCurrentAnimationChanged(AtomAnimation.CurrentAnimationChangedEventArgs args)
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
            _ensureQuaternionContinuity.valNoCallback = current.ensureQuaternionContinuity;
            _autoPlayJSON.valNoCallback = current.autoPlay;
            _linkedAnimationPatternJSON.valNoCallback = current.animationPattern?.containingAtom.uid ?? "";
            _blendDurationJSON.valNoCallback = current.blendDuration;
            _transitionPreviousJSON.valNoCallback = current.autoTransitionPrevious;
            _transitionNextJSON.valNoCallback = current.autoTransitionNext;
            _transitionSyncTime.valNoCallback = current.syncTransitionTime;
            _nextAnimationJSON.valNoCallback = string.IsNullOrEmpty(current.nextAnimationName) ? NoNextAnimation : current.nextAnimationName;
            _nextAnimationJSON.choices = GetEligibleNextAnimations();
            _nextAnimationTimeJSON.valNoCallback = current.nextAnimationTime;
            RefreshTransitionUI();
            UpdateNextAnimationPreview();
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

