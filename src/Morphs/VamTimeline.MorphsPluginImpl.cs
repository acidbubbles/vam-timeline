using System;
using System.Linq;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class MorphsPluginImpl : PluginImplBase<JSONStorableFloatAnimation>
    {
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
            var geometry = _plugin.ContainingAtom.GetStorableByID("geometry");
            if (geometry == null) throw new NullReferenceException("geometry");
            var character = geometry as DAZCharacterSelector;
            if (character == null) throw new NullReferenceException("character");
            var morphControl = character.morphsControlUI;
            if (morphControl == null) throw new NullReferenceException("morphControl");
            foreach (var morphDisplayName in morphControl.GetMorphDisplayNames())
            {
                var morph = morphControl.GetMorphByDisplayName(morphDisplayName);
                if (morph == null) continue;

                if (morph.animatable)
                {
                    var morphJSON = new JSONStorableFloat($"Morph:{morphDisplayName}", morph.jsonFloat.defaultVal, (float val) => UpdateMorph(morph, val), morph.jsonFloat.min, morph.jsonFloat.max, morph.jsonFloat.constrained, true);
                    _plugin.CreateSlider(morphJSON, true);
                }
            }
        }

        #endregion

        #region Lifecycle

        public void Update()
        {
        }

        public void OnEnable()
        {
        }

        public void OnDisable()
        {
        }

        public void OnDestroy()
        {
        }

        #endregion

        #region Callbacks

        private void UpdateMorph(DAZMorph morph, float val)
        {
            morph.jsonFloat.val = val;
            // TODO: This should be done by the controller (updating the animation resets the time)
            var time = _animation.Time;
            var target = _animation.Current.Morphs.FirstOrDefault(m => m.Name == morph.jsonFloat.name);
            if (target == null)
            {

                // TODO: This is temporary for testing
                target = new JSONStorableFloatAnimationTarget(morph.jsonFloat, _animation.AnimationLength);
                target.SetKeyframe(0, val);
                _animation.Current.Morphs.Add(target);
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

        #endregion
    }
}
