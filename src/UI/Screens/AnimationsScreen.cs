using System.Collections;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class AnimationsScreen : ScreenBase
    {
        public const string ScreenName = "Animations";

        public override string screenId => ScreenName;

        public AnimationsScreen()
            : base()
        {
        }

        public override void Init(IAtomPlugin plugin)
        {
            base.Init(plugin);

            CreateHeader("Animations", 1);

            InitClipsUI();

            prefabFactory.CreateSpacer();

            CreateHeader("Operations", 1);

            CreateChangeScreenButton("<i><b>Add</b> animations/layers...</i>", AddAnimationScreen.ScreenName);
            CreateChangeScreenButton("<i><b>Manage</b> animations list...</i>", ManageAnimationsScreen.ScreenName);
        }

        private void InitClipsUI()
        {
            if (!animation.clips.Any()) return;

            var hasLayers = animation.EnumerateLayers().Skip(1).Any();

            var layerName = animation.clips[0].animationLayer;
            if (hasLayers)
                CreateHeader($"Layer: [{layerName}]", 2);

            foreach (var clip in animation.clips)
            {
                if (hasLayers && clip.animationLayer != layerName)
                {
                    layerName = clip.animationLayer;
                    CreateHeader($"Layer: [{layerName}]", 2);
                }

                InitAnimButton(clip);
            }
        }

        private void InitAnimButton(AtomAnimationClip clip)
        {
            var btn = prefabFactory.CreateButton($"...");
            btn.buttonText.alignment = TextAnchor.MiddleLeft;
            btn.button.onClick.AddListener(() =>
            {
                if (clip.playbackEnabled)
                {
                    animation.StopClip(clip);
                }
                else
                {
                    animation.PlayClip(clip, true);
                }
            });
            StartCoroutine(UpdateAnimButton(btn, clip));
        }

        private IEnumerator UpdateAnimButton(UIDynamicButton btn, AtomAnimationClip clip)
        {
            yield return 0;
            var playLabel = $" \u25B6 {clip.animationName}";
            while (!_disposing)
            {
                if (!clip.playbackEnabled)
                {
                    if (btn.label != playLabel)
                        btn.label = playLabel;
                }
                else
                {
                    btn.label = $" \u25A0 [time: {clip.clipTime:00.000}, weight: {Mathf.Round(clip.weight * 100f):000}%]";
                }

                for (var i = 0; i < 4; i++)
                    yield return 0;
            }
        }
    }
}

