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
        private JSONStorableBool _preserveLastFrame;
        private JSONStorableFloat _loopSelfBlend;
        private JSONStorableStringChooser _linkedAnimationPatternJSON;
        private JSONStorableStringChooser _linkedAudioSourceJSON;
        private JSONStorableString _segmentNameJSON;
        private JSONStorableString _layerNameJSON;
        private JSONStorableString _animationNameJSON;
        private UIDynamicToggle _loopUI;
        private UIDynamicToggle _preserveLastFrameUI;
        private UIDynamicSlider _loopSelfBlendUI;
        private JSONStorableFloat _globalSpeedJSON;
        private JSONStorableFloat _localSpeedJSON;
        private JSONStorableFloat _globalWeightJSON;
        private JSONStorableFloat _localWeightJSON;
        private UIDynamicButton _applyLengthUI;

        #region Init

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            InitRenameSegment();
            InitRenameLayer();
            InitRenameAnimation();

            prefabFactory.CreateHeader("Speed", 1);
            InitSpeedUI();

            prefabFactory.CreateHeader("Options", 1);
            InitLoopUI();
            InitSelfBlendUI();

            prefabFactory.CreateHeader("Length", 1);
            InitAnimationLengthUI();

            prefabFactory.CreateHeader("Advanced", 1);
            InitWeightUI();
            InitAudioSourceLinkUI();
            InitEnsureQuaternionContinuityUI();
            InitAnimationPatternLinkUI();

            current.onAnimationSettingsChanged.AddListener(OnAnimationSettingsChanged);
            current.onPlaybackSettingsChanged.AddListener(OnPlaybackSettingsChanged);
            animation.onSpeedChanged.AddListener(OnSpeedChanged);
            animation.onWeightChanged.AddListener(OnWeightChanged);
            OnSpeedChanged();
            OnPlaybackSettingsChanged();
            OnAnimationSettingsChanged();
        }

        private void InitSpeedUI()
        {
            _globalSpeedJSON = new JSONStorableFloat("Speed (Global)", 1f, val => animation.globalSpeed = val, -1f, 5f, false)
            {
                valNoCallback = animation.globalSpeed
            };
            var globalSpeedUI = prefabFactory.CreateSlider(_globalSpeedJSON);
            globalSpeedUI.valueFormat = "F3";

            _localSpeedJSON = new JSONStorableFloat("Speed (This)", 1f, val => { foreach (var clip in animation.index.ByName(current.animationSegment, current.animationName)) { clip.speed = val; } }, -1f, 5f, false)
            {
                valNoCallback = current.speed
            };
            var localSpeedUI = prefabFactory.CreateSlider(_localSpeedJSON);
            localSpeedUI.valueFormat = "F3";
        }

        private void InitWeightUI()
        {
            _globalWeightJSON = new JSONStorableFloat("Weight (Global)", 1f, val => animation.globalWeight = val, 0f, 1f)
            {
                valNoCallback = animation.globalWeight
            };
            var globalWeightUI = prefabFactory.CreateSlider(_globalWeightJSON);
            globalWeightUI.valueFormat = "F4";

            _localWeightJSON = new JSONStorableFloat("Weight (This)", 1f, val => current.weight = val, 0f, 1f)
            {
                valNoCallback = current.weight
            };
            var localWeightUI = prefabFactory.CreateSlider(_localWeightJSON);
            localWeightUI.valueFormat = "F4";
        }

        private void InitRenameSegment()
        {
            if (animation.index.useSegment)
            {
                _segmentNameJSON = new JSONStorableString("Segment name", "", UpdateSegmentName);
                prefabFactory.CreateTextInput(_segmentNameJSON);
                _segmentNameJSON.valNoCallback = current.animationSegment;
            }
            else
            {
                prefabFactory.CreateNote("Segment name: Segment feature not used");
            }
        }

        private void UpdateSegmentName(string to)
        {
            var from = current.animationSegment;
            if (from == AtomAnimationClip.SharedAnimationSegment)
            {
                _segmentNameJSON.valNoCallback = current.animationSegment;
                return;
            }
            to = to.Trim();
            if (to == "" || to == AtomAnimationClip.SharedAnimationSegment || to == current.animationSegment)
            {
                _segmentNameJSON.valNoCallback = current.animationSegment;
                return;
            }

            if (animationEditContext.animation.index.segmentNames.Any(l => l == to))
            {
                _segmentNameJSON.valNoCallback = current.animationSegment;
                return;
            }

            foreach (var clip in animationEditContext.currentSegment.layers.SelectMany(c => c))
            {
                clip.animationSegment = to;
            }

            foreach (var clip in animation.clips.Where(c => c.nextAnimationName != null && c.nextAnimationName == $"{AtomAnimationClip.NextAnimationSegmentPrefix}{from}"))
            {
                clip.nextAnimationName = $"{AtomAnimationClip.NextAnimationSegmentPrefix}{to}";
            }

            if (animation.playingAnimationSegment == from)
            {
                animation.playingAnimationSegment = to;
            }

            animation.index.Rebuild();
        }

        private void InitRenameLayer()
        {
            if (animation.index.clipsGroupedByLayer.Count > 1)
            {
                _layerNameJSON = new JSONStorableString("Layer name (specifies targets)", "", UpdateLayerName);
                prefabFactory.CreateTextInput(_layerNameJSON);
                _layerNameJSON.valNoCallback = current.animationLayer;
            }
            else
            {
                prefabFactory.CreateNote("Layer name: Layers feature not used");
            }
        }

        private void UpdateLayerName(string to)
        {
            to = to.Trim();
            if (to == "" || to == current.animationLayer)
            {
                _layerNameJSON.valNoCallback = current.animationLayer;
                return;
            }

            if (animationEditContext.currentSegment.layerNames.Any(l => l == to))
            {
                _layerNameJSON.valNoCallback = current.animationLayer;
                return;
            }

            foreach (var clip in animationEditContext.currentLayer)
            {
                clip.animationLayer = to;
            }

            animation.index.Rebuild();
        }

        private void InitRenameAnimation()
        {
            _animationNameJSON = new JSONStorableString("Animation name (group with 'group/anim')", "", UpdateAnimationName);
            prefabFactory.CreateTextInput(_animationNameJSON);
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

            val = val.Trim();

            if (!(current.isOnSharedSegment
                    ? animation.index.segmentsById.Where(kvp => kvp.Key != AtomAnimationClip.SharedAnimationSegmentId)
                        .SelectMany(l => l.Value.allClips)
                        .All(c => c.animationName != val)
                    : animation.index.ByName(AtomAnimationClip.SharedAnimationSegment, val).Count == 0))
            {
                _animationNameJSON.valNoCallback = current.animationName;
                return;
            }

            if (animationEditContext.currentLayer.Any(c => c.animationName == val))
            {
                _animationNameJSON.valNoCallback = current.animationName;
                return;
            }

            current.animationName = val;
            foreach (var other in currentLayer.Where(c => c.nextAnimationName == previousAnimationName))
            {
                other.nextAnimationName = val;
            }
            animation.index.Rebuild();
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
            prefabFactory.CreatePopup(_lengthModeJSON, false, true, 350f);

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
            _ensureQuaternionContinuity = new JSONStorableBool("Ensure quaternion continuity", true, SetEnsureQuaternionContinuity);
            prefabFactory.CreateToggle(_ensureQuaternionContinuity);
        }

        private void InitAudioSourceLinkUI()
        {
            var choicesList = GetEligibleAudioSourceAtoms();
            _linkedAudioSourceJSON = new JSONStorableStringChooser("Audio Link", choicesList, "", "Audio Link", LinkAudioSource)
            {
                isStorable = false
            };
            var linkedAnimationPatternUI = prefabFactory.CreatePopup(_linkedAudioSourceJSON, true, true, 240f, true, 110f);
            linkedAnimationPatternUI.popup.onOpenPopupHandlers += () => GetEligibleAudioSourceAtoms();
        }

        private static List<string> GetEligibleAudioSourceAtoms()
        {
            return new[] { "" }.Concat(SuperController.singleton
                    .GetAtoms()
                    .Where(a => a.GetStorableIDs().Select(a.GetStorableByID).OfType<AudioSourceControl>().Any())
                    .Select(a => a.uid))
                .ToList();
        }

        private void InitAnimationPatternLinkUI()
        {
            _linkedAnimationPatternJSON = new JSONStorableStringChooser("AnimPat Lnk", GetEligibleAnimationPatternAtoms(), "", "AnimPatLnk", LinkAnimationPattern)
            {
                isStorable = false
            };
            var linkedAnimationPatternUI = prefabFactory.CreatePopup(_linkedAnimationPatternJSON, true, true, 240f, true, 112f);
            linkedAnimationPatternUI.popup.onOpenPopupHandlers += () => _linkedAnimationPatternJSON.choices = GetEligibleAnimationPatternAtoms();
        }

        private static List<string> GetEligibleAnimationPatternAtoms()
        {
            return new[] { "" }.Concat(SuperController.singleton.GetAtoms().Where(a => a.type == "AnimationPattern").Select(a => a.uid)).ToList();
        }

        private void InitLoopUI()
        {
            _loop = new JSONStorableBool("Loop", current?.loop ?? true, val =>
            {
                current.loop = val;
                #warning To merge into a drop down with loop
                current.loopPreserveLastFrame = false;
            });
            _loopUI = prefabFactory.CreateToggle(_loop);
        }

        private void InitSelfBlendUI()
        {
            #warning To merge into a drop down with loop
            _preserveLastFrame = new JSONStorableBool("Preserve Last Frame", current?.loopPreserveLastFrame ?? false, val =>
            {
                current.loopPreserveLastFrame = val;
            });
            _preserveLastFrameUI = prefabFactory.CreateToggle(_preserveLastFrame);

            _loopSelfBlend = new JSONStorableFloat("Self Blend", current?.loopBlendSelfDuration ?? 0f, val =>
            {
                current.loopBlendSelfDuration = val;
            }, 0f, 5f, false);
            _loopSelfBlendUI = prefabFactory.CreateSlider(_loopSelfBlend);
            _loopSelfBlendUI.valueFormat = "F3";
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

        private void LinkAudioSource(string uid)
        {
            if (string.IsNullOrEmpty(uid))
            {
                current.audioSourceControl = null;
                return;
            }

            var atom = SuperController.singleton.GetAtomByUid(uid);
            if (atom == null)
            {
                SuperController.LogError($"Timeline: Could not find Atom '{uid}'");
                return;
            }

            current.audioSourceControl = atom.GetStorableIDs().Select(atom.GetStorableByID).OfType<AudioSourceControl>().FirstOrDefault();
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
            animationPattern.SetFloatParamValue("speed", animation.globalSpeed);
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
            OnAnimationSettingsChanged();
        }

        private void OnAnimationSettingsChanged(string _)
        {
            OnAnimationSettingsChanged();
        }

        private void OnPlaybackSettingsChanged()
        {
            _localWeightJSON.valNoCallback = current.weight;
            _localSpeedJSON.valNoCallback = current.speed;
        }

        private void OnSpeedChanged()
        {
            _globalSpeedJSON.valNoCallback = animation.globalSpeed;
        }

        private void OnWeightChanged()
        {
            _globalWeightJSON.valNoCallback = animation.globalWeight;
        }

        private void OnAnimationSettingsChanged()
        {
            _animationNameJSON.valNoCallback = current.animationName;
            if (_layerNameJSON != null)
                _layerNameJSON.valNoCallback = current.animationLayer;
            if (_segmentNameJSON != null)
                _segmentNameJSON.valNoCallback = current.animationSegment;
            _lengthJSON.valNoCallback = current.animationLength;
            _lengthJSON.max = Mathf.Max((current.animationLength * 5f).Snap(10f), 10f);
            _loop.valNoCallback = current.loop;
            _loopUI.toggle.interactable = !current.autoTransitionNext;
            _preserveLastFrame.valNoCallback = current.loopPreserveLastFrame;
            _preserveLastFrameUI.toggle.interactable = current.loop;
            _loopSelfBlend.valNoCallback = current.loopBlendSelfDuration;
            _loopSelfBlendUI.slider.interactable = current.loop;
            _ensureQuaternionContinuity.valNoCallback = current.ensureQuaternionContinuity;
            _linkedAudioSourceJSON.valNoCallback = current.audioSourceControl != null ? current.audioSourceControl.containingAtom.uid : "";
            _linkedAnimationPatternJSON.valNoCallback = current.animationPattern != null ? current.animationPattern.containingAtom.uid : "";
        }

        public override void OnDestroy()
        {
            animation.onSpeedChanged.RemoveListener(OnSpeedChanged);
            animation.onWeightChanged.RemoveListener(OnWeightChanged);
            current.onAnimationSettingsChanged.RemoveListener(OnAnimationSettingsChanged);
            current.onPlaybackSettingsChanged.RemoveListener(OnPlaybackSettingsChanged);
            base.OnDestroy();
        }

        #endregion
    }
}

