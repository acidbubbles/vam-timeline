using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class SequencingScreen : ScreenBase
    {
        public const string ScreenName = "Sequence";
        public const string NoNextAnimation = "[None]";

        public override string screenId => ScreenName;

        private JSONStorableBool _masterJSON;
        private JSONStorableBool _autoPlayJSON;
        private JSONStorableBool _loop;
        private UIDynamicToggle _loopUI;
        private JSONStorableBool _uninterruptible;
        private JSONStorableFloat _blendDurationJSON;
        private JSONStorableStringChooser _nextAnimationJSON;
        private JSONStorableFloat _nextAnimationTimeJSON;
        private JSONStorableString _nextAnimationPreviewJSON;
        private JSONStorableBool _transitionPreviousJSON;
        private JSONStorableBool _transitionNextJSON;
        private JSONStorableBool _transitionSyncTime;

        public SequencingScreen()
            : base()
        {
        }

        #region Init

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            CreateHeader("Sequence master", 1);
            InitSequenceMasterUI();

            CreateHeader("Auto play", 1);
            InitAutoPlayUI();

            CreateHeader("Blending", 1);
            InitBlendUI();

            CreateHeader("Sequence", 1);
            InitSequenceUI();
            InitUninterruptibleUI();

            CreateHeader("Transition (auto keyframes)", 1);
            InitLoopUI();
            InitTransitionUI();

            CreateHeader("Result", 1);
            InitPreviewUI();

            // To allow selecting in the popup
            prefabFactory.CreateSpacer().height = 200f;

            current.onAnimationSettingsChanged.AddListener(OnAnimationSettingsChanged);

            UpdateValues();
        }

        private void InitSequenceMasterUI()
        {
            _masterJSON = new JSONStorableBool("Master (atom controls others)", false, (bool val) =>
            {
                animation.master = val;
            })
            {
                isStorable = false
            };
            var masterUI = prefabFactory.CreateToggle(_masterJSON);
        }

        private void InitAutoPlayUI()
        {
            _autoPlayJSON = new JSONStorableBool("Auto play on load", false, (bool val) =>
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

        private void InitBlendUI()
        {
            _blendDurationJSON = new JSONStorableFloat("Blend-in duration", AtomAnimationClip.DefaultBlendDuration, v => UpdateBlendDuration(v), 0f, 5f, false);
            var blendDurationUI = prefabFactory.CreateSlider(_blendDurationJSON);
            blendDurationUI.valueFormat = "F3";

            _transitionSyncTime = new JSONStorableBool("Blend in sync", true, (bool val) => current.syncTransitionTime = val);
            prefabFactory.CreateToggle(_transitionSyncTime);
        }

        private void InitSequenceUI()
        {
            _nextAnimationJSON = new JSONStorableStringChooser("Play next", GetEligibleNextAnimations(), "", "Play next", (string val) => ChangeNextAnimation());
            var nextAnimationUI = prefabFactory.CreatePopup(_nextAnimationJSON, true, true);
            nextAnimationUI.popupPanelHeight = 360f;

            _nextAnimationTimeJSON = new JSONStorableFloat("... after seconds", 0f, (float val) => ChangeNextAnimation(), 0f, 60f, false)
            {
                valNoCallback = current.nextAnimationTime
            };
            var nextAnimationTimeUI = prefabFactory.CreateSlider(_nextAnimationTimeJSON);
            nextAnimationTimeUI.valueFormat = "F3";
        }

        private void InitPreviewUI()
        {
            _nextAnimationPreviewJSON = new JSONStorableString("Next preview", "");
            var nextAnimationResultUI = prefabFactory.CreateTextField(_nextAnimationPreviewJSON);
            nextAnimationResultUI.height = 50f;
        }

        private void InitTransitionUI()
        {
            _transitionPreviousJSON = new JSONStorableBool("Sync first frame with previous", false, (bool val) => ChangeTransitionPrevious(val));
            prefabFactory.CreateToggle(_transitionPreviousJSON);

            _transitionNextJSON = new JSONStorableBool("Sync last frame with next", false, (bool val) => ChangeTransitionNext(val));
            prefabFactory.CreateToggle(_transitionNextJSON);
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

        private void InitUninterruptibleUI()
        {
            _uninterruptible = new JSONStorableBool("Prevent trigger interruptions", current.uninterruptible, (bool val) =>
            {
                foreach (var clip in animation.GetClips(current.animationName))
                    clip.uninterruptible = val;
            });
            prefabFactory.CreateToggle(_uninterruptible);
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
                _loopUI.toggle.interactable = true;

                if (current.loop)
                {
                    _transitionNextJSON.toggle.interactable = false;
                }
                else
                {
                    var targetClip = animation.clips.FirstOrDefault(c => c != current && c.animationName == current.nextAnimationName);
                    if (targetClip == null || targetClip.autoTransitionNext == true)
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
                _nextAnimationPreviewJSON.val = $"Will loop {Math.Round((current.nextAnimationTime + current.blendInDuration) / current.animationLength, 2)} times including blending";
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

        private void UpdateBlendDuration(float v)
        {
            if (v < 0)
                _blendDurationJSON.valNoCallback = v = 0f;
            v = v.Snap();
            if (!current.loop && v >= (current.animationLength - 0.001f))
                _blendDurationJSON.valNoCallback = v = (current.animationLength - 0.001f).Snap();
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

        private void ChangeNextAnimation()
        {
            var nextTime = _nextAnimationTimeJSON.val.Snap();
            var nextName = _nextAnimationJSON.val;

            foreach (var clip in animation.GetClips(current.animationName))
            {
                if (nextName == NoNextAnimation)
                {
                    clip.nextAnimationName = null;
                    clip.nextAnimationTime = 0f;
                }
                else
                {
                    if (clip.nextAnimationName == null)
                        nextTime = Mathf.Max((clip.animationLength - clip.blendInDuration).Snap(), 0f);
                    else
                        nextTime = clip.loop ? nextTime : Mathf.Min(nextTime, clip.animationLength);
                    clip.nextAnimationName = _nextAnimationJSON.val;
                    clip.nextAnimationTime = nextTime;
                }
            }
            RefreshTransitionUI();
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
            _autoPlayJSON.valNoCallback = current.autoPlay;
            _blendDurationJSON.valNoCallback = current.blendInDuration;
            _transitionPreviousJSON.valNoCallback = current.autoTransitionPrevious;
            _transitionNextJSON.valNoCallback = current.autoTransitionNext;
            _transitionSyncTime.valNoCallback = current.syncTransitionTime;
            _nextAnimationJSON.valNoCallback = string.IsNullOrEmpty(current.nextAnimationName) ? NoNextAnimation : current.nextAnimationName;
            _nextAnimationJSON.choices = GetEligibleNextAnimations();
            _nextAnimationTimeJSON.valNoCallback = current.nextAnimationTime;
            _nextAnimationTimeJSON.slider.enabled = current.nextAnimationName != null;
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

