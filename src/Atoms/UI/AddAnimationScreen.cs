using System.Linq;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class AddAnimationScreen : ScreenBase
    {
        public const string ScreenName = "Add Animation";
        public override string Name => ScreenName;

        public AddAnimationScreen(IAtomPlugin plugin)
            : base(plugin)
        {

        }

        #region Init

        public override void Init()
        {
            base.Init();

            // Right side

            CreateChangeScreenButton("<b><</b> <i>Back</i>", MoreScreen.ScreenName, true);

            CreateSpacer(true);

            InitCreateAnimationUI(true);

            CreateSpacer(true);

            CreateChangeScreenButton("<i><b>Edit</b> animation settings...</i>", EditAnimationScreen.ScreenName, true);
            CreateChangeScreenButton("<i><b>Reorder</b> and <b>delete</b> animations...</i>", ManageAnimationsScreen.ScreenName, true);
        }

        private void InitCreateAnimationUI(bool rightSide)
        {
            var addAnimationFromCurrentFrameUI = Plugin.CreateButton("Create Animation From Current Frame", rightSide);
            addAnimationFromCurrentFrameUI.button.onClick.AddListener(() => AddAnimationFromCurrentFrame());
            RegisterComponent(addAnimationFromCurrentFrameUI);

            var addAnimationAsCopyUI = Plugin.CreateButton("Create Copy Of Current Animation", rightSide);
            addAnimationAsCopyUI.button.onClick.AddListener(() => AddAnimationAsCopy());
            RegisterComponent(addAnimationAsCopyUI);
        }

        #endregion

        #region Callbacks

        private void AddAnimationAsCopy()
        {
            var clip = Plugin.Animation.AddAnimation();
            clip.Loop = Current.Loop;
            clip.NextAnimationName = Current.NextAnimationName;
            clip.NextAnimationTime = Current.NextAnimationTime;
            clip.EnsureQuaternionContinuity = Current.EnsureQuaternionContinuity;
            clip.BlendDuration = Current.BlendDuration;
            clip.CropOrExtendLengthEnd(Current.AnimationLength);
            foreach (var origTarget in Current.TargetControllers)
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
            foreach (var origTarget in Current.TargetFloatParams)
            {
                var newTarget = clip.Add(origTarget.Storable, origTarget.FloatParam);
                newTarget.Value.keys = origTarget.Value.keys.ToArray();
                newTarget.Dirty = true;
            }
            // TODO: The animation was built before, now it's built after. Make this this works.
            Plugin.Animation.ChangeAnimation(clip.AnimationName);
        }

        private void AddAnimationFromCurrentFrame()
        {
            var clip = Plugin.Animation.AddAnimation();
            clip.Loop = Current.Loop;
            clip.NextAnimationName = Current.NextAnimationName;
            clip.NextAnimationTime = Current.NextAnimationTime;
            clip.EnsureQuaternionContinuity = Current.EnsureQuaternionContinuity;
            clip.BlendDuration = Current.BlendDuration;
            clip.CropOrExtendLengthEnd(Current.AnimationLength);
            foreach (var origTarget in Current.TargetControllers)
            {
                var newTarget = clip.Add(origTarget.Controller);
                newTarget.SetKeyframeToCurrentTransform(0f);
                newTarget.SetKeyframeToCurrentTransform(clip.AnimationLength);
            }
            foreach (var origTarget in Current.TargetFloatParams)
            {
                var newTarget = clip.Add(origTarget.Storable, origTarget.FloatParam);
                newTarget.SetKeyframe(0f, origTarget.FloatParam.val);
                newTarget.SetKeyframe(clip.AnimationLength, origTarget.FloatParam.val);
            }
            Plugin.Animation.ChangeAnimation(clip.AnimationName);
        }

        #endregion

        #region Events

        protected override void OnCurrentAnimationChanged(AtomAnimation.CurrentAnimationChangedEventArgs args)
        {
            base.OnCurrentAnimationChanged(args);
        }

        #endregion
    }
}

