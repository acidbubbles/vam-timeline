using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class SequencingScreen : ScreenBase
    {
        public const string ScreenName = "Sequence";
        private const string _noNextAnimation = "[None]";
        private const string _animationSetAuto = "[Sync with other layers by name]";

        public override string screenId => ScreenName;

        private JSONStorableBool _masterJSON;
        private JSONStorableBool _autoPlayJSON;
        private JSONStorableBool _loop;
        private UIDynamicToggle _loopUI;
        private JSONStorableBool _uninterruptible;
        private JSONStorableFloat _blendDurationJSON;
        private JSONStorableStringChooser _nextAnimationJSON;
        private JSONStorableFloat _nextAnimationTimeJSON;
        private JSONStorableFloat _nextAnimationRandomizeWeightJSON;
        private JSONStorableString _nextAnimationPreviewJSON;
        private JSONStorableString _animationSetJSON;
        private JSONStorableBool _transitionPreviousJSON;
        private JSONStorableBool _transitionNextJSON;
        private JSONStorableBool _applyPoseOnTransition;
        private JSONStorableBool _preserveLoopsJSON;
        private UIDynamicToggle _preserveLoopsUI;
        private JSONStorableFloat _randomizeRangeJSON;
        private JSONStorableBool _fadeOnTransition;

        #region Init

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            prefabFactory.CreateHeader("Options", 1);
            InitSequenceMasterUI();
            InitAutoPlayUI();
            InitBlendUI();
            InitPreserveLoopsUI();
            InitUninterruptibleUI();

            prefabFactory.CreateHeader("Sequencing", 1);
            InitSequenceUI();
            RandomizeWeightUI();
            InitRandomizeLengthUI();
            InitPreviewUI();

            prefabFactory.CreateHeader("Sync", 1);
            InitAnimationSetUI();

            prefabFactory.CreateHeader("Transition (auto keyframes)", 1);
            InitLoopUI();
            InitTransitionUI();
            if (plugin.containingAtom.type == "Person")
                InitPoseUI();
            InitFadeUI();

            prefabFactory.CreateHeader("Fading (VAMOverlays)", 1);
            InitOverlaysUI();

            current.onAnimationSettingsChanged.AddListener(OnAnimationSettingsChanged);

            UpdateValues();
        }

        private void InitSequenceMasterUI()
        {
            _masterJSON = new JSONStorableBool("Master (atom controls others)", false, val =>
            {
                animation.master = val;
            })
            {
                isStorable = false
            };
            prefabFactory.CreateToggle(_masterJSON);
        }

        private void InitAutoPlayUI()
        {
            _autoPlayJSON = new JSONStorableBool("Auto play on load", false, val =>
            {
                foreach (var c in currentLayer.Where(c => c != current))
                    c.autoPlay = false;
                current.autoPlay = val;
            })
            {
                isStorable = false
            };
            prefabFactory.CreateToggle(_autoPlayJSON);
        }

        private void InitSequenceUI()
        {
            _nextAnimationJSON = new JSONStorableStringChooser("Play next", GetEligibleNextAnimations(), "", "Play next", (string val) => SyncPlayNext());
            prefabFactory.CreatePopup(_nextAnimationJSON, true, true, 360f);

            _nextAnimationTimeJSON = new JSONStorableFloat("Play next in (seconds)", 0f, (float val) => SyncPlayNext(), 0f, 60f, false)
            {
                valNoCallback = current.nextAnimationTime
            };
            var nextAnimationTimeUI = prefabFactory.CreateSlider(_nextAnimationTimeJSON);
            nextAnimationTimeUI.valueFormat = "F3";
        }

        private void RandomizeWeightUI()
        {
            _nextAnimationRandomizeWeightJSON = new JSONStorableFloat("Random group weight", 1f, val => current.nextAnimationRandomizeWeight = val, 0f, 1f, false);
            prefabFactory.CreateSlider(_nextAnimationRandomizeWeightJSON);
        }

        private void InitUninterruptibleUI()
        {
            _uninterruptible = new JSONStorableBool("Prevent trigger interruptions", current.uninterruptible, val =>
            {
                foreach (var clip in animation.GetClips(current.animationName))
                    clip.uninterruptible = val;
            });
            prefabFactory.CreateToggle(_uninterruptible);
        }

        private void InitBlendUI()
        {
            _blendDurationJSON = new JSONStorableFloat("Blend-in duration", AtomAnimationClip.DefaultBlendDuration, UpdateBlendDuration, 0f, 5f, false);
            var blendDurationUI = prefabFactory.CreateSlider(_blendDurationJSON);
            blendDurationUI.valueFormat = "F3";
        }

        private void InitPreserveLoopsUI()
        {
            _preserveLoopsJSON = new JSONStorableBool("Preserve loops", true, val =>
            {
                current.preserveLoops = val;
                RoundNextTimeToNearestLoop();
            });
            _preserveLoopsUI = prefabFactory.CreateToggle(_preserveLoopsJSON);
        }

        private void RoundNextTimeToNearestLoop()
        {
            if (current.loop && current.preserveLoops)
            {
                _nextAnimationTimeJSON.valNoCallback = _nextAnimationTimeJSON.val.RoundToNearest(current.animationLength);
            }
            _nextAnimationTimeJSON.valNoCallback = _nextAnimationTimeJSON.val.Snap();
        }

        private void InitRandomizeLengthUI()
        {
            _randomizeRangeJSON = new JSONStorableFloat("Add random time range", 0f, (float _) => SyncPlayNext(), 0f, 60f, false)
            {
                valNoCallback = current.nextAnimationTimeRandomize
            };
            var randomizeRangeUI = prefabFactory.CreateSlider(_randomizeRangeJSON);
            randomizeRangeUI.valueFormat = "F3";
        }

        private void InitPreviewUI()
        {
            _nextAnimationPreviewJSON = new JSONStorableString("Next preview", "");
            var nextAnimationResultUI = prefabFactory.CreateTextField(_nextAnimationPreviewJSON);
            nextAnimationResultUI.height = 50f;
        }

        private void InitAnimationSetUI()
        {
            _animationSetJSON = new JSONStorableString("Anim set (sync across layers)", "", val => current.animationSet = val != _animationSetAuto && !string.IsNullOrWhiteSpace(val) ? val : null);
            prefabFactory.CreateTextInput(_animationSetJSON);
        }

        private void InitLoopUI()
        {
            _loop = new JSONStorableBool("Loop", current?.loop ?? true, val =>
            {
                current.loop = val;
                UpdateNextAnimationPreview();
                RefreshTransitionUI();
            });
            _loopUI = prefabFactory.CreateToggle(_loop);
        }

        private void InitTransitionUI()
        {
            _transitionPreviousJSON = new JSONStorableBool("Sync first frame with previous", false, ChangeTransitionPrevious);
            prefabFactory.CreateToggle(_transitionPreviousJSON);

            _transitionNextJSON = new JSONStorableBool("Sync last frame with next", false, ChangeTransitionNext);
            prefabFactory.CreateToggle(_transitionNextJSON);
        }

        private void InitPoseUI()
        {
            _applyPoseOnTransition = new JSONStorableBool("Apply pose on transition", false, v => current.applyPoseOnTransition = v);
            prefabFactory.CreateToggle(_applyPoseOnTransition);
        }

        private void InitFadeUI()
        {
            _fadeOnTransition = new JSONStorableBool("Fade on transitions", false, val => current.fadeOnTransition = val);
            var fadeOnTransitionToggle = prefabFactory.CreateToggle(_fadeOnTransition);
            fadeOnTransitionToggle.toggle.interactable = animation.fadeManager != null;
        }

        [SuppressMessage("ReSharper", "AccessToModifiedClosure")]
        private void InitOverlaysUI()
        {
            UIDynamicSlider blackTimeSlider = null;
            var blackTime = new JSONStorableFloat("Black time (global)", 0.5f, val =>
            {
                if (animation.fadeManager != null)
                    animation.fadeManager.blackTime = val;
            }, 0f, 2f, false)
            {
                valNoCallback = animation.fadeManager?.blackTime ?? 0.5f
            };
            var atomSelector = new JSONStorableStringChooser("Overlays (global)", new List<string>(), "", "Overlays", val =>
            {
                if (string.IsNullOrEmpty(val))
                {
                    animation.fadeManager = null;
                    if (blackTimeSlider != null)
                        blackTimeSlider.gameObject.SetActive(false);
                    if (_fadeOnTransition != null)
                        _fadeOnTransition.toggle.interactable = false;
                    return;
                }
                animation.fadeManager = VamOverlaysFadeManager.FromAtomUid(val, blackTime.val);
                if (blackTimeSlider != null)
                    blackTimeSlider.gameObject.SetActive(true);
                if (_fadeOnTransition != null)
                    _fadeOnTransition.toggle.interactable = true;
                if(!animation.fadeManager.TryConnectNow())
                    SuperController.LogError($"Timeline: Could not find VAMOverlays on atom '{val}'");
            })
            {
                valNoCallback = animation.fadeManager?.GetAtomUid()
            };
            prefabFactory.CreatePopup(atomSelector, false, false, 350f, true);
            atomSelector.popupOpenCallback = () =>
            {
                atomSelector.choices = new List<string> { "" }.Concat(
                    SuperController.singleton.GetAtoms()
                        .Where(a => a.type == "Empty")
                        .Where(a => a.GetStorableIDs().Select(a.GetStorableByID).Any(s => s.IsAction("Start Fade In")))
                        .Select(a => a.uid)
                ).ToList();
            };
            blackTimeSlider = prefabFactory.CreateSlider(blackTime);
            blackTimeSlider.gameObject.SetActive(animation.fadeManager != null);
        }

        private void RefreshTransitionUI()
        {
            _transitionPreviousJSON.toggle.interactable = true;
            _transitionNextJSON.toggle.interactable = true;
            _loopUI.toggle.interactable = false;
            _preserveLoopsUI.toggle.interactable = current.loop;
            _fadeOnTransition.valNoCallback = current.fadeOnTransition;

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
                _loopUI.toggle.interactable = true;

                if (current.loop)
                {
                    _transitionNextJSON.toggle.interactable = false;
                }
                else
                {
                    var targetClip = animation.clips.FirstOrDefault(c => c != current && c.animationName == current.nextAnimationName);
                    if (targetClip == null || targetClip.autoTransitionNext)
                    {
                        _transitionNextJSON.toggle.interactable = false;
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
                _nextAnimationPreviewJSON.val = $"Will loop {Math.Round(current.nextAnimationTime / current.animationLength, 2)} times";
            }

            if (current.nextAnimationTimeRandomize > 0f)
            {
                _nextAnimationPreviewJSON.val += $"\nRandomized up to {Math.Round((current.nextAnimationTime + current.nextAnimationTimeRandomize) / current.animationLength, 2)} times";
            }
        }

        private List<string> GetEligibleNextAnimations()
        {
            var animations = currentLayer
                .Where(c => c.animationName != current.animationName)
                .Select(c => c.animationName)
                .GroupBy(x =>
                {
                    var i = x.IndexOf("/", StringComparison.Ordinal);
                    return i == -1 ? null : x.Substring(0, i);
                });
            var segments = animation.index.segmentNames
                .Where(s => s != AtomAnimationClip.SharedAnimationSegment && s != current.animationSegment)
                .Select(s => $"{AtomAnimation.NextAnimationSegmentPrefix}{s}");
            return new[] { _noNextAnimation }
                .Concat(animations.SelectMany(EnumerateAnimations))
                .Concat(new[] { AtomAnimation.RandomizeAnimationName })
                .Concat(segments)
                .ToList();
        }

        private static IEnumerable<string> EnumerateAnimations(IGrouping<string, string> group)
        {
            foreach (var groupName in group)
                yield return groupName;

            if (group.Key != null)
                yield return group.Key + AtomAnimation.RandomizeGroupSuffix;
        }

        #endregion

        #region Callbacks

        private void UpdateBlendDuration(float v)
        {
            if (current.applyPoseOnTransition)
            {
                _blendDurationJSON.valNoCallback = 0;
                return;
            }
            v = v.Snap();
            if (v < 0)
                _blendDurationJSON.valNoCallback = v = 0f;
            current.blendInDuration = v;
        }

        private void ChangeTransitionPrevious(bool val)
        {
            current.autoTransitionPrevious = val;
            RefreshTransitionUI();
            plugin.animationEditContext.Sample();
        }

        private void ChangeTransitionNext(bool val)
        {
            current.autoTransitionNext = val;
            RefreshTransitionUI();
            plugin.animationEditContext.Sample();
        }

        private void SyncPlayNext()
        {
            RoundNextTimeToNearestLoop();
            var nextTime = _nextAnimationTimeJSON.val;
            var nextName = _nextAnimationJSON.val;
            var randomizeTime = current.preserveLoops ? _randomizeRangeJSON.val.RoundToNearest(current.animationLength) : _randomizeRangeJSON.val.Snap(animationEditContext.snap);

            var clips = animation.index.ByName(current.animationName);

            if (nextName == AtomAnimation.SlaveAnimationName)
            {
                // Do nothing, but this shouldn't be "set"
            }
            else if (nextName == _noNextAnimation)
            {
                foreach (var clip in clips)
                {
                    clip.nextAnimationName = null;
                    clip.nextAnimationTime = 0f;
                    clip.nextAnimationTimeRandomize = 0f;
                    clip.nextAnimationRandomizeWeight = 1f;
                }
            }
            else
            {
                foreach (var clip in clips)
                {
                    if (!NextExists(clip, nextName))
                        continue;

                    if (!clip.loop)
                        nextTime = clip.nextAnimationTime == 0 ? clip.animationLength : Mathf.Min(nextTime, clip.animationLength);
                    else if (clip.nextAnimationTime == 0)
                        nextTime = clip.animationLength;

                    break;
                }

                foreach (var clip in clips)
                {
                    if (!NextExists(clip, nextName))
                    {
                        clip.nextAnimationName = null;
                        clip.nextAnimationTime = 0f;
                        clip.nextAnimationTimeRandomize = 0f;
                    }
                    else
                    {
                        clip.nextAnimationName = clip == current ? _nextAnimationJSON.val : AtomAnimation.SlaveAnimationName;
                        clip.nextAnimationTime = nextTime;
                        clip.nextAnimationTimeRandomize = randomizeTime;
                    }
                }

                _nextAnimationTimeJSON.valNoCallback = nextTime;
                _randomizeRangeJSON.valNoCallback = randomizeTime;
            }

            RefreshTransitionUI();
        }

        private bool NextExists(AtomAnimationClip clip, string nextName)
        {
            if (nextName == null)
                return false;

            if (nextName.StartsWith(AtomAnimation.NextAnimationSegmentPrefix))
                return true;

            if (nextName == AtomAnimation.RandomizeAnimationName)
                return true;

            if (nextName.EndsWith(AtomAnimation.RandomizeGroupSuffix))
                return true;

            return currentLayer.Any(c => c.animationName == nextName);
        }

        #endregion

        #region Events

        protected override void OnCurrentAnimationChanged(AtomAnimationEditContext.CurrentAnimationChangedEventArgs args)
        {
            base.OnCurrentAnimationChanged(args);

            args.before.onAnimationSettingsChanged.RemoveListener(OnAnimationSettingsChanged);
            args.after.onAnimationSettingsChanged.AddListener(OnAnimationSettingsChanged);

            UpdateValues();
        }

        private void OnAnimationSettingsChanged(string arg0)
        {
            UpdateValues();
        }

        private void UpdateValues()
        {
            _masterJSON.valNoCallback = animation.master;
            _autoPlayJSON.valNoCallback = current.autoPlay;
            _blendDurationJSON.valNoCallback = current.blendInDuration;
            _uninterruptible.valNoCallback = current.uninterruptible;
            _transitionPreviousJSON.valNoCallback = current.autoTransitionPrevious;
            _transitionNextJSON.valNoCallback = current.autoTransitionNext;
            _preserveLoopsJSON.valNoCallback = current.preserveLoops;
            _nextAnimationJSON.valNoCallback = string.IsNullOrEmpty(current.nextAnimationName) ? _noNextAnimation : current.nextAnimationName;
            _nextAnimationJSON.choices = GetEligibleNextAnimations();
            _nextAnimationTimeJSON.valNoCallback = current.nextAnimationTime;
            _nextAnimationTimeJSON.slider.enabled = current.nextAnimationName != null;
            _nextAnimationRandomizeWeightJSON.valNoCallback = current.nextAnimationRandomizeWeight;
            _randomizeRangeJSON.valNoCallback = current.nextAnimationTimeRandomize;
            _randomizeRangeJSON.slider.enabled = current.nextAnimationName != null;
            _animationSetJSON.valNoCallback = current.animationSet ?? (animation.index.ByName(current.animationName).Count > 1 ? _animationSetAuto : "");
            if (_applyPoseOnTransition != null)
            {
                _applyPoseOnTransition.valNoCallback = current.applyPoseOnTransition;
                _applyPoseOnTransition.toggle.interactable = current.pose != null;
            }
            RefreshTransitionUI();
            UpdateNextAnimationPreview();
        }

        public override void OnDestroy()
        {
            current.onAnimationSettingsChanged.RemoveListener(OnAnimationSettingsChanged);
            base.OnDestroy();
        }

        #endregion
    }
}

