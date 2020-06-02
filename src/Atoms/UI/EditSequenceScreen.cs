using System;
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
    public class EditSequenceScreen : ScreenBase
    {
        public const string ScreenName = "Edit Sequence";
        public override string Name => ScreenName;

        private JSONStorableBool _loop;
        private JSONStorableFloat _blendDurationJSON;
        private JSONStorableStringChooser _nextAnimationJSON;
        private JSONStorableFloat _nextAnimationTimeJSON;
        private JSONStorableString _nextAnimationPreviewJSON;
        private JSONStorableBool _transitionJSON;
        private UIDynamicToggle _transitionUI;
        private UIDynamicToggle _loopUI;

        public EditSequenceScreen(IAtomPlugin plugin)
            : base(plugin)
        {

        }

        #region Init

        public override void Init()
        {
            base.Init();

            // Left side

            InitPreviewUI(false);

            // Right side

            CreateChangeScreenButton("<b><</b> <i>Back</i>", MoreScreen.ScreenName, true);

            CreateSpacer(true);

            InitSequenceUI(true);

            CreateSpacer(true);

            InitTransitionUI(true);

            InitLoopUI(true);

            UpdateValues();
        }

        private void InitSequenceUI(bool rightSide)
        {
            _nextAnimationJSON = new JSONStorableStringChooser("Next Animation", GetEligibleNextAnimations(), "", "Next Animation", (string val) => ChangeNextAnimation(val));
            RegisterStorable(_nextAnimationJSON);
            var nextAnimationUI = Plugin.CreateScrollablePopup(_nextAnimationJSON, rightSide);
            nextAnimationUI.popupPanelHeight = 260f;
            RegisterComponent(nextAnimationUI);

            _nextAnimationTimeJSON = new JSONStorableFloat("Next Blend After Seconds", 0f, (float val) => SetNextAnimationTime(val), 0f, 60f, false)
            {
                valNoCallback = Current.NextAnimationTime
            };
            RegisterStorable(_nextAnimationTimeJSON);
            var nextAnimationTimeUI = Plugin.CreateSlider(_nextAnimationTimeJSON, rightSide);
            nextAnimationTimeUI.valueFormat = "F3";
            RegisterComponent(nextAnimationTimeUI);

            _blendDurationJSON = new JSONStorableFloat("BlendDuration", AtomAnimationClip.DefaultBlendDuration, v => UpdateBlendDuration(v), 0f, 5f, false);
            RegisterStorable(_blendDurationJSON);
            var blendDurationUI = Plugin.CreateSlider(_blendDurationJSON, rightSide);
            blendDurationUI.valueFormat = "F3";
            RegisterComponent(blendDurationUI);

            UpdateNextAnimationPreview();
        }

        private void InitPreviewUI(bool rightSide)
        {
            _nextAnimationPreviewJSON = new JSONStorableString("Next Preview", "");
            RegisterStorable(_nextAnimationPreviewJSON);
            var nextAnimationResultUI = Plugin.CreateTextField(_nextAnimationPreviewJSON, rightSide);
            nextAnimationResultUI.height = 30f;
            RegisterComponent(nextAnimationResultUI);
        }

        private void InitTransitionUI(bool rightSide)
        {
            var transitionLabelJSON = new JSONStorableString("Transition (Help)", "<b>Transition animations</b> can be enabled when there is an animation targeting the current animation, and when the current animation has a next animation configured. Only non-looping animations can be transition animations. This will automatically copy the last frame from the previous animation and the first frame from the next animation.");
            RegisterStorable(transitionLabelJSON);
            var transitionLabelUI = Plugin.CreateTextField(transitionLabelJSON, rightSide);
            RegisterComponent(transitionLabelUI);
            // var layout = animationNameLabelUI.GetComponent<LayoutElement>();
            // layout.minHeight = 36f;
            transitionLabelUI.height = 340f;
            // UnityEngine.Object.Destroy(animationNameLabelUI.gameObject.GetComponentInChildren<Image>());

            _transitionJSON = new JSONStorableBool("Transition", false, (bool val) => ChangeTransition(val));
            RegisterStorable(_transitionJSON);
            _transitionUI = Plugin.CreateToggle(_transitionJSON, rightSide);
            RegisterComponent(_transitionUI);
        }

        private void InitLoopUI(bool rightSide)
        {
            _loop = new JSONStorableBool("Loop", Current?.Loop ?? true, (bool val) =>
            {
                Current.Loop = val;
                UpdateNextAnimationPreview();
                RefreshTransitionUI();
            });
            RegisterStorable(_loop);
            _loopUI = Plugin.CreateToggle(_loop, rightSide);
            RegisterComponent(_loopUI);
        }

        private void RefreshTransitionUI()
        {
            if (Current.Loop)
            {
                _transitionUI.toggle.interactable = false;
                _loopUI.toggle.interactable = true;
                return;
            }
            var clipsPointingToHere = Plugin.Animation.Clips.Where(c => c != Current && c.NextAnimationName == Current.AnimationName).ToList();
            var targetClip = Plugin.Animation.Clips.FirstOrDefault(c => c != Current && c.AnimationName == Current.NextAnimationName);
            if (clipsPointingToHere.Count == 0 || targetClip == null)
            {
                _transitionUI.toggle.interactable = false;
                _loopUI.toggle.interactable = true;
                return;
            }

            if (clipsPointingToHere.Any(c => c.Transition) || targetClip?.Transition == true)
            {
                _transitionUI.toggle.interactable = false;
                _loopUI.toggle.interactable = true;
                return;
            }

            _transitionUI.toggle.interactable = true;
            _loopUI.toggle.interactable = !_transitionUI.toggle.isOn;
        }

        private void UpdateNextAnimationPreview()
        {
            if (Current.NextAnimationName == null)
            {
                _nextAnimationPreviewJSON.val = "No next animation configured";
                return;
            }

            if (!Current.Loop)
            {
                _nextAnimationPreviewJSON.val = $"Will play once and blend at {Current.NextAnimationTime}s";
                return;
            }

            if (_nextAnimationTimeJSON.val.IsSameFrame(0))
            {
                _nextAnimationPreviewJSON.val = "Will loop indefinitely";
            }
            else
            {
                _nextAnimationPreviewJSON.val = $"Will loop {Math.Round((Current.NextAnimationTime + Current.BlendDuration) / Current.AnimationLength, 2)} times including blending";
            }
        }

        private List<string> GetEligibleNextAnimations()
        {
            var animations = Plugin.Animation.GetAnimationNames()
                .GroupBy(x =>
                {
                    var i = x.IndexOf("/");
                    if (i == -1) return null;
                    return x.Substring(0, i);
                });
            return new[] { "" }
                .Concat(animations.SelectMany(EnumerateAnimations))
                .Where(n => n != Current.AnimationName)
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
            if (!Current.Loop && v >= (Current.AnimationLength - 0.001f))
                _blendDurationJSON.valNoCallback = v = (Current.AnimationLength - 0.001f).Snap();
            Current.BlendDuration = v;
        }

        private void ChangeTransition(bool val)
        {
            Current.Transition = val;
            RefreshTransitionUI();
            Plugin.SampleAfterRebuild();
        }

        private void ChangeNextAnimation(string val)
        {
            Current.NextAnimationName = val;
            SetNextAnimationTime(
                Current.NextAnimationTime == 0
                ? Current.NextAnimationTime = Current.AnimationLength - Current.BlendDuration
                : Current.NextAnimationTime
            );
            RefreshTransitionUI();
        }

        private void SetNextAnimationTime(float nextTime)
        {
            if (Current.NextAnimationName == null)
            {
                _nextAnimationTimeJSON.valNoCallback = 0f;
                Current.NextAnimationTime = 0f;
                return;
            }
            else if (!Current.Loop)
            {
                nextTime = (Current.AnimationLength - Current.BlendDuration).Snap();
                Current.NextAnimationTime = nextTime;
                _nextAnimationTimeJSON.valNoCallback = nextTime;
                return;
            }

            nextTime = nextTime.Snap();

            _nextAnimationTimeJSON.valNoCallback = nextTime;
            Current.NextAnimationTime = nextTime;
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
            _blendDurationJSON.valNoCallback = Current.BlendDuration;
            _loop.valNoCallback = Current.Loop;
            _transitionJSON.valNoCallback = Current.Transition;
            _nextAnimationJSON.valNoCallback = Current.NextAnimationName;
            _nextAnimationJSON.choices = GetEligibleNextAnimations();
            _nextAnimationTimeJSON.valNoCallback = Current.NextAnimationTime;
            RefreshTransitionUI();
            UpdateNextAnimationPreview();
        }

        public override void Dispose()
        {
            base.Dispose();
        }

        #endregion
    }
}

