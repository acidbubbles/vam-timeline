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
    public class AtomAnimationSettingsUI : AtomAnimationBaseUI
    {
        public const string ScreenName = "Animation Settings";
        public override string Name => ScreenName;

        public const string ChangeLengthModeLocked = "Length Locked";
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
        private JSONStorableFloat _blendDurationJSON;
        private JSONStorableBool _ensureQuaternionContinuity;
        private JSONStorableBool _loop;
        private JSONStorableStringChooser _nextAnimationJSON;
        private JSONStorableFloat _nextAnimationTimeJSON;
        private JSONStorableString _nextAnimationPreviewJSON;
        private JSONStorableBool _transitionJSON;
        private JSONStorableBool _autoPlayJSON;
        private JSONStorableStringChooser _linkedAnimationPatternJSON;
        private float _lengthWhenLengthModeChanged;

        public AtomAnimationSettingsUI(IAtomPlugin plugin)
            : base(plugin)
        {

        }

        #region Init

        public override void Init()
        {
            base.Init();

            // Left side

            InitAnimationSettingsUI(false);

            // Right side

            InitSequenceUI(true);

            InitMiscSettingsUI(true);

            InitAnimationPatternLinkUI(true);

            _lengthWhenLengthModeChanged = Plugin.Animation?.Current?.AnimationLength ?? 0;
        }

        private void InitAnimationSettingsUI(bool rightSide)
        {
            _lengthModeJSON = new JSONStorableStringChooser("Change Length Mode", new List<string> {
                ChangeLengthModeLocked,
                ChangeLengthModeCropExtendEnd,
                ChangeLengthModeAddKeyframeEnd,
                ChangeLengthModeCropExtendBegin,
                ChangeLengthModeAddKeyframeBegin,
                ChangeLengthModeCropExtendAtTime,
                ChangeLengthModeStretch,
                ChangeLengthModeLoop
             }, ChangeLengthModeLocked, "Change Length Mode", (string _) => _lengthWhenLengthModeChanged = Plugin.Animation?.Current?.AnimationLength ?? 0f);
            RegisterStorable(_lengthModeJSON);
            var lengthModeUI = Plugin.CreateScrollablePopup(_lengthModeJSON);
            lengthModeUI.popupPanelHeight = 550f;
            RegisterComponent(lengthModeUI);

            _lengthJSON = new JSONStorableFloat("AnimationLength", AtomAnimationClip.DefaultAnimationLength, v => UpdateAnimationLength(v), 0.5f, 10f, false, true);
            RegisterStorable(_lengthJSON);
            var lengthUI = Plugin.CreateSlider(_lengthJSON, rightSide);
            lengthUI.valueFormat = "F3";
            RegisterComponent(lengthUI);
            RegisterStorable(Plugin.SnapJSON);
            var snapUI = Plugin.CreateSlider(Plugin.SnapJSON);
            snapUI.valueFormat = "F3";
            RegisterComponent(snapUI);

            var addAnimationFromCurrentFrameUI = Plugin.CreateButton("Create Animation From Current Frame", rightSide);
            addAnimationFromCurrentFrameUI.button.onClick.AddListener(() => AddAnimationFromCurrentFrame());
            RegisterComponent(addAnimationFromCurrentFrameUI);

            var addAnimationAsCopyUI = Plugin.CreateButton("Create Copy Of Current Animation", rightSide);
            addAnimationAsCopyUI.button.onClick.AddListener(() => AddAnimationAsCopy());
            RegisterComponent(addAnimationAsCopyUI);

            RegisterStorable(_animationNameJSON);
            _animationNameJSON = new JSONStorableString("Animation Name", "", (string val) => UpdateAnimationName(val));
            var animationNameUI = Plugin.CreateTextInput(_animationNameJSON);
            RegisterComponent(animationNameUI);
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
                valNoCallback = Plugin.Animation.Current.NextAnimationTime
            };
            RegisterStorable(_nextAnimationTimeJSON);
            var nextAnimationTimeUI = Plugin.CreateSlider(_nextAnimationTimeJSON, rightSide);
            nextAnimationTimeUI.valueFormat = "F3";
            RegisterComponent(nextAnimationTimeUI);

            _nextAnimationPreviewJSON = new JSONStorableString("Next Preview", "");
            RegisterStorable(_nextAnimationPreviewJSON);
            var nextAnimationResultUI = Plugin.CreateTextField(_nextAnimationPreviewJSON, rightSide);
            nextAnimationResultUI.height = 30f;
            RegisterComponent(nextAnimationResultUI);

            _blendDurationJSON = new JSONStorableFloat("BlendDuration", AtomAnimationClip.DefaultBlendDuration, v => UpdateBlendDuration(v), 0f, 5f, false);
            RegisterStorable(_blendDurationJSON);
            var blendDurationUI = Plugin.CreateSlider(_blendDurationJSON, rightSide);
            blendDurationUI.valueFormat = "F3";
            RegisterComponent(blendDurationUI);

            UpdateNextAnimationPreview();
        }

        private void InitMiscSettingsUI(bool rightSide)
        {
            RegisterStorable(Plugin.SpeedJSON);
            var speedUI = Plugin.CreateSlider(Plugin.SpeedJSON, rightSide);
            speedUI.valueFormat = "F3";
            RegisterComponent(speedUI);

            _loop = new JSONStorableBool("Loop", Plugin.Animation?.Current?.Loop ?? true, (bool val) => ChangeLoop(val));
            RegisterStorable(_loop);
            var loopingUI = Plugin.CreateToggle(_loop, rightSide);
            RegisterComponent(loopingUI);

            _transitionJSON = new JSONStorableBool("Transition (Sync First/Last Frames)", false, (bool val) => ChangeTransition(val));
            RegisterStorable(_transitionJSON);
            var transitionUI = Plugin.CreateToggle(_transitionJSON, rightSide);
            RegisterComponent(transitionUI);

            _ensureQuaternionContinuity = new JSONStorableBool("Ensure Quaternion Continuity", true, (bool val) => SetEnsureQuaternionContinuity(val));
            RegisterStorable(_ensureQuaternionContinuity);
            var ensureQuaternionContinuityUI = Plugin.CreateToggle(_ensureQuaternionContinuity, rightSide);
            RegisterComponent(ensureQuaternionContinuityUI);

            _autoPlayJSON = new JSONStorableBool("Auto Play On Load", false, (bool val) =>
            {
                foreach (var c in Plugin.Animation.Clips)
                    c.AutoPlay = false;
                Plugin.Animation.Current.AutoPlay = true;
            })
            {
                isStorable = false
            };
            RegisterStorable(_autoPlayJSON);
            var autoPlayUI = Plugin.CreateToggle(_autoPlayJSON, rightSide);
            RegisterComponent(autoPlayUI);
        }

        private void UpdateForcedNextAnimationTime()
        {
            if (Plugin.Animation.Current.Loop) return;
            if (Plugin.Animation.Current.NextAnimationName == null)
            {
                Plugin.Animation.Current.NextAnimationTime = 0;
                _nextAnimationTimeJSON.valNoCallback = 0;
            }
            Plugin.Animation.Current.NextAnimationTime = (Plugin.Animation.Current.AnimationLength - Plugin.Animation.Current.BlendDuration).Snap();
            _nextAnimationTimeJSON.valNoCallback = Plugin.Animation.Current.NextAnimationTime;
        }

        private void UpdateNextAnimationPreview()
        {
            var current = Plugin.Animation.Current;

            if (current.NextAnimationName == null)
            {
                _nextAnimationPreviewJSON.val = "No next animation configured";
                return;
            }

            if (!current.Loop)
            {
                _nextAnimationPreviewJSON.val = $"Will play once and blend at {current.NextAnimationTime}s";
                return;
            }

            if (_nextAnimationTimeJSON.val.IsSameFrame(0))
            {
                _nextAnimationPreviewJSON.val = "Will loop indefinitely";
            }
            else
            {
                _nextAnimationPreviewJSON.val = $"Will loop {Math.Round((current.NextAnimationTime + current.BlendDuration) / current.AnimationLength, 2)} times including blending";
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
                .Where(n => n != Plugin.Animation.Current.AnimationName)
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

        private void AddAnimationAsCopy()
        {
            var current = Plugin.Animation.Current;
            var clip = Plugin.Animation.AddAnimation();
            clip.Loop = current.Loop;
            clip.NextAnimationName = current.NextAnimationName;
            clip.NextAnimationTime = current.NextAnimationTime;
            clip.EnsureQuaternionContinuity = current.EnsureQuaternionContinuity;
            clip.BlendDuration = current.BlendDuration;
            clip.CropOrExtendLengthEnd(current.AnimationLength);
            foreach (var origTarget in current.TargetControllers)
            {
                var newTarget = clip.Add(origTarget.Controller);
                for (var i = 0; i < origTarget.Curves.Count; i++)
                {
                    newTarget.Curves[i].keys = origTarget.Curves[i].keys.ToArray();
                }
                foreach (var kvp in origTarget.Settings)
                {
                    newTarget.Settings[kvp.Key] = new KeyframeSettings { CurveType = kvp.Value.CurveType };
                }
                newTarget.Dirty = true;
            }
            foreach (var origTarget in current.TargetFloatParams)
            {
                var newTarget = clip.Add(origTarget.Storable, origTarget.FloatParam);
                newTarget.Value.keys = origTarget.Value.keys.ToArray();
                newTarget.Dirty = true;
            }
            Plugin.Animation.RebuildAnimation();
            Plugin.Animation.ChangeAnimation(clip.AnimationName);
            Plugin.AnimationModified();
        }

        private void AddAnimationFromCurrentFrame()
        {
            var current = Plugin.Animation.Current;
            var clip = Plugin.Animation.AddAnimation();
            clip.Loop = current.Loop;
            clip.NextAnimationName = current.NextAnimationName;
            clip.NextAnimationTime = current.NextAnimationTime;
            clip.EnsureQuaternionContinuity = current.EnsureQuaternionContinuity;
            clip.BlendDuration = current.BlendDuration;
            clip.CropOrExtendLengthEnd(current.AnimationLength);
            foreach (var origTarget in current.TargetControllers)
            {
                var newTarget = clip.Add(origTarget.Controller);
                newTarget.SetKeyframeToCurrentTransform(0f);
                newTarget.SetKeyframeToCurrentTransform(clip.AnimationLength);
            }
            foreach (var origTarget in current.TargetFloatParams)
            {
                var newTarget = clip.Add(origTarget.Storable, origTarget.FloatParam);
                newTarget.SetKeyframe(0f, origTarget.FloatParam.val);
                newTarget.SetKeyframe(clip.AnimationLength, origTarget.FloatParam.val);
            }
            Plugin.Animation.ChangeAnimation(clip.AnimationName);
            Plugin.AnimationModified();
        }

        private void UpdateAnimationName(string val)
        {
            var previousAnimationName = Plugin.Animation.Current.AnimationName;
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
            Plugin.Animation.Current.AnimationName = val;
            foreach (var clip in Plugin.Animation.Clips)
            {
                if (clip.NextAnimationName == previousAnimationName)
                    clip.NextAnimationName = val;
            }
            Plugin.Animation.RebuildAnimation();
            Plugin.AnimationModified();
        }

        private void UpdateAnimationLength(float newLength)
        {
            if (_lengthWhenLengthModeChanged == 0f) return;

            newLength = newLength.Snap(Plugin.SnapJSON.val);
            if (newLength < 0.1f) newLength = 0.1f;
            var time = Plugin.Animation.Time.Snap();

            switch (_lengthModeJSON.val)
            {
                case ChangeLengthModeLocked:
                    {
                        _lengthJSON.valNoCallback = Plugin.Animation.Current.AnimationLength;
                        return;
                    }
                case ChangeLengthModeStretch:
                    Plugin.Animation.Current.StretchLength(newLength);
                    _lengthWhenLengthModeChanged = newLength;
                    break;
                case ChangeLengthModeCropExtendEnd:
                    Plugin.Animation.Current.CropOrExtendLengthEnd(newLength);
                    _lengthWhenLengthModeChanged = newLength;
                    break;
                case ChangeLengthModeCropExtendBegin:
                    Plugin.Animation.Current.CropOrExtendLengthBegin(newLength);
                    _lengthWhenLengthModeChanged = newLength;
                    break;
                case ChangeLengthModeCropExtendAtTime:
                    {
                        if (Plugin.Animation.IsPlaying())
                        {
                            _lengthJSON.valNoCallback = Plugin.Animation.Current.AnimationLength;
                            return;
                        }
                        var previousKeyframe = Plugin.Animation.Current.AllTargets.SelectMany(t => t.GetAllKeyframesTime()).Where(t => t <= time + 0.0011f).Max();
                        var nextKeyframe = Plugin.Animation.Current.AllTargets.SelectMany(t => t.GetAllKeyframesTime()).Where(t => t > time + 0.0001f).Min();

                        var keyframeAllowedDiff = (nextKeyframe - time - 0.001f).Snap();

                        if ((Plugin.Animation.Current.AnimationLength - newLength) > keyframeAllowedDiff)
                        {
                            newLength = Plugin.Animation.Current.AnimationLength - keyframeAllowedDiff;
                        }

                        Plugin.Animation.Current.CropOrExtendLengthAtTime(newLength, time);
                        break;
                    }
                case ChangeLengthModeAddKeyframeEnd:
                    {
                        if (newLength <= _lengthWhenLengthModeChanged + float.Epsilon)
                        {
                            _lengthJSON.valNoCallback = Plugin.Animation.Current.AnimationLength;
                            return;
                        }
                        var snapshot = Plugin.Animation.Current.Copy(_lengthWhenLengthModeChanged, true);
                        Plugin.Animation.Current.CropOrExtendLengthEnd(newLength);
                        Plugin.Animation.Current.Paste(_lengthWhenLengthModeChanged, snapshot);
                        break;
                    }
                case ChangeLengthModeAddKeyframeBegin:
                    {
                        if (newLength <= _lengthWhenLengthModeChanged + float.Epsilon)
                        {
                            _lengthJSON.valNoCallback = Plugin.Animation.Current.AnimationLength;
                            return;
                        }
                        var snapshot = Plugin.Animation.Current.Copy(0f, true);
                        Plugin.Animation.Current.CropOrExtendLengthBegin(newLength);
                        Plugin.Animation.Current.Paste((newLength - _lengthWhenLengthModeChanged).Snap(), snapshot);
                        break;
                    }
                case ChangeLengthModeLoop:
                    {
                        newLength = newLength.Snap(_lengthWhenLengthModeChanged);
                        var loops = (int)Math.Round(newLength / _lengthWhenLengthModeChanged);
                        if (loops <= 1 || newLength <= _lengthWhenLengthModeChanged)
                        {
                            _lengthJSON.valNoCallback = Plugin.Animation.Current.AnimationLength;
                            return;
                        }
                        var frames = Plugin.Animation.Current
                            .TargetControllers.SelectMany(t => t.GetLeadCurve().keys.Select(k => k.time))
                            .Concat(Plugin.Animation.Current.TargetFloatParams.SelectMany(t => t.Value.keys.Select(k => k.time)))
                            .Select(t => t.Snap())
                            .Where(t => t < _lengthWhenLengthModeChanged)
                            .Distinct()
                            .ToList();

                        var snapshots = frames.Select(f => Plugin.Animation.Current.Copy(f, true)).ToList();
                        foreach (var c in snapshots[0].Controllers)
                        {
                            c.Snapshot.CurveType = CurveTypeValues.Smooth;
                        }

                        Plugin.Animation.Current.CropOrExtendLengthEnd(newLength);

                        for (var repeat = 0; repeat < loops; repeat++)
                        {
                            for (var i = 0; i < frames.Count; i++)
                            {
                                var pasteTime = frames[i] + (_lengthWhenLengthModeChanged * repeat);
                                if (pasteTime >= newLength) continue;
                                Plugin.Animation.Current.Paste(pasteTime, snapshots[i]);
                            }
                        }
                    }
                    break;
                default:
                    SuperController.LogError($"VamTimeline: Unknown animation length type: {_lengthModeJSON.val}");
                    break;
            }

            Plugin.Animation.Current.DirtyAll();

            Plugin.Animation.RebuildAnimation();
            UpdateForcedNextAnimationTime();
            Plugin.AnimationModified();
            Plugin.Animation.Time = Math.Max(time, newLength);
        }

        private void UpdateBlendDuration(float v)
        {
            if (v < 0)
                _blendDurationJSON.valNoCallback = v = 0f;
            v = v.Snap();
            if (!Plugin.Animation.Current.Loop && v >= (Plugin.Animation.Current.AnimationLength - 0.001f))
                _blendDurationJSON.valNoCallback = v = (Plugin.Animation.Current.AnimationLength - 0.001f).Snap();
            Plugin.Animation.Current.BlendDuration = v;
            UpdateForcedNextAnimationTime();
            Plugin.AnimationModified();
        }

        private void ChangeLoop(bool val)
        {
            Plugin.Animation.Current.Loop = val;
            if (val == true)
            {
                foreach (var target in Plugin.Animation.Current.TargetControllers)
                {
                    if (target.Settings.Count == 2)
                        target.Settings[Plugin.Animation.Current.AnimationLength.ToMilliseconds()].CurveType = CurveTypeValues.LeaveAsIs;
                }
            }
            else
            {
                foreach (var target in Plugin.Animation.Current.TargetControllers)
                {
                    if (target.Settings.Count == 2)
                        target.Settings[Plugin.Animation.Current.AnimationLength.ToMilliseconds()].CurveType = CurveTypeValues.CopyPrevious;
                }
            }
            SetNextAnimationTime(
                Plugin.Animation.Current.NextAnimationTime == 0
                ? Plugin.Animation.Current.NextAnimationTime = Plugin.Animation.Current.AnimationLength - Plugin.Animation.Current.BlendDuration
                : Plugin.Animation.Current.NextAnimationTime
            );
            if (val)
            {
                _transitionJSON.valNoCallback = false;
                Plugin.Animation.Current.Transition = false;
            }
            Plugin.Animation.Current.DirtyAll();
            Plugin.Animation.RebuildAnimation();
            Plugin.AnimationModified();
        }

        private void ChangeTransition(bool val)
        {
            if (Plugin.Animation.Current.Loop) _loop.val = false;
            Plugin.Animation.Current.Transition = val;
            Plugin.Animation.Current.DirtyAll();
            Plugin.Animation.RebuildAnimation();
            Plugin.AnimationModified();
            Plugin.Animation.Sample();
        }

        private void SetEnsureQuaternionContinuity(bool val)
        {
            Plugin.Animation.Current.EnsureQuaternionContinuity = val;

            Plugin.Animation.Current.DirtyAll();

            Plugin.Animation.RebuildAnimation();
            Plugin.AnimationModified();
        }

        private void ChangeNextAnimation(string val)
        {
            Plugin.Animation.Current.NextAnimationName = val;
            SetNextAnimationTime(
                Plugin.Animation.Current.NextAnimationTime == 0
                ? Plugin.Animation.Current.NextAnimationTime = Plugin.Animation.Current.AnimationLength - Plugin.Animation.Current.BlendDuration
                : Plugin.Animation.Current.NextAnimationTime
            );
            Plugin.AnimationModified();
        }

        private void SetNextAnimationTime(float nextTime)
        {
            if (Plugin.Animation.Current.NextAnimationName == null)
            {
                _nextAnimationTimeJSON.valNoCallback = 0f;
                Plugin.Animation.Current.NextAnimationTime = 0f;
                return;
            }
            else if (!Plugin.Animation.Current.Loop)
            {
                nextTime = (Plugin.Animation.Current.AnimationLength - Plugin.Animation.Current.BlendDuration).Snap();
                Plugin.Animation.Current.NextAnimationTime = nextTime;
                _nextAnimationTimeJSON.valNoCallback = nextTime;
                return;
            }

            nextTime = nextTime.Snap();

            _nextAnimationTimeJSON.valNoCallback = nextTime;
            Plugin.Animation.Current.NextAnimationTime = nextTime;
            Plugin.AnimationModified();
        }

        private void LinkAnimationPattern(string uid)
        {
            if (string.IsNullOrEmpty(uid))
            {
                Plugin.Animation.Current.AnimationPattern = null;
                return;
            }
            var animationPattern = SuperController.singleton.GetAtomByUid(uid)?.GetComponentInChildren<AnimationPattern>();
            if (animationPattern == null)
            {
                SuperController.LogError($"VamTimeline: Could not find Animation Pattern '{uid}'");
                return;
            }
            Plugin.Animation.Current.AnimationPattern = animationPattern;
            animationPattern.SetBoolParamValue("autoPlay", false);
            animationPattern.SetBoolParamValue("pause", false);
            animationPattern.SetBoolParamValue("loop", false);
            animationPattern.SetBoolParamValue("loopOnce", false);
            animationPattern.SetFloatParamValue("speed", Plugin.Animation.Speed);
            animationPattern.ResetAnimation();
            Plugin.AnimationModified();
        }

        #endregion

        #region Events

        public override void AnimationModified()
        {
            base.AnimationModified();

            var current = Plugin.Animation.Current;
            _animationNameJSON.valNoCallback = current.AnimationName;
            _lengthJSON.valNoCallback = current.AnimationLength;
            _blendDurationJSON.valNoCallback = current.BlendDuration;
            _loop.valNoCallback = current.Loop;
            _transitionJSON.valNoCallback = current.Transition;
            _ensureQuaternionContinuity.valNoCallback = current.EnsureQuaternionContinuity;
            _nextAnimationJSON.valNoCallback = current.NextAnimationName;
            _nextAnimationJSON.choices = GetEligibleNextAnimations();
            _nextAnimationTimeJSON.valNoCallback = current.NextAnimationTime;
            _autoPlayJSON.valNoCallback = current.AutoPlay;
            _linkedAnimationPatternJSON.valNoCallback = current.AnimationPattern?.containingAtom.uid ?? "";
            UpdateNextAnimationPreview();
        }

        public override void Dispose()
        {
            base.Dispose();
        }

        #endregion
    }
}

