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
    public class MorphsPluginImpl : PluginImplBase<JSONStorableFloatAnimation, JSONStorableFloatAnimationClip, JSONStorableFloatAnimationTarget>
    {
        private class MorphJSONRef
        {
            public JSONStorableFloat Original;
            public JSONStorableFloat Local;
        }

        private MorphsList _morphsList;
        private List<MorphJSONRef> _morphJSONRefs;

        // Backup
        protected override string BackupStorableName => StorableNames.MorphsAnimationBackup;

        public MorphsPluginImpl(IAnimationPlugin plugin)
            : base(plugin)
        {
        }

        #region Initialization

        public void Init()
        {
            if (_plugin.ContainingAtom.type != "Person")
            {
                SuperController.LogError("VamTimeline.MorphsAnimation can only be applied on a Person atom.");
                return;
            }

            _morphsList = new MorphsList(_plugin.ContainingAtom);

            RegisterSerializer(new MorphsAnimationSerializer(_plugin.ContainingAtom));
            InitStorables();
            InitCustomUI();
            // Try loading from backup
            _plugin.StartCoroutine(CreateAnimationIfNoneIsLoaded());
        }

        private void InitStorables()
        {
            InitCommonStorables();
        }

        private void InitCustomUI()
        {
            // Left side

            InitPlaybackUI(false);

            InitFrameNavUI(false);

            InitClipboardUI(false);

            InitAnimationSettingsUI(false);

            // Right side

            InitDisplayUI(true);

            InitMorphsListUI();
        }

        private void InitMorphsListUI()
        {
            _morphsList.Refresh();
            _morphJSONRefs = new List<MorphJSONRef>();
            foreach (var morphJSONRef in _morphsList.GetAnimatableMorphs())
            {
                var morphJSON = new JSONStorableFloat($"Morph:{morphJSONRef.name}", morphJSONRef.defaultVal, (float val) => UpdateMorph(morphJSONRef, val), morphJSONRef.min, morphJSONRef.max, morphJSONRef.constrained, true);
                _plugin.CreateSlider(morphJSON, true);
                _morphJSONRefs.Add(new MorphJSONRef
                {
                    Original = morphJSONRef,
                    Local = morphJSON,
                });
            }
        }

        #endregion

        #region Lifecycle

        protected override void UpdatePlaying()
        {
            _animation.Update();

            if (!_lockedJSON.val)
                ContextUpdatedCustom();
        }

        protected override void UpdateNotPlaying()
        {
        }

        public void OnEnable()
        {
        }

        public void OnDisable()
        {
            if (_animation == null) return;

            _animation.Stop();
        }

        public void OnDestroy()
        {
        }

        #endregion

        #region Callbacks

        private void UpdateMorph(JSONStorableFloat morphJSONRef, float val)
        {
            morphJSONRef.val = val;
            // TODO: This should be done by the controller (updating the animation resets the time)
            var time = _animation.Time;
            var target = _animation.Current.Targets.FirstOrDefault(m => m.Name == morphJSONRef.name);
            if (target == null)
            {

                // TODO: This is temporary for testing
                target = new JSONStorableFloatAnimationTarget(morphJSONRef, _animation.AnimationLength);
                target.SetKeyframe(0, val);
                _animation.Current.Targets.Add(target);
            }
            target.SetKeyframe(time, val);
            _animation.RebuildAnimation();
            UpdateTime(time);
            AnimationUpdated();
        }

        #endregion

        #region Updates

        protected override void AnimationUpdatedCustom()
        {
        }

        protected override void ContextUpdatedCustom()
        {
            foreach (var morphJSONRef in _morphJSONRefs)
                morphJSONRef.Local.valNoCallback = morphJSONRef.Original.val;
        }

        #endregion
    }
}
