using System.Collections;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class AnimationsScreen : ScreenBase
    {
        public const string ScreenName = "Animations";

        public override string screenId => ScreenName;

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            prefabFactory.CreateHeader("Animations", 1);

            InitClipsUI();

            prefabFactory.CreateSpacer();

            prefabFactory.CreateHeader("Operations", 1);

            CreateChangeScreenButton("<i><b>Add</b> animations/layers...</i>", AddAnimationScreen.ScreenName);
            CreateChangeScreenButton("<i><b>Manage</b> animations list...</i>", ManageAnimationsScreen.ScreenName);
        }

        private void InitClipsUI()
        {
            if (!animation.clips.Any()) return;

            var hasLayers = animation.EnumerateLayers().Skip(1).Any();

            var layerName = animation.clips[0].animationLayer;
            if (hasLayers)
                prefabFactory.CreateHeader($"Layer: [{layerName}]", 2);

            foreach (var clip in animation.clips)
            {
                if (hasLayers && clip.animationLayer != layerName)
                {
                    layerName = clip.animationLayer;
                    prefabFactory.CreateHeader($"Layer: [{layerName}]", 2);
                }

                InitAnimButton(clip);
            }
        }

        private void InitAnimButton(AtomAnimationClip clip)
        {
            var btn = prefabFactory.CreateButton("");
            btn.buttonText.alignment = TextAnchor.MiddleLeft;
            btn.button.onClick.AddListener(() =>
            {
                if (clip.playbackMainInLayer)
                {
                    animation.StopClip(clip);
                }
                else if (!clip.playbackEnabled)
                {
                    animation.PlayClips(clip.animationName, true);
                }
            });
            StartCoroutine(UpdateAnimButton(btn, clip));
        }

        private IEnumerator UpdateAnimButton(UIDynamicButton btn, AtomAnimationClip clip)
        {
            yield return 0;
            var playLabel = $" \u25B6 {clip.animationName}";
            while (!disposing)
            {
                if (UIPerformance.ShouldSkip(UIPerformance.LowFrequency))
                    yield return 0;

                if (!clip.playbackMainInLayer)
                {
                    if (btn.label != playLabel)
                        btn.label = playLabel;
                }
                else
                {
                    //btn.label = $" \u25A0 [{clip.clipTime:00.000}, weight: {Mathf.Round(clip.playbackBlendWeight * 100f):000}%]";
                    btn.label = $" \u25A0 [{clip.clipTime:00.00}] {clip.animationName}";
                }

                yield return 0;
            }
        }
    }
}

