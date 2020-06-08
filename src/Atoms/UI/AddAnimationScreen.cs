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

        public override string name => ScreenName;

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
            var addAnimationFromCurrentFrameUI = plugin.CreateButton("Create Animation From Current Frame", rightSide);
            addAnimationFromCurrentFrameUI.button.onClick.AddListener(() => AddAnimationFromCurrentFrame());
            RegisterComponent(addAnimationFromCurrentFrameUI);

            var addAnimationAsCopyUI = plugin.CreateButton("Create Copy Of Current Animation", rightSide);
            addAnimationAsCopyUI.button.onClick.AddListener(() => AddAnimationAsCopy());
            RegisterComponent(addAnimationAsCopyUI);
        }

        #endregion

        #region Callbacks

        private void AddAnimationAsCopy()
        {
            var clip = plugin.animation.AddAnimation();
            clip.loop = current.loop;
            clip.NextAnimationName = current.NextAnimationName;
            clip.NextAnimationTime = current.NextAnimationTime;
            clip.EnsureQuaternionContinuity = current.EnsureQuaternionContinuity;
            clip.BlendDuration = current.BlendDuration;
            clip.CropOrExtendLengthEnd(current.animationLength);
            foreach (var origTarget in current.TargetControllers)
            {
                var newTarget = clip.Add(origTarget.controller);
                for (var i = 0; i < origTarget.curves.Count; i++)
                {
                    newTarget.curves[i].keys = origTarget.curves[i].keys.ToArray();
                }
                foreach (var kvp in origTarget.settings)
                {
                    newTarget.settings[kvp.Key] = new KeyframeSettings { curveType = kvp.Value.curveType };
                }
                newTarget.dirty = true;
            }
            foreach (var origTarget in current.TargetFloatParams)
            {
                var newTarget = clip.Add(origTarget.storable, origTarget.floatParam);
                newTarget.value.keys = origTarget.value.keys.ToArray();
                newTarget.dirty = true;
            }

            plugin.animation.ChangeAnimation(clip.AnimationName);
            onScreenChangeRequested.Invoke(EditScreen.ScreenName);
        }

        private void AddAnimationFromCurrentFrame()
        {
            var clip = plugin.animation.AddAnimation();
            clip.loop = current.loop;
            clip.NextAnimationName = current.NextAnimationName;
            clip.NextAnimationTime = current.NextAnimationTime;
            clip.EnsureQuaternionContinuity = current.EnsureQuaternionContinuity;
            clip.BlendDuration = current.BlendDuration;
            clip.CropOrExtendLengthEnd(current.animationLength);
            foreach (var origTarget in current.TargetControllers)
            {
                var newTarget = clip.Add(origTarget.controller);
                newTarget.SetKeyframeToCurrentTransform(0f);
                newTarget.SetKeyframeToCurrentTransform(clip.animationLength);
            }
            foreach (var origTarget in current.TargetFloatParams)
            {
                var newTarget = clip.Add(origTarget.storable, origTarget.floatParam);
                newTarget.SetKeyframe(0f, origTarget.floatParam.val);
                newTarget.SetKeyframe(clip.animationLength, origTarget.floatParam.val);
            }

            plugin.animation.ChangeAnimation(clip.AnimationName);
            onScreenChangeRequested.Invoke(EditScreen.ScreenName);
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

